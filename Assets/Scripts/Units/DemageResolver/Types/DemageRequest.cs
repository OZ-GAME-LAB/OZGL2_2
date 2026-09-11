using System.Collections.Generic;



namespace Units
{
    public readonly struct DamageRequest
    {
        // ============================================================
        // Source
        // ============================================================

        public Unit_Core Attacker { get; }

        public DamageSourceType SourceType { get; }

        public float DamageMultiplier { get; }


        // ============================================================
        // Target
        // ============================================================

        public IReadOnlyList<ICombatTarget> Targets { get; }


        // ============================================================
        // Constructor
        // ============================================================

        public DamageRequest(
            Unit_Core attacker,
            IReadOnlyList<ICombatTarget> targets,
            DamageSourceType sourceType,
            float damageMultiplier)
        {
            Attacker =
                attacker;

            Targets =
                targets;

            SourceType =
                sourceType;

            DamageMultiplier =
                damageMultiplier;
        }
    }
}