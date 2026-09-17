// Current date KDH 2026-09-16
using System.Collections.Generic;
using UnityEngine;

/// <summary>선택 가능한 제단 목록입니다. ID 중복은 등록하지 않습니다.</summary>
[CreateAssetMenu(fileName = "AltarCatalog", menuName = "OutGame/Altar Catalog")]
public class AltarCatalog : ScriptableObject
{
    public IReadOnlyList<AltarData> Altars => _registeredAltars;

    [SerializeField] private List<AltarData> _altars = new List<AltarData>();

    private readonly Dictionary<string, AltarData> _altarById =
        new Dictionary<string, AltarData>();
    private List<AltarData> _registeredAltars = new List<AltarData>();

    private void OnEnable()
    {
        BuildLookup();
    }

    private void OnValidate()
    {
        BuildLookup();
    }

    public bool TryGetById(string id, out AltarData altar)
    {
        altar = null;
        return !string.IsNullOrWhiteSpace(id) && _altarById.TryGetValue(id, out altar);
    }

    public bool Contains(AltarData altar)
    {
        return altar != null && TryGetById(altar.Id, out AltarData registered)
            && registered == altar;
    }

    // Inspector 변경 시에만 조회 캐시를 다시 만듭니다. 런타임 Update에서는 호출하지 않습니다.
    private void BuildLookup()
    {
        _altarById.Clear();
        _registeredAltars = new List<AltarData>();

        if (_altars == null)
        {
            Debug.LogError("[OutGame/AltarCatalog] 제단 목록이 null입니다.", this);
            return;
        }

        for (int i = 0; i < _altars.Count; i++)
        {
            AltarData altar = _altars[i];
            if (altar == null)
            {
                Debug.LogError($"[OutGame/AltarCatalog] 비어 있는 제단이 있습니다. Index: {i}", this);
                continue;
            }

            if (string.IsNullOrWhiteSpace(altar.Id))
            {
                Debug.LogError($"[OutGame/AltarCatalog] 제단 ID가 비어 있습니다. Asset: {altar.name}", altar);
                continue;
            }

            if (_altarById.ContainsKey(altar.Id))
            {
                Debug.LogError($"[OutGame/AltarCatalog] 중복된 제단 ID입니다. ID: {altar.Id}", altar);
                continue;
            }

            _altarById.Add(altar.Id, altar);
            _registeredAltars.Add(altar);
        }

        if (_registeredAltars.Count == 0)
        {
            Debug.LogWarning("[OutGame/AltarCatalog] 등록된 제단이 없습니다. Inspector 목록을 확인해주세요.", this);
        }
    }
}
