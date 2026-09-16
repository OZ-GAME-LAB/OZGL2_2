using System;
using UnityEngine;

[Serializable]
public struct CurrencyEffectData
{
    public CurrencyType CurrencyType => _currencyType;
    public CurrencyRewardType RewardType => _rewardType;
    public CurrencyModifierType ModifierType => _modifierType;
    public float Value => _value;

    [SerializeField] private CurrencyType _currencyType;
    [SerializeField] private CurrencyRewardType _rewardType;
    [SerializeField] private CurrencyModifierType _modifierType;

    [Tooltip("Flat은 고정 수치, Percent는 0.2 = +20%입니다. 음수는 감소 효과입니다.")]
    [SerializeField] private float _value;
}
