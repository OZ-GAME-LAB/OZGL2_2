namespace Units.Skills
{
    public readonly struct SkillEffectRequest
    {
        // ============================================================
        // Source
        // ============================================================

        public Unit_Core Caster { get; }


        // ============================================================
        // Target
        // ============================================================

        public ICombatTarget Target { get; }


        // ============================================================
        // Skill
        // ============================================================

        public ActiveSkillData SkillData { get; }


        // ============================================================
        // Constructor
        // ============================================================

        public SkillEffectRequest(
            Unit_Core caster,
            ICombatTarget target,
            ActiveSkillData skillData)
        {
            Caster =
                caster;

            Target =
                target;

            SkillData =
                skillData;
        }
    }
}