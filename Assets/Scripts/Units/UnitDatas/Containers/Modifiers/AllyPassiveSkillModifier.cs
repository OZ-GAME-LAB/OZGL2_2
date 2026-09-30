using Units.Skills;


namespace Units
{
    // 스폰에 추가할 패시브와 등록 출처·적용 대상을 보관한다.
    public readonly struct AllyPassiveSkillModifier
    {
        // ============================================================
        // Properties
        // ============================================================

        public object Source { get; }

        public PassiveSkillData PassiveSkill { get; }

        public UnitModifierApplyType ApplyType { get; }

        public AllyUnitClass TargetClass { get; }

        public AllyUnitType TargetUnitType { get; }

        public AllyUnitTier TargetTier { get; }


        // ============================================================
        // Constructor
        // ============================================================

        public AllyPassiveSkillModifier(
            object source,
            PassiveSkillData passiveSkill,
            UnitModifierApplyType applyType = UnitModifierApplyType.All,
            AllyUnitClass targetClass = default,
            AllyUnitType targetUnitType = default,
            AllyUnitTier targetTier = default)
        {
            Source = source;

            PassiveSkill = passiveSkill;

            ApplyType = applyType;

            TargetClass = targetClass;

            TargetUnitType = targetUnitType;

            TargetTier = targetTier;
        }
    }
}
