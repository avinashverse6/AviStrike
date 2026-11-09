using System;
using CageDuel.Match;
using UnityEngine;

namespace CageDuel.Player
{
    /// <summary>
    /// Manages character health, armor absorption, critical headshot detection, death state,
    /// and optional sandbox respawn.
    /// </summary>
    [DisallowMultipleComponent]
    public class HealthArmor : MonoBehaviour, IDamageable
    {
        [Header("Vitals")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float maxArmor = 50f;
        [Range(0f, 1f)]
        [SerializeField] private float armorAbsorptionRatio = 0.65f;

        [Header("Headshot Detection")]
        [SerializeField] private float headshotMultiplier = 1.5f;
        [Range(0.5f, 0.95f)]
        [SerializeField] private float headshotHeightFraction = 0.77f;
        [SerializeField] private float defaultColliderHeight = 1.8f;

        [Header("Visual Collapse & Respawn")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private bool autoRespawnInSandbox = true;
        [SerializeField] private float sandboxRespawnDelay = 3.0f;
        [SerializeField] private SpawnPoint[] respawnPoints;

        private float _currentHealth;
        private float _currentArmor;
        private bool _isDead;
        private float _respawnTimer;
        private int _nextSpawnCursor;
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private CharacterController _characterController;
        private CapsuleCollider _capsuleCollider;

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => maxHealth;
        public float CurrentArmor => _currentArmor;
        public float MaxArmor => maxArmor;
        public bool IsDead => _isDead;
        public float RespawnRemainingSeconds => _isDead ? Mathf.Max(0f, _respawnTimer) : 0f;

        public bool AutoRespawnInSandbox
        {
            get => autoRespawnInSandbox;
            set => autoRespawnInSandbox = value;
        }

        public event Action<float, float, float, float> OnVitalsChanged;
        public event Action<DamageResult> OnDamaged;
        public event Action<DamageInfo> OnDied;
        public event Action OnRespawned;

        public void Configure(
            Transform bodyVisualRoot,
            SpawnPoint[] availableSpawnPoints,
            float health = 100f,
            float armor = 50f,
            bool sandboxAutoRespawn = true,
            float respawnSeconds = 3.0f)
        {
            visualRoot = bodyVisualRoot;
            respawnPoints = availableSpawnPoints;
            maxHealth = Mathf.Max(1f, health);
            maxArmor = Mathf.Max(0f, armor);
            autoRespawnInSandbox = sandboxAutoRespawn;
            sandboxRespawnDelay = Mathf.Max(0.25f, respawnSeconds);
        }

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _capsuleCollider = GetComponent<CapsuleCollider>();
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
            ResetVitals();
        }

        private void Update()
        {
            if (!_isDead)
            {
                return;
            }

            // Smoothly tip visual body onto its back when dead
            if (visualRoot != null)
            {
                Quaternion fallenRotation = Quaternion.Euler(-84f, 0f, 0f);
                visualRoot.localRotation = Quaternion.Slerp(
                    visualRoot.localRotation,
                    fallenRotation,
                    Time.deltaTime * 10f);
            }

            if (autoRespawnInSandbox)
            {
                _respawnTimer -= Time.deltaTime;
                if (_respawnTimer <= 0f)
                {
                    PerformSandboxRespawn();
                }
            }
        }

        public void ResetVitals()
        {
            _currentHealth = maxHealth;
            _currentArmor = maxArmor;
            _isDead = false;
            _respawnTimer = 0f;

            if (visualRoot != null)
            {
                visualRoot.localRotation = Quaternion.identity;
            }

            RaiseVitalsChanged();
        }

        public DamageResult TakeDamage(DamageInfo info)
        {
            if (_isDead || info.RawDamage <= 0f)
            {
                return new DamageResult { Applied = false, HitPoint = info.HitPoint };
            }

            bool isHeadshot = info.ForceHeadshot || IsHitInHeadRegion(info.HitPoint);
            float effectiveDamage = info.RawDamage * (isHeadshot ? headshotMultiplier : 1f);

            float armorAbsorbed = 0f;
            if (_currentArmor > 0f && armorAbsorptionRatio > 0f)
            {
                float desiredArmorShare = effectiveDamage * armorAbsorptionRatio;
                armorAbsorbed = Mathf.Min(_currentArmor, desiredArmorShare);
                _currentArmor -= armorAbsorbed;
            }

            float healthDamage = Mathf.Max(0f, effectiveDamage - armorAbsorbed);
            float previousHealth = _currentHealth;
            _currentHealth = Mathf.Max(0f, _currentHealth - healthDamage);
            float actualHealthLost = previousHealth - _currentHealth;

            bool killed = _currentHealth <= 0.001f;
            if (killed)
            {
                _currentHealth = 0f;
                _isDead = true;
                _respawnTimer = sandboxRespawnDelay;
            }

            var result = new DamageResult
            {
                Applied = true,
                TotalDamageDealt = armorAbsorbed + actualHealthLost,
                ArmorAbsorbed = armorAbsorbed,
                HealthLost = actualHealthLost,
                IsHeadshot = isHeadshot,
                KilledTarget = killed,
                HitPoint = info.HitPoint
            };

            RaiseVitalsChanged();
            OnDamaged?.Invoke(result);
            CombatEvents.NotifyDamageApplied(gameObject, info, result);

            if (killed)
            {
                OnDied?.Invoke(info);
            }

            return result;
        }

        public void RespawnAt(Vector3 worldPosition, Quaternion worldRotation)
        {
            bool hadController = _characterController != null && _characterController.enabled;
            if (hadController)
            {
                _characterController.enabled = false;
            }

            transform.SetPositionAndRotation(worldPosition, worldRotation);

            if (hadController)
            {
                _characterController.enabled = true;
            }

            ResetVitals();
            OnRespawned?.Invoke();
        }

        private void PerformSandboxRespawn()
        {
            Vector3 targetPos = _initialPosition;
            Quaternion targetRot = _initialRotation;

            if (respawnPoints != null && respawnPoints.Length > 0)
            {
                SpawnPoint chosen = respawnPoints[_nextSpawnCursor % respawnPoints.Length];
                _nextSpawnCursor++;
                if (chosen != null)
                {
                    targetPos = chosen.transform.position;
                    targetRot = chosen.transform.rotation;
                }
            }

            RespawnAt(targetPos, targetRot);
        }

        private bool IsHitInHeadRegion(Vector3 worldHitPoint)
        {
            float activeHeight = defaultColliderHeight;
            float bottomY = transform.position.y;

            if (_characterController != null)
            {
                activeHeight = _characterController.height;
                bottomY = transform.position.y + _characterController.center.y - (activeHeight * 0.5f);
            }
            else if (_capsuleCollider != null)
            {
                activeHeight = _capsuleCollider.height * transform.lossyScale.y;
                bottomY = transform.position.y + (_capsuleCollider.center.y * transform.lossyScale.y) - (activeHeight * 0.5f);
            }

            if (activeHeight <= 0.05f)
            {
                return false;
            }

            float normalizedY = (worldHitPoint.y - bottomY) / activeHeight;
            return normalizedY >= headshotHeightFraction;
        }

        private void RaiseVitalsChanged()
        {
            OnVitalsChanged?.Invoke(_currentHealth, maxHealth, _currentArmor, maxArmor);
        }
    }
}
