// 콘텐츠 ID 번호 대역: 토템 10000~10999, 제단 11000~11999, 특성 12000~19999.
// None(0)은 미선택 상태입니다. 저장된 ID를 유지하기 위해 기존 번호를 변경하거나 재사용하지 않습니다.

/// <summary>토템 식별자입니다. 새 토템은 10000~10999 범위에서 고유 번호를 지정합니다.</summary>
public enum TotemId
{
    None = 0,
    EnemyDamage = 10000,
    EnemyCount = 10001,
    DamageTaken = 10002,
    GoldReduction = 10003,
    EchoAttack = 10004,
    Crossfire = 10005,
    DeathBurst = 10006,
    Pursuer = 10007
}

/// <summary>제단 식별자입니다. 새 제단은 11000~11999 범위에서 고유 번호를 지정합니다.</summary>
public enum AltarId
{
    None = 0,
    Abundance = 11000,
    Conquest = 11001,
    Guardian = 11002,
    Arcane = 11003
}

/// <summary>영구 특성 식별자입니다. 새 특성은 12000~19999 범위에서 고유 번호를 지정합니다.</summary>
public enum TraitId
{
    None = 0,
    StartingGold = 12000,
    Production = 12001,
    WaveReward = 12002,
    KillGold = 12003,
    MaxHealth = 12004,
    AttackPower = 12005,
    Defense = 12006,
    AttackSpeed = 12007,
    BloodstoneReward = 12008
}
