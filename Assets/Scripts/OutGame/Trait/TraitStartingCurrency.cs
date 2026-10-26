// Current date KDH 2026-09-28
using System;
using UnityEngine;

/// <summary>
/// Run 시작 시 1회 지급하는 재화입니다. 지급량 = TraitData.ValuePerLevel × 특성 레벨
/// CurrencyRewardType에 "시작 지급"이 없어 제단과 같은 방식으로 직접 지급합니다.
/// </summary>
[Serializable]
public struct TraitStartingCurrency
{
    public CurrencyType CurrencyType => _currencyType;

    [Tooltip("Run 재화(Gold, Gem)만 지급됩니다.")]
    [SerializeField] private CurrencyType _currencyType;
}
