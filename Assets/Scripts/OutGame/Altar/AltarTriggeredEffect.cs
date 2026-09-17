// Current date KDH 2026-09-16
using System;
using UnityEngine;

/// <summary>
/// 기존 재화 보상 경로에 없는 제단 타이밍 효과입니다.
/// 숫자와 재화 종류는 SO에서 바꾸면 되고, 새 타이밍은 AltarTriggerMoment만 늘리면 됩니다.
/// </summary>
[Serializable]
public struct AltarTriggeredEffect
{
    public AltarTriggerMoment Moment => _moment;
    public AltarTriggerCalc Calc => _calc;
    public CurrencyType CurrencyType => _currencyType;
    public float Value => _value;

    [SerializeField] private AltarTriggerMoment _moment;
    [SerializeField] private AltarTriggerCalc _calc;
    [SerializeField] private CurrencyType _currencyType;

    [Tooltip("FlatGrant는 고정 수량, PercentOfBalance는 0.1 = 10%입니다.")]
    [SerializeField] private float _value;
}
