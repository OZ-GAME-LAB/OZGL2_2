


namespace Units
{
    public readonly struct AllyStatModifier
    {
        public object Source { get; }

        public UnitModifierApplyType ApplyType { get; }

        public AllyUnitClass TargetClass { get; }

        public AllyUnitType TargetUnitType { get; }

        public UnitStatType StatType { get; }

        public UnitStatModifierType ModifierType { get; }

        public float Value { get; }


        public AllyStatModifier(
            object source,
            UnitModifierApplyType applyType,
            AllyUnitClass targetClass,
            AllyUnitType targetUnitType,
            UnitStatType statType,
            UnitStatModifierType modifierType,
            float value)
        {
            Source = source;
            ApplyType = applyType;
            TargetClass = targetClass;
            TargetUnitType = targetUnitType;
            StatType = statType;
            ModifierType = modifierType;
            Value = value;
        }
    }
}