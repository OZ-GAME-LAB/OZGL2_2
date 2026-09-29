// Current date KDH 2026-09-29
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인게임에서 TraitId로 TraitData를 찾기 위한 특성 목록입니다.
/// 아웃게임 시작 정보(OutGameStartContext.Traits)는 ID와 레벨만 담으므로 이 카탈로그로 SO를 되찾습니다.
/// </summary>
[CreateAssetMenu(fileName = "TraitCatalog", menuName = "OutGame/Trait Catalog")]
public class TraitCatalog : ScriptableObject
{
    public IReadOnlyList<TraitData> Traits => _registeredTraits;

    [SerializeField] private List<TraitData> _traits = new List<TraitData>();

    private readonly Dictionary<TraitId, TraitData> _traitById = new Dictionary<TraitId, TraitData>();
    private readonly List<TraitData> _registeredTraits = new List<TraitData>();

    private void OnEnable()
    {
        BuildLookup();
    }

    private void OnValidate()
    {
        BuildLookup();
    }

    public bool TryGetById(TraitId id, out TraitData trait)
    {
        trait = null;
        return id != TraitId.None && _traitById.TryGetValue(id, out trait);
    }

    public bool Contains(TraitData trait)
    {
        return trait != null && TryGetById(trait.Id, out TraitData registered) && registered == trait;
    }

    // 로드·Inspector 변경 시에만 조회 캐시를 만듭니다. 런타임 반복 호출은 없습니다.
    private void BuildLookup()
    {
        _traitById.Clear();
        _registeredTraits.Clear();

        if (_traits == null)
        {
            Debug.LogError("[OutGame/TraitCatalog] 특성 목록이 null입니다.", this);
            return;
        }

        for (int i = 0; i < _traits.Count; i++)
        {
            TraitData trait = _traits[i];
            if (trait == null || trait.Id == TraitId.None)
            {
                Debug.LogError($"[OutGame/TraitCatalog] 비어 있거나 ID가 None인 특성이 있습니다. Index: {i}", this);
                continue;
            }

            if (_traitById.ContainsKey(trait.Id))
            {
                Debug.LogError($"[OutGame/TraitCatalog] 중복된 특성 ID입니다. ID: {trait.Id}", trait);
                continue;
            }

            _traitById.Add(trait.Id, trait);
            _registeredTraits.Add(trait);
        }
    }
}
