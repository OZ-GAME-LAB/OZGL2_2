namespace Units
{
    public readonly struct DotDamageRequest
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


        // ============================================================
        // Constructor
        // ============================================================

        public DotDamageRequest(
            ICombatTarget attacker,
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