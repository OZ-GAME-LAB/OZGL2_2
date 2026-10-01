using System.Collections.Generic;
using Units;
using Units.UnitDatas;
using UnityEngine;

/// <summary>
/// 영구 특성의 고정 표시 정보입니다. 해금 여부와 현재 레벨은 영구 저장 상태에서 관리합니다.
/// Current date KDH 2026-09-29: 인게임 효과 데이터와 레벨별 비용·선행 요구 레벨을 통합했습니다.
/// 효과 수치는 모두 "1레벨당" 값이며, 인게임 적용 시 현재 레벨을 곱합니다.
/// </summary>
[CreateAssetMenu(fileName = "TraitData", menuName = "OutGame/Trait Data")]
public class TraitData : ScriptableObject
{
    public TraitId Id => _id;
    public string DisplayName => _displayName;
    public string Description => _description;
    public Sprite Icon => _icon;
    public string EffectLabel => _effectLabel;
    public float ValuePerLevel => _valuePerLevel;
    public string ValueUnit => _valueUnit;
    public int MaxLevel => _maxLevel;
    public TraitData Prerequisite => _prerequisite;
    // Current date KDH 2026-09-29
    public int PrerequisiteLevel => Mathf.Max(1, _prerequisiteLevel);
    public IReadOnlyList<TraitStatEffect> StatEffects => _statEffects;
    public IReadOnlyList<CurrencyEffectData> CurrencyEffects => _currencyEffects;
    public IReadOnlyList<TraitStartingCurrency> StartingCurrencies => _startingCurrencies;

    [SerializeField] private TraitId _id;
    [SerializeField] private string _displayName;
    [SerializeField, TextArea] private string _description;
    [SerializeField] private Sprite _icon;

    [Header("UI 표시")]
    [SerializeField] private string _effectLabel;
    [Tooltip("UI 표시 단위입니다. 5와 %를 조합하면 5%이며, 누적 표시는 이 값에 현재 레벨을 곱합니다. 실제 적용 수치는 아래 효과 목록입니다.")]
    [SerializeField, Min(0f)] private float _valuePerLevel;
    [Tooltip("수치 뒤에 표시할 단위입니다. 예: %, 골드. 단위가 없으면 비워둡니다.")]
    [SerializeField] private string _valueUnit;

    [Header("레벨·비용")]
    [SerializeField, Min(1)] private int _maxLevel = 5;

    [Tooltip("레벨별 비용 목록이 비어 있거나 부족할 때 사용하는 한 레벨당 혈석 비용입니다.")]
    [SerializeField, Min(0)] private int _upgradeCost = 200;

    // Current date KDH 2026-09-29
    [Tooltip("Index 0 = 0→1레벨 비용, Index 1 = 1→2레벨 비용 (혈석). 비워두면 위의 고정 비용을 사용합니다.")]
    [SerializeField] private List<int> _upgradeCosts = new List<int>();

    [Header("선행 조건")]
    [Tooltip("선행 특성입니다. 비워두면 바로 구매할 수 있습니다.")]
    [SerializeField] private TraitData _prerequisite;

    // Current date KDH 2026-09-29
    // 기존 에셋에는 이 필드가 없으므로 기본값 1이 적용되어 이전 규칙(선행 특성 1레벨)을 유지합니다.
    [Tooltip("선행 특성이 이 레벨 이상이어야 구매할 수 있습니다.")]
    [SerializeField, Min(1)] private int _prerequisiteLevel = 1;

    // Current date KDH 2026-09-29
    [Header("인게임 효과 (1레벨당 수치)")]
    // 유닛 스탯 효과입니다. 예: Ally / Tier / Hero / MaxHp / Percent / 0.05 → 영웅 체력 레벨당 +5%
    [SerializeField] private List<TraitStatEffect> _statEffects = new List<TraitStatEffect>();

    // 웨이브 보상·생산·정산 등 기존 재화 보상 경로 보정입니다. EffectManager가 변환합니다.
    [SerializeField] private List<CurrencyEffectData> _currencyEffects = new List<CurrencyEffectData>();

