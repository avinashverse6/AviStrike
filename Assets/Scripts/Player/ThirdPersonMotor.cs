using UnityEngine;

namespace CageDuel.Player
{
    /// <summary>
    /// CharacterController-based third-person movement motor supporting camera-planar strafing,
    /// jumping, crouch stance with ceiling clearance verification, and sandbox test damage.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class ThirdPersonMotor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputRouter inputRouter;
        [SerializeField] private ThirdPersonCameraRig cameraRig;
        [SerializeField] private HealthArmor vitals;
        [SerializeField] private Transform visualBodyRoot;

        [Header("Locomotion")]
        [SerializeField] private float walkSpeed = 5.6f;
        [SerializeField] private float crouchSpeed = 3.25f;
        [SerializeField] private float rotationSharpness = 18f;
        [SerializeField] private float jumpHeight = 1.25f;
        [SerializeField] private float gravity = -22f;

        [Header("Stance Dimensions")]
        [SerializeField] private float standingHeight = 1.8f;
        [SerializeField] private float crouchingHeight = 1.15f;
        [SerializeField] private float visualCrouchScaleY = 0.64f;

        private CharacterController _controller;
        private float _verticalVelocity;
        private bool _isCrouching;
        private bool _movementEnabled = true;
        private float _currentVisualScaleY = 1f;

        public bool IsCrouching => _isCrouching;
        public bool IsGrounded => _controller != null && _controller.isGrounded;
        public Vector3 HorizontalVelocity { get; private set; }

        public void Configure(
            PlayerInputRouter router,
            ThirdPersonCameraRig camRig,
            HealthArmor healthArmor,
            Transform bodyVisualRoot)
        {
            inputRouter = router;
            cameraRig = camRig;
            vitals = healthArmor;
            visualBodyRoot = bodyVisualRoot;
        }

        public void SetMovementEnabled(bool enabledState)
        {
            _movementEnabled = enabledState;
            if (!enabledState)
            {
                HorizontalVelocity = Vector3.zero;
            }
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (inputRouter == null) inputRouter = GetComponent<PlayerInputRouter>();
            if (vitals == null) vitals = GetComponent<HealthArmor>();
            ApplyControllerDimensions(false);
        }

        private void OnEnable()
        {
            if (vitals != null)
            {
                vitals.OnRespawned += HandleRespawned;
            }
        }

        private void OnDisable()
        {
            if (vitals != null)
            {
                vitals.OnRespawned -= HandleRespawned;
            }
        }

        private void Update()
        {
            bool isDead = vitals != null && vitals.IsDead;

            // Optional Editor self-damage test key ('K') to verify player armor/health/death/respawn in Phase 1
            if (!isDead && inputRouter != null && inputRouter.SelfDamageTestPressedThisFrame && vitals != null)
            {
                vitals.TakeDamage(new DamageInfo
                {
                    RawDamage = 30f,
                    HitPoint = transform.position + Vector3.up * 1.1f,
                    HitNormal = -transform.forward,
                    ShotDirection = transform.forward,
                    Attacker = gameObject,
                    ForceHeadshot = false
                });
                isDead = vitals.IsDead;
            }

            if (isDead || !_movementEnabled)
            {
                HorizontalVelocity = Vector3.zero;
                ApplyGravityOnly();
                return;
            }

            if (inputRouter != null && inputRouter.CrouchTogglePressedThisFrame)
            {
                if (_isCrouching)
                {
                    if (CanStandUp())
                    {
                        SetCrouchState(false);
                    }
                }
                else
                {
                    SetCrouchState(true);
                }
            }

            Vector2 moveInput = inputRouter != null ? inputRouter.MoveInput : Vector2.zero;
            Vector3 fwd = cameraRig != null ? cameraRig.PlanarForward : transform.forward;
            Vector3 right = cameraRig != null ? cameraRig.PlanarRight : transform.right;

            // Keep character horizontal yaw aligned with aim direction for responsive third-person shooting
            if (fwd.sqrMagnitude > 0.001f)
            {
                Quaternion desiredRot = Quaternion.LookRotation(fwd, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, Time.deltaTime * rotationSharpness);
            }

            float speed = _isCrouching ? crouchSpeed : walkSpeed;
            Vector3 desiredHorizontal = (right * moveInput.x + fwd * moveInput.y) * speed;
            HorizontalVelocity = desiredHorizontal;

            bool grounded = _controller.isGrounded;
            if (grounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }

            if (grounded && inputRouter != null && inputRouter.JumpPressedThisFrame)
            {
                if (_isCrouching && CanStandUp())
                {
                    SetCrouchState(false);
                }

                if (!_isCrouching)
                {
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }

            _verticalVelocity += gravity * Time.deltaTime;
            Vector3 motion = (desiredHorizontal + Vector3.up * _verticalVelocity) * Time.deltaTime;
            _controller.Move(motion);

            UpdateVisualPosture();
        }

        private void ApplyGravityOnly()
        {
            if (_controller == null || !_controller.enabled)
            {
                return;
            }

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }

            _controller.Move(Vector3.up * (_verticalVelocity * Time.deltaTime));
        }

        private void SetCrouchState(bool crouch)
        {
            _isCrouching = crouch;
            ApplyControllerDimensions(crouch);
            if (inputRouter != null)
            {
                inputRouter.SetCrouchButtonVisual(crouch);
            }
        }

        private void ApplyControllerDimensions(bool crouch)
        {
            if (_controller == null)
            {
                return;
            }

            float h = crouch ? crouchingHeight : standingHeight;
            _controller.height = h;
            _controller.center = new Vector3(0f, h * 0.5f, 0f);
        }

        private bool CanStandUp()
        {
            if (_controller == null)
            {
                return true;
            }

            float radius = Mathf.Max(0.08f, _controller.radius - 0.04f);
            Vector3 start = transform.position + Vector3.up * (crouchingHeight - radius);
            float castDistance = Mathf.Max(0.05f, standingHeight - crouchingHeight);

            RaycastHit[] hits = Physics.SphereCastAll(
                start,
                radius,
                Vector3.up,
                castDistance,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col != null && col.transform.root != transform.root)
                {
                    return false;
                }
            }

            return true;
        }

        private void UpdateVisualPosture()
        {
            if (visualBodyRoot == null)
            {
                return;
            }

            float targetScaleY = _isCrouching ? visualCrouchScaleY : 1f;
            _currentVisualScaleY = Mathf.Lerp(_currentVisualScaleY, targetScaleY, Time.deltaTime * 14f);
            visualBodyRoot.localScale = new Vector3(1f, _currentVisualScaleY, 1f);
        }

        private void HandleRespawned()
        {
            _verticalVelocity = -2f;
            SetCrouchState(false);
            _currentVisualScaleY = 1f;
            if (visualBodyRoot != null)
            {
                visualBodyRoot.localScale = Vector3.one;
            }

            if (cameraRig != null)
            {
                cameraRig.SnapYawToTarget();
            }
        }
    }
}
