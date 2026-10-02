namespace Units
{
    public readonly struct UnitDecision
    {
        public UnitAIActionType Action { get; }
        public string SkillId { get; }
        public CombatTargetSnapshot Target { get; }
        public CombatTargetSnapshot Owner { get; }
        public int PhaseVersion { get; }
        public float Score { get; }
        public UnitDecision(UnitAIActionType action, ICombatTarget target = null, string skillId = null,
            int phaseVersion = 0, float score = 0, ICombatTarget owner = null)
        { Action = action; Target = new CombatTargetSnapshot(target); Owner = new CombatTargetSnapshot(owner); SkillId = skillId; PhaseVersion = phaseVersion; Score = score; }
    }
}