    // 시작 골드처럼 CurrencyRewardType에 없는 타이밍 효과입니다.
    [SerializeField] private List<TraitStartingCurrency> _startingCurrencies = new List<TraitStartingCurrency>();

    // Current date KDH 2026-09-29
    // currentLevel에서 다음 레벨로 올리는 비용입니다. 목록에 해당 레벨이 없으면 고정 비용을 사용합니다.
    public int GetUpgradeCost(int currentLevel)
    {
        if (_upgradeCosts != null && currentLevel >= 0 && currentLevel < _upgradeCosts.Count)
        {
            return Mathf.Max(0, _upgradeCosts[currentLevel]);
        }

        return _upgradeCost;
    }

    private void OnValidate()
    {
        _maxLevel = Mathf.Max(1, _maxLevel);
        _valuePerLevel = Mathf.Max(0f, _valuePerLevel);
        _upgradeCost = Mathf.Max(0, _upgradeCost);

        // Current date KDH 2026-09-29
        _prerequisiteLevel = Mathf.Max(1, _prerequisiteLevel);
        ValidateCosts();
        ValidatePrerequisite();
        ValidateStatEffects();
    }

    // Current date KDH 2026-09-29
    private void ValidateCosts()
    {
        if (_upgradeCosts == null || _upgradeCosts.Count == 0)
        {
            return;
        }

        for (int i = 0; i < _upgradeCosts.Count; i++)
        {
            if (_upgradeCosts[i] < 0)
            {
                _upgradeCosts[i] = 0;
            }
        }

        if (_upgradeCosts.Count != _maxLevel)
        {
            Debug.LogWarning(
                $"[OutGame/TraitData] 레벨별 비용 개수({_upgradeCosts.Count})가 최대 레벨({_maxLevel})과 다릅니다. 부족한 레벨은 고정 비용을 사용합니다. Asset: {name}",
                this);
        }
    }

    // Current date KDH 2026-09-29
    private void ValidatePrerequisite()
    {
        if (_prerequisite == null)
        {
            return;
        }

        // 자기 자신을 선행 조건으로 두면 영원히 해금되지 않습니다.
        if (_prerequisite == this)
        {
            Debug.LogError($"[OutGame/TraitData] 자기 자신을 선행 특성으로 지정했습니다. Asset: {name}", this);
            return;
        }

        if (_prerequisiteLevel > _prerequisite.MaxLevel)
        {
            Debug.LogError(
                $"[OutGame/TraitData] 선행 요구 레벨이 선행 특성의 최대 레벨보다 큽니다. Asset: {name}, 선행: {_prerequisite.name}",
                this);
        }
    }

    // Current date KDH 2026-09-29
    private void ValidateStatEffects()
    {
        if (_statEffects == null)
        {
            return;
        }

        for (int i = 0; i < _statEffects.Count; i++)
        {
            TraitStatEffect effect = _statEffects[i];

            if (!effect.IsValidTarget())
            {
                Debug.LogError(
                    $"[OutGame/TraitData] 대상 팀과 적용 방식 조합이 잘못되었습니다. Tier는 아군, Faction은 적 전용입니다. Asset: {name}, Index: {i}",
                    this);
            }

            if (float.IsNaN(effect.ValuePerLevel) || float.IsInfinity(effect.ValuePerLevel))
            {
                Debug.LogError($"[OutGame/TraitData] 스탯 수치는 유한한 값이어야 합니다. Asset: {name}, Index: {i}", this);
            }

            // (기본값 + Flat) × (1 + Percent) 계산이라, 기본값 0인 비율 스탯(치명타 확률 등)은 Percent만으로는 변하지 않습니다.
            UnitStatDefinition definition = UnitStatDefinitions.Get(effect.StatType);
            if (effect.ModifierType == UnitStatModifierType.Percent
                && definition.DisplayType == UnitStatDisplayType.Percentage
                && Mathf.Approximately(definition.DefaultValue, 0f))
            {
                Debug.LogWarning(
                    $"[OutGame/TraitData] {effect.StatType}는 기본값이 0이라 Percent가 효과 없습니다. Flat을 쓰세요 (예: +5% → Flat 0.05). Asset: {name}, Index: {i}",
                    this);
            }
        }
    }
}
