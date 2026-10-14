

namespace Units.Skills
{
    public enum PassiveSkillTriggerType
    {
        Initialize,
        HealthChanged,
        ShieldChanged,
        DamageDealt,
        DamageTaken,
        Tick,
        ActiveSkillStarted = 6,
        ActiveSkillActionStarted = 7,
        ActiveSkillActionHit = 8,
        ActiveSkillActionCompleted = 9,
        ActiveSkillCompleted = 10,
        EnemyKilled = 11,
    }
}