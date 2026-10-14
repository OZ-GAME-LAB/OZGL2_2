using System.Collections.Generic;
using UnityEngine;

// 소모성 아이템 판매 후보와 공통 가격 범위. 등급 없이 같은 확률로 추첨
[CreateAssetMenu(fileName = "ShopConsumableTable", menuName = "Shop/Shop Consumable Table")]
public class ShopConsumableTable : ScriptableObject
{
    public int SlotCount => _slotCount;
    public List<ConsumableItemData> Items => _items;
    public int MinPrice => _minPrice;
    public int MaxPrice => _maxPrice;

    // 0이면 소모성 아이템 판매 슬롯을 생성하지 않음
    [SerializeField, Min(0)] private int _slotCount = 1;
    [SerializeField] private List<ConsumableItemData> _items = new List<ConsumableItemData>();

    // 모든 후보에 공통 적용. 상품 생성 시 양 끝값을 포함하여 한 번 결정
    [SerializeField, Min(0)] private int _minPrice = 30;
    [SerializeField, Min(0)] private int _maxPrice = 50;

    private void OnValidate()
    {
        if (_slotCount < 0 || _minPrice < 0 || _maxPrice < _minPrice)
        {
            Debug.LogError("[Shop/ShopConsumableTable] 슬롯 수와 가격은 0 이상, 최대 가격은 최소 가격 이상으로 설정", this);
            return;
        }

        if (_items == null)
        {
            Debug.LogError("[Shop/ShopConsumableTable] 판매 후보 목록을 확인해주세요.", this);
            return;
        }

        // 동일 후보가 중복 등록되어 등장 확률이 높아지지 않도록 확인
        HashSet<string> registered = new HashSet<string>();
        foreach (ConsumableItemData item in _items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                Debug.LogError("[Shop/ShopConsumableTable] 판매 후보와 아이템 ID를 확인해주세요.", this);
                return;
            }

            if (!registered.Add(item.Id))
            {
                Debug.LogError($"[Shop/ShopConsumableTable] 중복된 판매 후보 ID입니다. ID: {item.Id}", this);
                return;
            }
        }
    }
}
