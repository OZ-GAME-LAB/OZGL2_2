namespace Units
{
    public readonly struct DotDamageRequest
    {
        // ============================================================
        // Source
        // ============================================================

        public Unit_Core Attacker { get; }

        public DamageSourceType SourceType { get; }


        // ============================================================
        // Target
        // ============================================================

        public ICombatTarget Target { get; }


        // ============================================================
        // Damage
        // ============================================================

        public float Damage { get; }


        // ============================================================
        // Constructor
        // ============================================================

        public DotDamageRequest(
            Unit_Core attacker,
            ICombatTarget target,
            float damage,
            DamageSourceType sourceType)
        {
            Attacker =
                attacker;

            Target =
                target;

            Damage =
                damage;

            SourceType =
                sourceType;
        }
    }
}