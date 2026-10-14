


namespace Units
{
    public readonly struct UnitStatDefinition
    {
        // ============================================================
        // Properties
        // ============================================================

        public float DefaultValue { get; }

        public float MinValue { get; }

        public float MaxValue { get; }

        public UnitStatDisplayType DisplayType { get; }


        // ============================================================
        // Constructor
        // ============================================================

        public UnitStatDefinition(
            float defaultValue,
            float minValue,
            float maxValue,
            UnitStatDisplayType displayType)
        {
            DefaultValue =
                defaultValue;

            MinValue =
                minValue;

            MaxValue =
                maxValue;

            DisplayType =
                displayType;
        }
    }
}