using UnityEngine;

namespace CageDuel.Player
{
    /// <summary>
    /// Over-the-shoulder third-person camera rig with wall-collision SphereCast,
    /// crouch pivot smoothing, and weapon recoil kick/recovery.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ThirdPersonCameraRig : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private ThirdPersonMotor targetMotor;
        [SerializeField] private PlayerInputRouter inputRouter;

        [Header("Framing")]
        [SerializeField] private float standingPivotHeight = 1.52f;
        [SerializeField] private float crouchingPivotHeight = 1.00f;
        [SerializeField] private float shoulderRightOffset = 0.55f;
        [SerializeField] private float followDistance = 3.35f;
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 65f;

        [Header("Collision")]
        [SerializeField] private float collisionSphereRadius = 0.22f;
        [SerializeField] private float minCameraDistance = 0.65f;
        [SerializeField] private LayerMask collisionLayers = ~0;

        [Header("Recoil Recovery")]
        [SerializeField] private float recoilRecoverySpeed = 10f;

        private Camera _camera;
        private float _yaw;
        private float _pitch = 8f;
        private float _currentPivotHeight;
        private Vector2 _pendingRecoilOffset;

        public Camera AttachedCamera => _camera != null ? _camera : (_camera = GetComponent<Camera>());

        public Vector3 PlanarForward
        {
            get
            {
                Vector3 fwd = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
                fwd.y = 0f;
                return fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;
            }
        }

        public Vector3 PlanarRight => Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;

        public void Configure(Transform target, ThirdPersonMotor motor, PlayerInputRouter router)
        {
            followTarget = target;
            targetMotor = motor;
            inputRouter = router;
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _currentPivotHeight = standingPivotHeight;
            if (followTarget != null)
            {
                _yaw = followTarget.eulerAngles.y;
            }
        }

        public void SnapYawToTarget()
        {
            if (followTarget != null)
            {
                _yaw = followTarget.eulerAngles.y;
                _pitch = 8f;
                _pendingRecoilOffset = Vector2.zero;
            }
        }

        public void AddRecoil(float pitchUpDegrees, float yawSideDegrees)
        {
            _pitch = Mathf.Clamp(_pitch - pitchUpDegrees, minPitch, maxPitch);
            _yaw += yawSideDegrees;
            _pendingRecoilOffset += new Vector2(yawSideDegrees, pitchUpDegrees);
        }

        public Ray GetCenterAimRay()
        {
            Camera cam = AttachedCamera;
            return cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }

        private void LateUpdate()
        {
            if (followTarget == null)
            {
                return;
            }

            if (inputRouter != null)
            {
                Vector2 look = inputRouter.LookDelta;
                _yaw += look.x;
                _pitch = Mathf.Clamp(_pitch - look.y, minPitch, maxPitch);

                // If player manually pulls down against recoil, cancel accumulated recovery
                if (look.y < -0.01f && _pendingRecoilOffset.y > 0f)
                {
                    _pendingRecoilOffset.y = Mathf.Max(0f, _pendingRecoilOffset.y + look.y);
                }
            }

            // Gentle recoil recovery back toward pre-burst elevation
            if (_pendingRecoilOffset.sqrMagnitude > 0.0001f)
            {
                Vector2 step = Vector2.Lerp(Vector2.zero, _pendingRecoilOffset, Time.deltaTime * recoilRecoverySpeed);
                _pendingRecoilOffset -= step;
                _pitch = Mathf.Clamp(_pitch + step.y * 0.55f, minPitch, maxPitch);
            }

            bool crouching = targetMotor != null && targetMotor.IsCrouching;
            float desiredHeight = crouching ? crouchingPivotHeight : standingPivotHeight;
            _currentPivotHeight = Mathf.Lerp(_currentPivotHeight, desiredHeight, Time.deltaTime * 14f);

            Quaternion lookRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pivot = followTarget.position + Vector3.up * _currentPivotHeight;
            Vector3 shoulderPivot = pivot + (lookRotation * Vector3.right * shoulderRightOffset);

            // Ensure shoulder offset itself doesn't poke into an adjacent right-hand wall
            Vector3 pivotToShoulder = shoulderPivot - pivot;
            float shoulderDist = pivotToShoulder.magnitude;
            if (shoulderDist > 0.01f && TrySphereCastEnvironment(pivot, pivotToShoulder / shoulderDist, shoulderDist, out float shoulderHitDist))
            {
                shoulderPivot = pivot + (pivotToShoulder / shoulderDist) * Mathf.Max(0.05f, shoulderHitDist - 0.08f);
            }

            Vector3 backDir = -(lookRotation * Vector3.forward);
            float resolvedDistance = followDistance;
            if (TrySphereCastEnvironment(shoulderPivot, backDir, followDistance, out float hitDistance))
            {
                resolvedDistance = Mathf.Clamp(hitDistance - 0.08f, minCameraDistance, followDistance);
            }

            transform.SetPositionAndRotation(shoulderPivot + backDir * resolvedDistance, lookRotation);
        }

        private bool TrySphereCastEnvironment(Vector3 origin, Vector3 direction, float maxDist, out float closestHitDistance)
        {
            closestHitDistance = maxDist;
            RaycastHit[] hits = Physics.SphereCastAll(
                origin,
                collisionSphereRadius,
                direction,
                maxDist,
                collisionLayers,
                QueryTriggerInteraction.Ignore);

            bool found = false;
            Transform ignoredRoot = followTarget != null ? followTarget.root : null;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null) continue;
                if (ignoredRoot != null && col.transform.root == ignoredRoot) continue;

                if (hits[i].distance < closestHitDistance)
                {
                    closestHitDistance = hits[i].distance;
                    found = true;
                }
            }

            return found;
        }
    }
}
