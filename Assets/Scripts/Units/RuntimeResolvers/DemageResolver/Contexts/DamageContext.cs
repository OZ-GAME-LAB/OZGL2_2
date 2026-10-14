


namespace Units
{
    public readonly struct DamageContext
    {
        // ============================================================
        // Context
        // ============================================================

        public AttackerContext Attacker { get; }

        public TargetContext Target { get; }


        // ============================================================
        // Constructor
        // ============================================================

        public DamageContext(
            AttackerContext attacker,
            TargetContext target)
        {
            Attacker =
                attacker;

            Target =
                target;
        }
    }
}