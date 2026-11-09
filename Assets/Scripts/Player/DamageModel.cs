using System;
using UnityEngine;

namespace CageDuel.Player
{
    [Serializable]
    public struct DamageInfo
    {
        public float RawDamage;
        public Vector3 HitPoint;
        public Vector3 HitNormal;
        public Vector3 ShotDirection;
        public GameObject Attacker;
        public bool ForceHeadshot;
    }

    [Serializable]
    public struct DamageResult
    {
        public bool Applied;
        public float TotalDamageDealt;
        public float ArmorAbsorbed;
        public float HealthLost;
        public bool IsHeadshot;
        public bool KilledTarget;
        public Vector3 HitPoint;
    }

    public interface IDamageable
    {
        bool IsDead { get; }
        DamageResult TakeDamage(DamageInfo info);
    }

    /// <summary>
    /// Lightweight event bus for combat feedback (floating damage numbers, future audio/pooling hooks).
    /// </summary>
    public static class CombatEvents
    {
        public static event Action<GameObject, DamageInfo, DamageResult> OnAnyDamageApplied;

        public static void NotifyDamageApplied(GameObject target, DamageInfo info, DamageResult result)
        {
            OnAnyDamageApplied?.Invoke(target, info, result);
        }
    }
}
