// Current date KDH 2026-09-29
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 토템 ID로 표시용 TotemData와 인게임 스탯 효과를 찾습니다.
/// 스탯을 비워 두면 TotemBuiltinEffects의 기본값이 사용됩니다.
/// </summary>
[CreateAssetMenu(fileName = "TotemEffectCatalog", menuName = "OutGame/Totem Effect Catalog")]
public class TotemEffectCatalog : ScriptableObject
{
    public IReadOnlyList<TotemData> Totems => _registeredTotems;

    [SerializeField] private List<TotemData> _totems = new List<TotemData>();
    [SerializeField] private List<TotemStatEffectOverride> _statOverrides = new List<TotemStatEffectOverride>();

    private readonly Dictionary<TotemId, TotemData> _totemById = new Dictionary<TotemId, TotemData>();
    private readonly List<TotemData> _registeredTotems = new List<TotemData>();
    private readonly Dictionary<TotemId, List<TotemStatEffect>> _overrideById =
        new Dictionary<TotemId, List<TotemStatEffect>>();

    private void OnEnable()
    {
        BuildLookup();
    }

    private void OnValidate()
    {
        BuildLookup();
    }

    public bool TryGetTotem(TotemId id, out TotemData totem)
    {
        totem = null;
        return id != TotemId.None && _totemById.TryGetValue(id, out totem);
    }

    // 덮어쓴 목록이 있으면 그것을, 없으면 코드에 있는 기본 효과를 반환합니다.
    // 반환 목록은 캐시이므로 호출하는 쪽에서 수정하지 않습니다.
    public IReadOnlyList<TotemStatEffect> GetStatEffects(TotemId id)
    {
        if (_overrideById.TryGetValue(id, out List<TotemStatEffect> effects) && effects != null && effects.Count > 0)
        {
            return effects;
        }

        return TotemBuiltinEffects.Get(id);
    }

    // 로드·Inspector 변경 시에만 조회 캐시를 만듭니다. 런타임 반복 호출은 없습니다.
    private void BuildLookup()
    {
        _totemById.Clear();
        _registeredTotems.Clear();
        _overrideById.Clear();

        if (_totems != null)
        {
            for (int i = 0; i < _totems.Count; i++)
            {
                TotemData totem = _totems[i];
                if (totem == null || totem.Id == TotemId.None)
                {
                    Debug.LogError($"[OutGame/TotemEffectCatalog] 비어 있거나 ID가 None인 토템이 있습니다. Index: {i}", this);
                    continue;
                }

                if (_totemById.ContainsKey(totem.Id))
                {
                    Debug.LogError($"[OutGame/TotemEffectCatalog] 중복된 토템 ID입니다. ID: {totem.Id}", totem);
                    continue;
                }

                _totemById.Add(totem.Id, totem);
                _registeredTotems.Add(totem);
            }
        }

        if (_statOverrides == null)
        {
            return;
        }

        for (int i = 0; i < _statOverrides.Count; i++)
        {
            TotemStatEffectOverride entry = _statOverrides[i];
            if (entry == null || entry.Id == TotemId.None)
            {
                continue;
            }

            if (_overrideById.ContainsKey(entry.Id))
            {
                Debug.LogError($"[OutGame/TotemEffectCatalog] 스탯 덮어쓰기가 중복되었습니다. ID: {entry.Id}", this);
                continue;
            }

            _overrideById.Add(entry.Id, entry.Effects);
            ValidateEffects(entry);
        }
    }

    private void ValidateEffects(TotemStatEffectOverride entry)
    {
        if (entry.Effects == null)
        {
            return;
        }

        for (int i = 0; i < entry.Effects.Count; i++)
        {
            TotemStatEffect effect = entry.Effects[i];
            if (!effect.IsValidTarget())
            {
                Debug.LogError(
                    $"[OutGame/TotemEffectCatalog] 대상 팀과 적용 방식 조합이 잘못되었습니다. Tier는 아군, Faction은 적 전용입니다. ID: {entry.Id}, Index: {i}",
                    this);
            }

            if (float.IsNaN(effect.ValuePerLevel) || float.IsInfinity(effect.ValuePerLevel))
            {
                Debug.LogError($"[OutGame/TotemEffectCatalog] 스탯 수치는 유한한 값이어야 합니다. ID: {entry.Id}, Index: {i}", this);
            }
        }
    }
}

/// <summary>한 토템의 기본 스탯을 교체할 때 사용합니다. 비워 두면 기본 효과가 유지됩니다.</summary>
[Serializable]
public class TotemStatEffectOverride
{
    public TotemId Id;
    public List<TotemStatEffect> Effects = new List<TotemStatEffect>();
}
