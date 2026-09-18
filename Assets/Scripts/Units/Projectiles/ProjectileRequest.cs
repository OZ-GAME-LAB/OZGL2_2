using System;
using UnityEngine;

namespace Units
{
    public enum ProjectileImpactType
    {
        Single,

        Circle,

        Cone
    }

    public readonly struct ProjectileRequest
    {
        // ============================================================
        // Properties
        // ============================================================

        public Unit_Core Attacker { get; }


        public ICombatTarget Target { get; }


        public UnitTeam TargetTeam { get; }


        public Vector2 Origin { get; }


        public float ProjectileSpeed { get; }


        public ProjectileImpactType ImpactType { get; }


        public float AreaRadius { get; }


        public float AreaAngle { get; }


        public int MaxDamageableCount { get; }


        public DamageSourceType DamageSourceType { get; }


        public float DamageMultiplier { get; }

        public Predicate<ICombatTarget> TargetFilter { get; }

        public int AttackerLifetimeVersion { get; }


        // ============================================================
        // Constructor
        // ============================================================

        public ProjectileRequest(
            Unit_Core attacker,
            ICombatTarget target,
            Vector2 origin,
            float projectileSpeed,
            ProjectileImpactType impactType,
            float areaRadius,
            float areaAngle,
            int maxDamageableCount,
            DamageSourceType damageSourceType,
            float damageMultiplier,
            Predicate<ICombatTarget> targetFilter = null)
        {
            Attacker = attacker;

            Target = target;

            TargetTeam =
                target != null
                    ? target.Team
                    : default;

            Origin = origin;

            ProjectileSpeed = projectileSpeed;

            ImpactType = impactType;

            AreaRadius = Mathf.Max(
                0f,
                areaRadius
            );

            AreaAngle = Mathf.Clamp(
                areaAngle,
                0f,
                360f
            );

            MaxDamageableCount = Mathf.Max(
                1,
                maxDamageableCount
            );

            DamageSourceType = damageSourceType;

            DamageMultiplier = damageMultiplier;
            TargetFilter = targetFilter;
            Unit_Gateway gateway = attacker != null ? attacker.GetComponent<Unit_Gateway>() : null;
            AttackerLifetimeVersion = gateway != null ? gateway.LifetimeVersion : 0;
        }
    }
}