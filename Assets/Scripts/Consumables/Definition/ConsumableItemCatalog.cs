using System.Collections.Generic;
using UnityEngine;

// 사용 가능한 아이템 정의 목록과 ID 조회
[CreateAssetMenu(fileName = "ConsumableItemCatalog", menuName = "Consumables/Consumable Item Catalog")]
public class ConsumableItemCatalog : ScriptableObject
{
    public IReadOnlyList<ConsumableItemData> Items => _registeredItems;

    [SerializeField] private List<ConsumableItemData> _items = new List<ConsumableItemData>();

    private readonly Dictionary<string, ConsumableItemData> _itemById =
        new Dictionary<string, ConsumableItemData>();
    private List<ConsumableItemData> _registeredItems = new List<ConsumableItemData>();

    private void OnEnable()
    {
        BuildLookup();
    }

    private void OnValidate()
    {
        BuildLookup();
    }

    public bool TryGetById(string id, out ConsumableItemData item)
    {
        item = null;
        return !string.IsNullOrWhiteSpace(id) && _itemById.TryGetValue(id, out item);
    }

    public bool Contains(ConsumableItemData item)
    {
        return item != null && TryGetById(item.Id, out ConsumableItemData registered) && registered == item;
    }

    // 유효한 항목만 등록하며 중복 ID는 먼저 등록된 항목 사용
    private void BuildLookup()
    {
        _itemById.Clear();
        _registeredItems = new List<ConsumableItemData>();
        if (_items == null)
        {
            Debug.LogError("[Consumables/ConsumableItemCatalog] 아이템 목록이 null입니다.", this);
            return;
        }
        for (int i = 0; i < _items.Count; i++)
        {
            ConsumableItemData item = _items[i];
            if (item == null)
            {
                Debug.LogError($"[Consumables/ConsumableItemCatalog] 비어 있는 아이템이 있습니다. Index: {i}", this);
                continue;
            }
            if (string.IsNullOrWhiteSpace(item.Id) || item.MaxCount < 1)
            {
                Debug.LogError($"[Consumables/ConsumableItemCatalog] ID와 최대 보유 수량(1 이상)을 확인하세요. Asset: {item.name}", item);
                continue;
            }
            if (_itemById.ContainsKey(item.Id))
            {
                Debug.LogError($"[Consumables/ConsumableItemCatalog] 중복된 아이템 ID입니다. ID: {item.Id}", item);
                continue;
            }
            _itemById.Add(item.Id, item);
            _registeredItems.Add(item);
        }
    }
}
