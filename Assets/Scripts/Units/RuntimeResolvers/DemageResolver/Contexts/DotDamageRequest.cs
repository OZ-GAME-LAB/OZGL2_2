namespace Units
{
    public readonly struct DotDamageRequest
    {

        public CombatEventMetadata Metadata { get; }

        // ============================================================
        // Source
        // ============================================================

        public ICombatTarget Attacker { get; }

        public DamageSourceType SourceType { get; }


        // ============================================================
        // Target
        // ============================================================

        public ICombatTarget Target { get; }

        public CombatTargetSnapshot TargetSnapshot { get; }


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
            DamageSourceType sourceType,
            CombatEventMetadata metadata = default)
        {
            TargetSnapshot = new CombatTargetSnapshot(target);

            Metadata = metadata.EventId != 0 ? metadata : CombatEventMetadata.Create(attacker);

            Attacker = attacker;

            Target = target;

            Damage = damage;

            SourceType = sourceType;
        }
    }
}