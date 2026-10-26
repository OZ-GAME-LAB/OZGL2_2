using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 토템의 고정 표시 정보입니다. 현재 선택 레벨은 런 준비 상태에서 별도로 관리합니다.
/// 이 데이터는 전투 효과를 실행하지 않습니다.
/// </summary>
[CreateAssetMenu(fileName = "TotemData", menuName = "OutGame/Totem Data")]
public class TotemData : ScriptableObject
{
    public TotemId Id => _id;
    public TotemEffectCategory Category => _category;
    public bool IsAvailable => _isAvailable;
    public string DisplayName => _displayName;
    public string Description => FormatDescription(_description, 1);
    public string ActiveDescription => _activeDescription;
    public Sprite Icon => _icon;
    public TotemSelectionMode SelectionMode => _selectionMode;
    public string EffectLabel => _effectLabel;
    public float ValuePerLevel => _valuePerLevel;
    public string ValueUnit => _valueUnit;
    public int MaxLevel => _maxLevel;
    public float RewardBonusPerLevel => _rewardBonusPerLevel;
    public IReadOnlyList<CurrencyEffectData> CurrencyEffects => OutGameCurrencyEffect.ResolveAll(_currencyEffects, ValuePerLevel);
    public float GetStatValuePerLevel(TotemStatEffect effect) => OutGameEffectValue.Stat(ValuePerLevel, ValueUnit, effect.ModifierType, effect.IsReduction);

    [SerializeField] private TotemId _id;
    [SerializeField] private TotemEffectCategory _category;
    [SerializeField] private bool _isAvailable = true;
    [SerializeField] private string _displayName;
    [Tooltip("레벨당 설명입니다. {value}와 {unit}을 사용하면 공통 효과 수치가 자동 표시됩니다.")]
    [SerializeField, TextArea] private string _description;

    [Tooltip("현재 활성화된 토템 효과 목록에 표시할 짧은 설명입니다. {level}, {max}, {value}, {unit}을 사용할 수 있습니다.")]
    [SerializeField, TextArea] private string _activeDescription;

    [SerializeField] private Sprite _icon;

    [SerializeField] private TotemSelectionMode _selectionMode;
    [SerializeField] private string _effectLabel;

    [Tooltip("표시와 실제 효과가 함께 사용하는 레벨당 수치입니다. 15와 %는 15%입니다. 감소 효과도 양수로 입력합니다. ON/OFF형에서는 활성화 시 표시할 수치입니다.")]
    [SerializeField, Min(0f)] private float _valuePerLevel;
    [Tooltip("수치 뒤에 표시할 단위입니다. 예: %, 명, 회, 발. 단위가 없으면 비워둡니다.")]
    [SerializeField] private string _valueUnit;
    [Tooltip("레벨형의 선택 상한입니다. ON/OFF형은 항상 1입니다.")]
    [SerializeField, Min(1)] private int _maxLevel = 3;

    [Tooltip("선택 레벨당 표시할 보상 보너스(%)입니다. 10은 10%입니다. ON/OFF형에서는 활성화 시 보너스입니다.")]
    [SerializeField, Min(0f)] private float _rewardBonusPerLevel;

    [Tooltip("재화 대상과 증감 방향입니다. 수치는 위의 Value Per Level을 사용합니다.")]
    [SerializeField] private List<OutGameCurrencyEffect> _currencyEffects = new();

    public string GetActiveDescription(int level)
    {
        string template = string.IsNullOrWhiteSpace(_activeDescription)
            ? (_effectLabel + " {value}{unit}") : _activeDescription;
        return FormatDescription(template, Mathf.Clamp(level, 0, MaxLevel));
    }

    private string FormatDescription(string template, int level)
    {
        return (template ?? string.Empty)
            .Replace("{level}", level.ToString())
            .Replace("{max}", MaxLevel.ToString())
            .Replace("{value}", (ValuePerLevel * level).ToString("0.##"))
            .Replace("{unit}", ValueUnit ?? string.Empty);
    }

    public int GetRewardBonusPercent(int level)
    {
        if (level <= 0)
        {
            return 0;
        }

        float perLevel = RewardBonusPerLevel;
        if (float.IsNaN(perLevel) || float.IsInfinity(perLevel) || perLevel <= 0f)
        {
            return 0;
        }

        long bonus = (long)Math.Round(perLevel * level, MidpointRounding.AwayFromZero);
        if (bonus <= 0)
        {
            return 0;
        }

        return (int)Math.Min(bonus, int.MaxValue);
    }

    private void OnValidate()
    {
        _maxLevel = _selectionMode == TotemSelectionMode.Toggle ? 1 : Mathf.Max(1, _maxLevel);
        _valuePerLevel = Mathf.Max(0f, _valuePerLevel);
        _rewardBonusPerLevel = Mathf.Max(0f, _rewardBonusPerLevel);
    }
}
