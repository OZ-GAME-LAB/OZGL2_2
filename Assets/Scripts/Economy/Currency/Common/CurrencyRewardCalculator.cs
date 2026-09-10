// Run/Persistent 매니저에서 사용하는 보상 계산기
// 현재는 계산식 없이 입력을 그대로 반환하며, 지급과 잔액 변경은 매니저가 담당
// 제단·토템·아티팩트의 효과 데이터 전달 방식은 해당 시스템 구성 후 연결
using UnityEngine;

public class CurrencyRewardCalculator
{
    public CurrencyAmount CalculateStartingReward(CurrencyAmount startingReward)
    {
        if (startingReward.Currency == null)
        {
            return startingReward;
        }

        switch (startingReward.Currency.Type)
        {
            case CurrencyType.Gold:
                return CalculateStartingGold(startingReward);
            case CurrencyType.Gem:
                return CalculateStartingGem(startingReward);
            default:
                return startingReward;
        }
    }

    private CurrencyAmount CalculateStartingGold(CurrencyAmount startingReward)
    {
        // 시작 골드에 적용되는 제단·토템 등의 효과 계산
        return startingReward;
    }

    private CurrencyAmount CalculateStartingGem(CurrencyAmount startingReward)
    {
        // 시작 보석에 적용되는 제단·토템 등의 효과 계산
        return startingReward;
    }

    public CurrencyAmount CalculateWaveReward(CurrencyAmount waveReward, int baseCampLevel)
    {
        // 웨이브 자체 보상 = 웨이브 기본 보상 * 베이스 캠프 레벨
        int baseReward = waveReward.Amount * baseCampLevel;

        int adjustedReward = baseReward; // 현재는 임시로 baseReward 그대로 사용, 추후 제단/토템/아티팩트 효과 적용 예정

        return new CurrencyAmount(waveReward.Currency, Mathf.Max(0, adjustedReward));
    }

    public CurrencyAmount CalculateProductionReward(CurrencyAmount productionReward)
    {
        // 건물의 기본 생산량에 해당 재화의 생산량 증가 효과 적용
        // 웨이브 자체 보상 효과와 구분
        return productionReward;
    }

    public CurrencyAmount CalculateEnemyDropReward(CurrencyAmount dropReward)
    {
        // 적의 기본 드랍량에 해당 재화의 적 드랍 보상 효과 적용
        return dropReward;
    }

    public CurrencyAmount CalculateRunSettlementReward(
        CurrencyData currency, int totalWaveCleared, int totalUnitsKilled, int totalBossesKilled)
    {

        // currency.Type에 해당하는 제단·토템·아티팩트의 정산 보상 효과를 적용합니다.
        return new CurrencyAmount(currency, (totalWaveCleared + totalUnitsKilled) * totalBossesKilled);
    }
}
