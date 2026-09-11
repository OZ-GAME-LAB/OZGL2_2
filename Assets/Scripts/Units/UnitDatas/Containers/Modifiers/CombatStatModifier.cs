


namespace Units
{
    public readonly struct CombatStatModifier
    {
        public object Source { get; }
        public UnitStatType StatType { get; }
        public UnitStatModifierType ModifierType { get; }
        public float Value { get; }

        public CombatStatModifier(
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