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


        public Vector2 Origin { get; }


        public float ProjectileSpeed { get; }


        public ProjectileImpactType ImpactType { get; }


        public float AreaRadius { get; }


        public float AreaAngle { get; }


        public int MaxDamageableCount { get; }


        public DamageSourceType DamageSourceType { get; }


        public float DamageMultiplier { get; }


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
            float damageMultiplier)
        {
            Attacker = attacker;

            Target = target;

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
        }
    }
}
