


namespace Units
{
    public readonly struct StatModifierValue
    {
        // ============================================================
        // Properties
        // ============================================================

        public float Flat { get; }

        public float Percent { get; }


        // ============================================================
        // Constructor
        // ============================================================

        public StatModifierValue(
            float flat,
            float percent)
        {
            Flat = flat;
            Percent = percent;
        }
    }
}