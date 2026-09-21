namespace Units
{
    public readonly struct DamageResult
    {
        // ============================================================
        // Source
        // ============================================================

        public ICombatTarget Attacker { get; }

        public DamageSourceType SourceType { get; }


        // ============================================================
        // Target
        // ============================================================

        public ICombatTarget Target { get; }


        // ============================================================
        // Damage
        // ============================================================

        public float Damage { get; }

        public bool IsCritical { get; }


        // ============================================================
        // Constructor
        // ============================================================

        public DamageResult(
            ICombatTarget attacker,
            ICombatTarget target,
            float damage,
            DamageSourceType sourceType,
            bool isCritical)
        {
            Attacker =
                attacker;

            Target =
                target;

            Damage =
                damage;

            SourceType =
                sourceType;

            IsCritical =
                isCritical;
        }
    }
}