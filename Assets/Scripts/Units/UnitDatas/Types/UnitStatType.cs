


namespace Units
{
    public enum UnitStatType
    {
        // Life
        MaxHp = 0,
        Defense = 10,
        DamageTakenMultiplier = 20,
        HealingTakenMultiplier = 30,

        // Combat
        AttackPower = 100,
        DamageMultiplier = 110,
        BasicAttackMultiplier = 120,
        SkillDamageMultiplier = 130,
        DefenseIgnore = 140,
        LifeSteal = 150,
        HealingMultiplier = 160,
        AttackSpeed = 170,
        CooldownReduction = 180,

        // Critical
        CriticalChance = 200,
        CriticalDamage = 210,

        // Movement
        MoveSpeed = 300,

        // Detection
        DetectionRange = 400
    }
}