namespace Units
{
    public readonly struct DotHealRequest
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

        public float HealAmount { get; }


        // ============================================================
        // Constructor
        // ============================================================

        public DotHealRequest(
            ICombatTarget healer,
            ICombatTarget target,
            float healAmount)
        {
            Healer =
                healer;

            Target =
                target;

            HealAmount =
                healAmount;
        }
    }
}