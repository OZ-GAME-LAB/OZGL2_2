using System.Collections.Generic;



namespace Units
{
    public readonly struct TargetContext
    {
        // ============================================================
        // Target
        // ============================================================

        public ICombatTarget Target { get; }


        // ============================================================
        // Defense
        // ============================================================

        public float Defense { get; }

        public float DamageTakenMultiplier { get; }


        // ============================================================
        // Passive
        // ============================================================

        public IReadOnlyList<PassiveDamageModifier> DamageModifiers
        {
            get;
        }


        // ============================================================
        // Constructor
        // ============================================================

        public TargetContext(
            ICombatTarget target,
            float defense,
            float damageTakenMultiplier,
            IReadOnlyList<PassiveDamageModifier> damageModifiers)
        {
            Target =
                target;

            Defense =
                defense;

            DamageTakenMultiplier =
                damageTakenMultiplier;

            DamageModifiers =
                damageModifiers;
        }
    }
}