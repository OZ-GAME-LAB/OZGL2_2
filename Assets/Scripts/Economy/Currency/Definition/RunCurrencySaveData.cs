using System;
using System.Collections.Generic;

[Serializable]
public class RunCurrencySaveData
{
    // 현재 보유한 런 재화(골드·보석)의 종류별 잔액입니다.
    public List<RunCurrencySaveEntry> Balances = new();
    // 웨이브 보상 계산을 이미 마쳤는지 나타냅니다. 복원 후 재계산을 방지합니다.
    public bool HasPreparedReward;
    // 계산된 보상이 속한 분기 번호입니다. 준비된 보상이 없으면 0입니다.
    public int PreparedQuarter;
    // 계산된 보상이 속한 분기 내 웨이브 번호입니다. 준비된 보상이 없으면 0입니다.
    public int PreparedWave;
    // 계산된 웨이브 보상을 이미 지급했는지 나타냅니다. 중복 지급을 방지합니다.
    public bool WaveRewardApplied;
    // 미리 계산해 둔 보상 금액 목록입니다. 지급 완료 여부는 WaveRewardApplied로 구분합니다.
    public List<RunCurrencySaveEntry> PreparedRewards = new();
}

[Serializable]
public class RunCurrencySaveEntry
{
    // 골드·보석 등 런 재화의 종류입니다.
    public CurrencyType Type;
    // Balances에서는 보유 잔액, PreparedRewards에서는 계산된 보상 금액입니다.
    public int Amount;
}
