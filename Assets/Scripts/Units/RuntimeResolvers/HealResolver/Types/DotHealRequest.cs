


namespace Units
{
    public readonly struct DotHealRequest
    {
        public Unit_Core Healer { get; }

        public ICombatTarget Target { get; }

        public float HealAmount { get; }


        public DotHealRequest(
            Unit_Core healer,
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