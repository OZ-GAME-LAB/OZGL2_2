using Units.Skills;



namespace Units
{
    public readonly struct ShieldRequest
    {

        public CombatEventMetadata Metadata { get; }

        public CombatSourceSnapshot SourceSnapshot { get; }

        // ============================================================
        // Source
        // ============================================================

        public ICombatTarget Caster { get; }


        // ============================================================
        // Target
        // ============================================================

        public ICombatTarget Target { get; }

        public CombatTargetSnapshot TargetSnapshot { get; }


        // ============================================================
        // Shield
        // ============================================================

        public ShieldScalingStatType ScalingStatType { get; }

        public float ShieldRatio { get; }


        // ============================================================
        // Constructor
        // ============================================================

        public ShieldRequest(
            ICombatTarget caster,
            ICombatTarget target,
            ShieldScalingStatType scalingStatType,
            float shieldRatio,
            CombatEventMetadata metadata = default,
            CombatSourceSnapshot sourceSnapshot = null)
        {
            SourceSnapshot = sourceSnapshot;

            TargetSnapshot = new CombatTargetSnapshot(target);

            Metadata = metadata.EventId != 0 ? metadata : CombatEventMetadata.Create(caster);

            Caster = caster;

            Target = target;

            ScalingStatType = scalingStatType;

            ShieldRatio = shieldRatio;
        }
    }
}