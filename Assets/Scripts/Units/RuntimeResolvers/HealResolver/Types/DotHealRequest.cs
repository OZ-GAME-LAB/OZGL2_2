namespace Units
{
    public readonly struct DotHealRequest
    {

        public CombatEventMetadata Metadata { get; }

        // ============================================================
        // Source
        // ============================================================

        public ICombatTarget Healer { get; }


        // ============================================================
        // Target
        // ============================================================

        public ICombatTarget Target { get; }

        public CombatTargetSnapshot TargetSnapshot { get; }


        // ============================================================
        // Heal
        // ============================================================

        public float HealAmount { get; }


        // ============================================================
        // Constructor
        // ============================================================

        public DotHealRequest(
            ICombatTarget healer,
            ICombatTarget target,
            float healAmount,
            CombatEventMetadata metadata = default)
        {
            TargetSnapshot = new CombatTargetSnapshot(target);

            Metadata = metadata.EventId != 0 ? metadata : CombatEventMetadata.Create(healer);

            Healer = healer;

            Target = target;

            HealAmount = healAmount;
        }
    }
}