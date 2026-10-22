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

        decimal baseReward = (decimal)waveReward.Amount * baseCampLevel; // 10.22 부동소수점 버그 수정
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

    private CurrencyAmount CalculateModifiedReward(CurrencyData currency, decimal baseAmount, // 10.22 부동소수점 버그 수정
        CurrencyRewardType rewardType, IReadOnlyList<CurrencyModifier> modifiers)
    {
        if (currency == null || !TryCalculateModifiedAmount(currency.Type, baseAmount, rewardType,
                modifiers, out decimal finalAmount, out _, out _) || finalAmount > int.MaxValue) // 10.22 부동소수점 버그 수정
            return new CurrencyAmount(currency, -1);
        // 기존 웨이브/생산 보상은 여기서 정수화합니다. 런 정산은 토템 적용 후 정수화합니다.
        return new CurrencyAmount(currency, (int)finalAmount);
    }

    public static bool TryCalculateModifiedAmount(CurrencyType currencyType, decimal baseAmount, // 10.22 부동소수점 버그 수정
        CurrencyRewardType rewardType, IReadOnlyList<CurrencyModifier> modifiers,
        out decimal finalAmount, out decimal totalFlat, out decimal totalPercent) // 10.22 부동소수점 버그 수정
    {
        finalAmount = 0;
        totalFlat = 0;
        totalPercent = 0;
        if (baseAmount < 0) return false; // 10.22 부동소수점 버그 수정
        if (modifiers != null)
        {
            foreach (CurrencyModifier modifier in modifiers)
            {
                if (modifier.CurrencyType != currencyType || modifier.RewardType != rewardType)
                    continue;

                if (modifier.ModifierType == CurrencyModifierType.Flat)
                    totalFlat += modifier.Value;
                else if (modifier.ModifierType == CurrencyModifierType.Percent)
                    totalPercent += modifier.Value;
            }
        }

        // 감소 효과는 0까지만 적용하여 음수끼리 곱해 보상이 늘어나는 것을 방지합니다.
        decimal adjustedBase = System.Math.Max(0m, baseAmount + totalFlat); // 10.22 부동소수점 버그 수정
        decimal multiplier = System.Math.Max(0m, 1m + totalPercent); // 10.22 부동소수점 버그 수정
        finalAmount = adjustedBase * multiplier;
        return true; // 10.22 부동소수점 버그 수정
    }
}
