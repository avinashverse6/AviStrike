using System;
using CageDuel.Player;
using UnityEngine;

namespace CageDuel.Weapons
{
    /// <summary>
    /// Hitscan firearm handling fire rate, dynamic spread, camera + weapon recoil,
    /// magazine/reserve ammo, reload state, muzzle flash particles, bullet tracer, and hit events.
    /// </summary>
    [DisallowMultipleComponent]
    public class HitscanWeapon : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField] private WeaponStats stats = new WeaponStats();

        [Header("References")]
        [SerializeField] private PlayerInputRouter inputRouter;
        [SerializeField] private ThirdPersonCameraRig cameraRig;
        [SerializeField] private ThirdPersonMotor motor;
        [SerializeField] private HealthArmor ownerVitals;
        [SerializeField] private Transform weaponModelPivot;
        [SerializeField] private Transform muzzlePoint;
        [SerializeField] private ParticleSystem muzzleFlashParticles;
        [SerializeField] private ParticleSystem impactSparkParticles;
        [SerializeField] private LineRenderer tracerLine;

        private int _currentInMag;
        private int _reserveAmmo;
        private bool _isReloading;
        private float _reloadElapsed;
        private float _nextFireTime;
        private float _currentBloomDegrees;
        private float _tracerHideTimer;
        private Vector3 _weaponRestLocalPos;
        private Quaternion _weaponRestLocalRot;
        private float _weaponKickBack;

        public WeaponStats Stats => stats;
        public string WeaponName => stats != null ? stats.WeaponName : "Rifle";
        public int CurrentInMag => _currentInMag;
        public int MagazineSize => stats != null ? stats.MagazineSize : 30;
        public int ReserveAmmo => _reserveAmmo;
        public bool IsReloading => _isReloading;
        public float ReloadProgress01 => _isReloading && stats.ReloadDuration > 0.01f
            ? Mathf.Clamp01(_reloadElapsed / stats.ReloadDuration)
            : 0f;

        public float EffectiveSpreadDegrees
        {
            get
            {
                if (stats == null) return 0.5f;
                float moveSpeed01 = motor != null ? Mathf.Clamp01(motor.HorizontalVelocity.magnitude / 5.5f) : 0f;
                float raw = stats.BaseSpreadDegrees + _currentBloomDegrees + (moveSpeed01 * stats.MovementSpreadBonus);
                if (motor != null && motor.IsCrouching)
                {
                    raw *= stats.CrouchSpreadMultiplier;
                }
                return Mathf.Clamp(raw, 0.05f, stats.MaxSpreadDegrees * 1.25f);
            }
        }

        public event Action<int, int, bool> OnAmmoStateChanged;
        public event Action<DamageResult> OnHitConfirmed;
        public event Action OnShotFired;

        public void Configure(
            WeaponStats weaponStats,
            PlayerInputRouter router,
            ThirdPersonCameraRig camRig,
            ThirdPersonMotor playerMotor,
            HealthArmor vitals,
            Transform gunPivot,
            Transform muzzle,
            ParticleSystem muzzleFlash,
            ParticleSystem impactSparks,
            LineRenderer tracer)
        {
            if (weaponStats != null)
            {
                stats = weaponStats;
            }
            inputRouter = router;
            cameraRig = camRig;
            motor = playerMotor;
            ownerVitals = vitals;
            weaponModelPivot = gunPivot;
            muzzlePoint = muzzle;
            muzzleFlashParticles = muzzleFlash;
            impactSparkParticles = impactSparks;
            tracerLine = tracer;
        }

        private void Awake()
        {
            if (stats == null)
            {
                stats = WeaponStats.CreateDefaultRifle();
            }

            if (weaponModelPivot != null)
            {
                _weaponRestLocalPos = weaponModelPivot.localPosition;
                _weaponRestLocalRot = weaponModelPivot.localRotation;
            }

            if (tracerLine != null)
            {
                tracerLine.enabled = false;
            }

            RefillFullAmmo();
        }

        private void OnEnable()
        {
            if (ownerVitals != null)
            {
                ownerVitals.OnDied += HandleOwnerDied;
                ownerVitals.OnRespawned += HandleOwnerRespawned;
            }
        }

        private void OnDisable()
        {
            if (ownerVitals != null)
            {
                ownerVitals.OnDied -= HandleOwnerDied;
                ownerVitals.OnRespawned -= HandleOwnerRespawned;
            }
        }

        public void RefillFullAmmo()
        {
            _isReloading = false;
            _reloadElapsed = 0f;
            _currentInMag = stats.MagazineSize;
            _reserveAmmo = stats.StartingReserveAmmo;
            _currentBloomDegrees = 0f;
            NotifyAmmoChanged();
        }

        private void Update()
        {
            UpdateTracerTimer();
            UpdateBloomRecovery();
            UpdateWeaponVisualMotion();

            if (ownerVitals != null && ownerVitals.IsDead)
            {
                return;
            }

            if (_isReloading)
            {
                _reloadElapsed += Time.deltaTime;
                if (_reloadElapsed >= stats.ReloadDuration)
                {
                    CompleteReload();
                }
                return;
            }

            if (inputRouter != null)
            {
                if (inputRouter.ReloadPressedThisFrame)
                {
                    TryStartReload();
                    return;
                }

                bool wantsFire = stats.Automatic ? inputRouter.FireHeld : inputRouter.FirePressedThisFrame;
                if (wantsFire)
                {
                    if (_currentInMag <= 0)
                    {
                        TryStartReload();
                    }
                    else if (Time.time >= _nextFireTime)
                    {
                        FireFromCameraAim();
                    }
                }
            }
        }

        public bool TryStartReload()
        {
            if (_isReloading) return false;
            if (_currentInMag >= stats.MagazineSize) return false;
            if (_reserveAmmo <= 0) return false;

            _isReloading = true;
            _reloadElapsed = 0f;
            NotifyAmmoChanged();
            return true;
        }

        private void CompleteReload()
        {
            _isReloading = false;
            _reloadElapsed = 0f;

            int needed = stats.MagazineSize - _currentInMag;
            int loaded = Mathf.Min(needed, _reserveAmmo);
            _currentInMag += loaded;
            _reserveAmmo -= loaded;
            NotifyAmmoChanged();
        }

        private void FireFromCameraAim()
        {
            Ray aimRay = cameraRig != null
                ? cameraRig.GetCenterAimRay()
                : new Ray(
                    muzzlePoint != null ? muzzlePoint.position : transform.position + Vector3.up * 1.4f,
                    transform.forward);

            ExecuteShot(aimRay);
        }

        /// <summary>
        /// Fires a single hitscan round along the provided aim ray (reusable for AI bots in Phase 2).
        /// </summary>
        public void ExecuteShot(Ray baseAimRay)
        {
            if (_isReloading || _currentInMag <= 0)
            {
                return;
            }

            _currentInMag--;
            float fireInterval = 1f / Mathf.Max(0.25f, stats.RoundsPerSecond);
            _nextFireTime = Time.time + fireInterval;

            // Apply spread cone around baseAimRay
            float spreadAngle = EffectiveSpreadDegrees;
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * spreadAngle;
            Quaternion spreadRot = Quaternion.Euler(randomCircle.y, randomCircle.x, 0f);
            Vector3 shotDir = (Quaternion.LookRotation(baseAimRay.direction) * spreadRot) * Vector3.forward;

            // Stage 1: Find target point along camera/aim ray
            Vector3 aimEndPoint = baseAimRay.origin + shotDir * stats.MaxRange;
            bool hasHit = TryRaycastIgnoringSelf(baseAimRay.origin, shotDir, stats.MaxRange, out RaycastHit resolvedHit);
            if (hasHit)
            {
                aimEndPoint = resolvedHit.point;
            }

            // Stage 2: Verify line-of-fire from actual muzzle barrel so cover walls in front of gun block shots
            Vector3 barrelOrigin = muzzlePoint != null ? muzzlePoint.position : baseAimRay.origin;
            Vector3 barrelToTarget = aimEndPoint - barrelOrigin;
            float barrelDistance = barrelToTarget.magnitude;
            if (barrelDistance > 0.05f)
            {
                Vector3 barrelDir = barrelToTarget / barrelDistance;
                if (TryRaycastIgnoringSelf(barrelOrigin, barrelDir, barrelDistance + 0.15f, out RaycastHit barrelHit))
                {
                    hasHit = true;
                    resolvedHit = barrelHit;
                    aimEndPoint = barrelHit.point;
                    shotDir = barrelDir;
                }
            }

            // Muzzle flash & visual tracer
            if (muzzleFlashParticles != null)
            {
                muzzleFlashParticles.Emit(10);
            }

            ShowTracer(barrelOrigin, aimEndPoint);
            _weaponKickBack = 0.075f;

            // Apply spread bloom & camera recoil
            _currentBloomDegrees = Mathf.Min(stats.MaxSpreadDegrees, _currentBloomDegrees + stats.SpreadPerShot);
            if (cameraRig != null)
            {
                float pitchKick = UnityEngine.Random.Range(stats.RecoilPitchMin, stats.RecoilPitchMax);
                float yawKick = UnityEngine.Random.Range(-stats.RecoilYawMax, stats.RecoilYawMax);
                if (motor != null && motor.IsCrouching)
                {
                    pitchKick *= 0.75f;
                    yawKick *= 0.75f;
                }
                cameraRig.AddRecoil(pitchKick, yawKick);
            }

            // Apply damage & impact sparks
            if (hasHit && resolvedHit.collider != null)
            {
                if (impactSparkParticles != null)
                {
                    impactSparkParticles.transform.SetPositionAndRotation(
                        resolvedHit.point + resolvedHit.normal * 0.02f,
                        Quaternion.LookRotation(resolvedHit.normal));
                    impactSparkParticles.Emit(6);
                }

                IDamageable damageable = resolvedHit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    DamageResult dmgResult = damageable.TakeDamage(new DamageInfo
                    {
                        RawDamage = stats.BaseDamage,
                        HitPoint = resolvedHit.point,
                        HitNormal = resolvedHit.normal,
                        ShotDirection = shotDir,
                        Attacker = gameObject,
                        ForceHeadshot = false
                    });

                    if (dmgResult.Applied)
                    {
                        OnHitConfirmed?.Invoke(dmgResult);
                    }
                }
            }

            NotifyAmmoChanged();
            OnShotFired?.Invoke();

            if (_currentInMag <= 0 && _reserveAmmo > 0)
            {
                TryStartReload();
            }
        }

        private bool TryRaycastIgnoringSelf(Vector3 origin, Vector3 direction, float maxDist, out RaycastHit closestHit)
        {
            closestHit = default;
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, maxDist, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            Transform selfRoot = transform.root;
            float bestDist = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null) continue;
                if (col.transform.root == selfRoot) continue;

                if (hits[i].distance < bestDist)
                {
                    bestDist = hits[i].distance;
                    closestHit = hits[i];
                    found = true;
                }
            }

            return found;
        }

        private void ShowTracer(Vector3 startWorld, Vector3 endWorld)
        {
            if (tracerLine == null)
            {
                return;
            }

            tracerLine.positionCount = 2;
            tracerLine.SetPosition(0, startWorld);
            tracerLine.SetPosition(1, endWorld);
            tracerLine.enabled = true;
            _tracerHideTimer = 0.055f;
        }

        private void UpdateTracerTimer()
        {
            if (_tracerHideTimer <= 0f)
            {
                return;
            }

            _tracerHideTimer -= Time.deltaTime;
            if (_tracerHideTimer <= 0f && tracerLine != null)
            {
                tracerLine.enabled = false;
            }
        }

        private void UpdateBloomRecovery()
        {
            if (_currentBloomDegrees > 0f && stats != null)
            {
                _currentBloomDegrees = Mathf.Max(0f, _currentBloomDegrees - stats.SpreadRecoveryPerSecond * Time.deltaTime);
            }
        }

        private void UpdateWeaponVisualMotion()
        {
            if (weaponModelPivot == null)
            {
                return;
            }

            _weaponKickBack = Mathf.Lerp(_weaponKickBack, 0f, Time.deltaTime * 18f);
            weaponModelPivot.localPosition = _weaponRestLocalPos + new Vector3(0f, 0f, -_weaponKickBack);

            Quaternion targetLocalRot = _isReloading
                ? _weaponRestLocalRot * Quaternion.Euler(-34f, 0f, 18f)
                : _weaponRestLocalRot * Quaternion.Euler(-_weaponKickBack * 45f, 0f, 0f);

            weaponModelPivot.localRotation = Quaternion.Slerp(
                weaponModelPivot.localRotation,
                targetLocalRot,
                Time.deltaTime * 14f);
        }

        private void HandleOwnerDied(DamageInfo fatalInfo)
        {
            _isReloading = false;
            _reloadElapsed = 0f;
            if (tracerLine != null)
            {
                tracerLine.enabled = false;
            }
            NotifyAmmoChanged();
        }

        private void HandleOwnerRespawned()
        {
            RefillFullAmmo();
        }

        private void NotifyAmmoChanged()
        {
            OnAmmoStateChanged?.Invoke(_currentInMag, _reserveAmmo, _isReloading);
        }
    }
}
