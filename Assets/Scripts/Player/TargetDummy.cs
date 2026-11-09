using UnityEngine;

namespace CageDuel.Player
{
    /// <summary>
    /// Armored training dummy for Phase 1 combat verification.
    /// Flinches when shot, disables its hitbox while knocked down, and resets on respawn.
    /// </summary>
    [RequireComponent(typeof(HealthArmor))]
    public class TargetDummy : MonoBehaviour
    {
        [SerializeField] private Transform flinchPivot;
        [SerializeField] private float flinchDegrees = 9f;
        [SerializeField] private float flinchRecoverySpeed = 14f;

        private HealthArmor _vitals;
        private Collider _hitCollider;
        private float _currentFlinch;

        public void Configure(Transform bodyPivot)
        {
            flinchPivot = bodyPivot;
        }

        private void Awake()
        {
            _vitals = GetComponent<HealthArmor>();
            _hitCollider = GetComponent<Collider>();
        }

        private void OnEnable()
        {
            if (_vitals != null)
            {
                _vitals.OnDamaged += HandleDamaged;
                _vitals.OnDied += HandleDied;
                _vitals.OnRespawned += HandleRespawned;
            }
        }

        private void OnDisable()
        {
            if (_vitals != null)
            {
                _vitals.OnDamaged -= HandleDamaged;
                _vitals.OnDied -= HandleDied;
                _vitals.OnRespawned -= HandleRespawned;
            }
        }

        private void Update()
        {
            if (_vitals != null && _vitals.IsDead)
            {
                return;
            }

            if (flinchPivot != null && Mathf.Abs(_currentFlinch) > 0.01f)
            {
                _currentFlinch = Mathf.Lerp(_currentFlinch, 0f, Time.deltaTime * flinchRecoverySpeed);
                flinchPivot.localRotation = Quaternion.Euler(-_currentFlinch, 0f, 0f);
            }
        }

        private void HandleDamaged(DamageResult result)
        {
            if (_vitals != null && !_vitals.IsDead)
            {
                _currentFlinch = result.IsHeadshot ? flinchDegrees * 1.5f : flinchDegrees;
            }
        }

        private void HandleDied(DamageInfo fatalInfo)
        {
            _currentFlinch = 0f;
            if (_hitCollider != null)
            {
                _hitCollider.enabled = false;
            }
        }

        private void HandleRespawned()
        {
            _currentFlinch = 0f;
            if (_hitCollider != null)
            {
                _hitCollider.enabled = true;
            }
        }
    }
}
