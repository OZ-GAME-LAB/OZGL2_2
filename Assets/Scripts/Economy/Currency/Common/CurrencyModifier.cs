/// <summary>특정 재화와 보상 경로에 적용할 보정치입니다. 합산과 지급은 재화 시스템이 담당합니다.</summary>
public struct CurrencyModifier
{
    // 같은 Source로 등록된 효과를 교체하거나 제거할 때 사용합니다.
    public object Source { get; }
    public CurrencyType CurrencyType { get; }
    public CurrencyRewardType RewardType { get; }
    public CurrencyModifierType ModifierType { get; }

    // 중첩을 반영한 수치입니다. Percent는 0.2 = +20%이며 음수도 허용합니다.
    public float Value { get; }

    public CurrencyModifier(
        object source,
        CurrencyType currencyType,
        CurrencyRewardType rewardType,
        CurrencyModifierType modifierType,
        float value)
    {
        Source = source;
        CurrencyType = currencyType;
        RewardType = rewardType;
        ModifierType = modifierType;
        Value = value;
    }
}
