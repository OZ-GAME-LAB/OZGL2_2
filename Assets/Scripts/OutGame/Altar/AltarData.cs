// Current date KDH 2026-09-16
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 제단의 표시 정보와 효과 목록입니다.
/// 새 제단은 이 에셋을 추가하고 Catalog에 등록하면 됩니다. 기존 코드는 수정하지 않습니다.
/// 
/// </summary>
[CreateAssetMenu(fileName = "AltarData", menuName = "OutGame/Altar Data")]
public class AltarData : ScriptableObject
{
    public string Id => _id;
    public string DisplayName => _displayName;
    public string Description => _description;
    public Sprite Icon => _icon;
    public IReadOnlyList<UnitStatEffectData> UnitStatEffects => _unitStatEffects;
    public IReadOnlyList<CurrencyEffectData> CurrencyEffects => _currencyEffects;
    public IReadOnlyList<AltarTriggeredEffect> TriggeredEffects => _triggeredEffects;

    [SerializeField] private string _id;
    [SerializeField] private string _displayName;
    [TextArea]
    [SerializeField] private string _description;
    [SerializeField] private Sprite _icon;

    // 유물과 같은 공통 효과 데이터입니다. EffectManager가 유닛·재화 보정으로 변환합니다.
    [SerializeField] private List<UnitStatEffectData> _unitStatEffects =
        new List<UnitStatEffectData>();
    [SerializeField] private List<CurrencyEffectData> _currencyEffects =
        new List<CurrencyEffectData>();

    // 시작 골드·이자처럼 기존 보상 enum에 없는 타이밍만 여기에 넣습니다.
    [SerializeField] private List<AltarTriggeredEffect> _triggeredEffects =
        new List<AltarTriggeredEffect>();

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_id))
        {
            Debug.LogError($"[OutGame/AltarData] 제단 ID가 비어 있습니다. Asset: {name}", this);
        }

        if (_triggeredEffects == null)
        {
            return;
        }

        for (int i = 0; i < _triggeredEffects.Count; i++)
        {
            AltarTriggeredEffect effect = _triggeredEffects[i];
            if (effect.Moment == AltarTriggerMoment.None && effect.Calc == AltarTriggerCalc.None)
            {
                continue;
            }

            if (effect.Moment == AltarTriggerMoment.None)
            {
                Debug.LogError($"[OutGame/AltarData] 트리거 시점이 없습니다. Asset: {name}, Index: {i}", this);
            }

            if (effect.Calc == AltarTriggerCalc.None)
            {
                Debug.LogError($"[OutGame/AltarData] 트리거 계산 방식이 없습니다. Asset: {name}, Index: {i}", this);
            }

            if (effect.CurrencyType == CurrencyType.None)
            {
                Debug.LogError($"[OutGame/AltarData] 트리거 재화 종류가 없습니다. Asset: {name}, Index: {i}", this);
            }

            if (float.IsNaN(effect.Value) || float.IsInfinity(effect.Value) || effect.Value <= 0f)
            {
                Debug.LogError(
                    $"[OutGame/AltarData] 트리거 수량이 올바르지 않습니다. Asset: {name}, Index: {i}, Value: {effect.Value}",
                    this);
            }
        }
    }
}
