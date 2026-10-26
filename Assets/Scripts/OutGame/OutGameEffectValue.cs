using System;
using Units;

/// <summary>화면의 레벨당 수치를 실제 효과로 변환하는 단일 경로입니다.</summary>
public static class OutGameEffectValue
{
    public static float Resolve(float value, bool percent, bool reduction = false)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            throw new ArgumentOutOfRangeException(nameof(value), "효과 수치는 유한한 0 이상의 값이어야 합니다.");
        float result = percent ? value / 100f : value;
        return reduction ? -result : result;
    }

    public static float Stat(float value, string unit, UnitStatModifierType modifierType, bool reduction)
    {
        // Flat 비율 스탯(예: 치명타 확률 +5%p)도 화면의 5를 0.05로 변환합니다.
        return Resolve(value, modifierType == UnitStatModifierType.Percent || unit?.Trim() == "%", reduction);
    }

    public static int StartingCurrency(float value, int level)
    {
        Resolve(value, false);
        if (level <= 0) return 0;
        // 재화는 정수이므로 총합에서 한 번만 반올림합니다.
        double total = Math.Round((double)value * level, MidpointRounding.AwayFromZero);
        return (int)Math.Min(total, int.MaxValue);
    }
}
