public enum CurrencyType
{
    None = 0,
    Gold = 1,
    Gem = 2,
    Bloodstone = 3
}

public enum CurrencyLifetime
{
    Run,
    Persistent
}

public enum CurrencyRewardType
{
    // 매 웨이브 준비 페이즈 진입 시 추가 재화 지급
    Preparation = 0,
    // 웨이브 클리어 보상 보정
    WaveReward = 1,
    // 자원 생산 건물의 생산량 보정
    Production = 2,
    // 쿼터 클리어 보상 보정
    QuarterComplete = 3,
    // Run 종료 정산 보상 보정
    RunSettlement = 4
}

public enum CurrencyModifierType
{
    Flat = 0,
    Percent = 1
}
