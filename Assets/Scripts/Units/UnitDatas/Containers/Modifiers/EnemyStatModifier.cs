


namespace Units
{
    public readonly struct EnemyStatModifier
    {
        // ============================================================
        // Properties
        // ============================================================

        public object Source { get; }

        public UnitModifierApplyType ApplyType { get; }

        public EnemyUnitClass TargetClass { get; }

        public EnemyUnitType TargetUnitType { get; }

        public UnitStatType StatType { get; }

        public UnitStatModifierType ModifierType { get; }

        public float Value { get; }


        // ============================================================
        // Constructor
        // ============================================================

        public EnemyStatModifier(
            object source,
            UnitModifierApplyType applyType,
            EnemyUnitClass targetClass,
            EnemyUnitType targetUnitType,
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