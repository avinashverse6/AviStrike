using System;
using UnityEngine;

namespace CageDuel.Weapons
{
    [Serializable]
    public class WeaponStats
    {
        public string WeaponName = "Vanguard AR";
        public bool Automatic = true;
        public float RoundsPerSecond = 8.5f;
        public float BaseDamage = 22f;
        public float MaxRange = 85f;

        [Header("Ammo & Reload")]
        public int MagazineSize = 30;
        public int StartingReserveAmmo = 120;
        public float ReloadDuration = 1.75f;

        [Header("Spread / Bloom")]
        public float BaseSpreadDegrees = 0.45f;
        public float MaxSpreadDegrees = 3.5f;
        public float SpreadPerShot = 0.48f;
        public float SpreadRecoveryPerSecond = 7.5f;
        public float CrouchSpreadMultiplier = 0.62f;
        public float MovementSpreadBonus = 0.75f;

        [Header("Camera Recoil")]
        public float RecoilPitchMin = 0.62f;
        public float RecoilPitchMax = 1.12f;
        public float RecoilYawMax = 0.45f;

        public static WeaponStats CreateDefaultRifle()
        {
            return new WeaponStats
            {
                WeaponName = "Vanguard AR",
                Automatic = true,
                RoundsPerSecond = 8.5f,
                BaseDamage = 22f,
                MaxRange = 85f,
                MagazineSize = 30,
                StartingReserveAmmo = 120,
                ReloadDuration = 1.75f,
                BaseSpreadDegrees = 0.45f,
                MaxSpreadDegrees = 3.5f,
                SpreadPerShot = 0.48f,
                SpreadRecoveryPerSecond = 7.5f,
                CrouchSpreadMultiplier = 0.62f,
                MovementSpreadBonus = 0.75f,
                RecoilPitchMin = 0.62f,
                RecoilPitchMax = 1.12f,
                RecoilYawMax = 0.45f
            };
        }

        public static WeaponStats CreateDefaultPistol()
        {
            return new WeaponStats
            {
                WeaponName = "Sidearm P9",
                Automatic = false,
                RoundsPerSecond = 5.2f,
                BaseDamage = 28f,
                MaxRange = 65f,
                MagazineSize = 12,
                StartingReserveAmmo = 60,
                ReloadDuration = 1.25f,
                BaseSpreadDegrees = 0.30f,
                MaxSpreadDegrees = 2.4f,
                SpreadPerShot = 0.55f,
                SpreadRecoveryPerSecond = 9.5f,
                CrouchSpreadMultiplier = 0.65f,
                MovementSpreadBonus = 0.45f,
                RecoilPitchMin = 0.80f,
                RecoilPitchMax = 1.25f,
                RecoilYawMax = 0.35f
            };
        }
    }
}
