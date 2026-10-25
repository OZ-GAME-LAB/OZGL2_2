using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>재화 효과의 대상과 방향입니다. 수치는 소유한 특성/토템의 ValuePerLevel을 사용합니다.</summary>
[Serializable]
public struct OutGameCurrencyEffect
{
    [SerializeField] private CurrencyType _currencyType;
    [SerializeField] private CurrencyRewardType _rewardType;
    [SerializeField] private CurrencyModifierType _modifierType;
    [Tooltip("감소 효과이면 선택합니다. 수치는 위의 Value Per Level에 양수로 입력합니다.")]
    [SerializeField] private bool _isReduction;

    public CurrencyEffectData Resolve(float valuePerLevel)
    {
        return new CurrencyEffectData(_currencyType, _rewardType, _modifierType,
            OutGameEffectValue.Resolve(valuePerLevel, _modifierType == CurrencyModifierType.Percent, _isReduction));
    }

    public static IReadOnlyList<CurrencyEffectData> ResolveAll(IReadOnlyList<OutGameCurrencyEffect> effects, float valuePerLevel)
    {
        if (effects == null || effects.Count == 0) return Array.Empty<CurrencyEffectData>();
        var result = new CurrencyEffectData[effects.Count];
        for (int i = 0; i < effects.Count; i++) result[i] = effects[i].Resolve(valuePerLevel);
        return result;
    }
}
