


namespace Units
{
    // ============================================================
    // Modifier Type
    // ============================================================

    public enum UnitStatModifierType
    {
        Flat,
        Percent
    }


    // ============================================================
    // Unit Stat Modifier
    // ============================================================

    public readonly struct UnitStatModifier
    {
        // ============================================================
        // Properties
        // ============================================================

        public object Source { get; }

        public UnitStatType StatType { get; }

        public UnitStatModifierType ModifierType { get; }

        public float Value { get; }


        // ============================================================
        // Constructor
        // ============================================================

        public UnitStatModifier(
            object source,
            UnitStatType statType,
            UnitStatModifierType modifierType,
            float value)
        {
            Source = source;
            StatType = statType;
            ModifierType = modifierType;
            Value = value;
        }
    }
}