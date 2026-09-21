using Units.Skills;

namespace Units
{
    public readonly struct HealRequest
    {
        // ============================================================
        // Source
        // ============================================================

        public ICombatTarget Healer { get; }


        // ============================================================
        // Target
        // ============================================================

        public ICombatTarget Target { get; }


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
            float healRatio)
        {
            Healer =
                healer;

            Target =
                target;

            ScalingStatType =
                scalingStatType;

            HealRatio =
                healRatio;
        }
    }
}