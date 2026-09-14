// Run/Persistent 매니저에서 사용하는 보상 계산기
// 기본 보상과 효과 보정치를 계산하며, 지급과 잔액 변경은 매니저가 담당합니다.
using System.Collections.Generic;

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

    public CurrencyAmount CalculateWaveReward(CurrencyAmount waveReward, int baseCampLevel,
        IReadOnlyList<CurrencyModifier> modifiers)
    {
        // 웨이브 자체 보상 = 웨이브 기본 보상 * 베이스 캠프 레벨
        if (waveReward.Amount < 0 || baseCampLevel < 0)
            return new CurrencyAmount(waveReward.Currency, -1);

        double baseReward = (double)waveReward.Amount * baseCampLevel;
        return CalculateModifiedReward(waveReward.Currency, baseReward, CurrencyRewardType.WaveReward, modifiers);
    }

    public CurrencyAmount CalculateProductionReward(CurrencyAmount productionReward,
        IReadOnlyList<CurrencyModifier> modifiers)
    {
        return CalculateModifiedReward(productionReward.Currency, productionReward.Amount,
            CurrencyRewardType.Production, modifiers);
    }

    public CurrencyAmount CalculateEnemyDropReward(CurrencyAmount dropReward)
    {
        // 적의 기본 드랍량에 해당 재화의 적 드랍 보상 효과 적용
        return dropReward;
    }

    public CurrencyAmount CalculateRunSettlementReward(
        CurrencyData currency, int totalWaveCleared, int totalUnitsKilled, int totalBossesKilled,
        IReadOnlyList<CurrencyModifier> modifiers)
    {

        if (totalWaveCleared < 0 || totalUnitsKilled < 0 || totalBossesKilled < 0)
            return new CurrencyAmount(currency, -1);

        // 기본 배수 1, 보스 처치 1회당 배수 1 증가
        double baseReward = ((double)totalWaveCleared + totalUnitsKilled) * (1d + totalBossesKilled);
        return CalculateModifiedReward(currency, baseReward, CurrencyRewardType.RunSettlement, modifiers);
    }

    private CurrencyAmount CalculateModifiedReward(CurrencyData currency, double baseAmount,
        CurrencyRewardType rewardType, IReadOnlyList<CurrencyModifier> modifiers)
    {
        // 잘못된 입력은 기존 Wallet의 음수 거부 처리로 지급을 실패시킵니다.
        if (currency == null || baseAmount < 0)
            return new CurrencyAmount(currency, -1);

        double totalFlat = 0;
        double totalPercent = 0;
        if (modifiers != null)
        {
            foreach (CurrencyModifier modifier in modifiers)
            {
                if (modifier.CurrencyType != currency.Type || modifier.RewardType != rewardType)
                    continue;

                if (float.IsNaN(modifier.Value) || float.IsInfinity(modifier.Value))
                    return new CurrencyAmount(currency, -1);

                if (modifier.ModifierType == CurrencyModifierType.Flat)
                    totalFlat += modifier.Value;
                else if (modifier.ModifierType == CurrencyModifierType.Percent)
                    totalPercent += modifier.Value;
            }
        }

        // 감소 효과는 0까지만 적용하여 음수끼리 곱해 보상이 늘어나는 것을 방지합니다.
        double adjustedBase = System.Math.Max(0, baseAmount + totalFlat);
        double multiplier = System.Math.Max(0, 1 + totalPercent);
        double finalAmount = adjustedBase * multiplier;
        if (double.IsNaN(finalAmount) || double.IsInfinity(finalAmount) || finalAmount > int.MaxValue)
            return new CurrencyAmount(currency, -1);

        // 모든 효과를 합산한 뒤 마지막에만 소수점을 버립니다.
        return new CurrencyAmount(currency, (int)finalAmount);
    }
}
