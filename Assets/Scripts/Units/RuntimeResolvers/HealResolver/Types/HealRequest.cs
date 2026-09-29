using Units.Skills;

namespace Units
{
    public readonly struct HealRequest
    {

        public CombatEventMetadata Metadata { get; }

        public CombatSourceSnapshot SourceSnapshot { get; }

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

        public HealScalingStatType ScalingStatType { get; }

        public float HealRatio { get; }


        // ============================================================
        // Constructor
        // ============================================================

        public HealRequest(
            ICombatTarget healer,
            ICombatTarget target,
            HealScalingStatType scalingStatType,
            float healRatio,
            CombatEventMetadata metadata = default,
            CombatSourceSnapshot sourceSnapshot = null)
        {
            SourceSnapshot = sourceSnapshot;

            TargetSnapshot = new CombatTargetSnapshot(target);

            Metadata = metadata.EventId != 0 ? metadata : CombatEventMetadata.Create(healer);

            Healer = healer;

            Target = target;

            ScalingStatType = scalingStatType;

            HealRatio = healRatio;
        }
    }
}