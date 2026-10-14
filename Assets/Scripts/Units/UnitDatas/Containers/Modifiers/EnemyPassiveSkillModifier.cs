using Units.Skills;


namespace Units
{
    // 스폰에 추가할 패시브와 등록 출처·적용 대상을 보관한다.
    public readonly struct EnemyPassiveSkillModifier
    {
        // ============================================================
        // Properties
        // ============================================================

        public object Source { get; }

        public PassiveSkillData PassiveSkill { get; }

        public UnitModifierApplyType ApplyType { get; }

        public EnemyUnitClass TargetClass { get; }

        public EnemyUnitType TargetUnitType { get; }

        public EnemyUnitFaction TargetFaction { get; }


        // ============================================================
        // Constructor
        // ============================================================

        public EnemyPassiveSkillModifier(
            object source,
            PassiveSkillData passiveSkill,
            UnitModifierApplyType applyType = UnitModifierApplyType.All,
            EnemyUnitClass targetClass = default,
            EnemyUnitType targetUnitType = default,
            EnemyUnitFaction targetFaction = default)
        {
            Source = source;

            PassiveSkill = passiveSkill;

            ApplyType = applyType;

            TargetClass = targetClass;

            TargetUnitType = targetUnitType;

            TargetFaction = targetFaction;
        }
    }
}
