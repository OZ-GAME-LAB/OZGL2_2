using System;
using System.Collections.Generic;

// 동일 확률로 중복 없이 판매 품목 추첨. 인벤토리와 재화는 변경하지 않음
public class ShopConsumableSelector
{
    private readonly ConsumableItemCatalog _catalog;
    private readonly ShopTable _table;
    private readonly Random _random;

    public ShopConsumableSelector(ConsumableItemCatalog catalog, ShopTable table, Random random = null)
    {
        _catalog = catalog;
        _table = table;
        _random = random ?? new Random();
    }

    // 설정 오류는 false. 후보가 부족하면 가능한 개수만 생성하고 true 반환
    public bool TryCreatePurchaseSlots(out List<ShopConsumableSlot> slots)
    {
        slots = new List<ShopConsumableSlot>();
        if (_table == null)
        {
            return false;
        }

        ShopConsumableTable settings = _table.ShopConsumableTable;
        // 소모성 판매 설정이 없거나 슬롯 수가 0이면 판매하지 않음
        if (settings == null || settings.SlotCount == 0)
        {
            return true;
        }
        if (_catalog == null || settings.SlotCount < 0 || settings.Items == null ||
            settings.MinPrice < 0 || settings.MaxPrice < settings.MinPrice ||
            (_table.PurchaseCurrency != CurrencyType.Gold &&
             _table.PurchaseCurrency != CurrencyType.Gem &&
             _table.PurchaseCurrency != CurrencyType.Bloodstone))
        {
            return false;
        }

        List<ConsumableItemData> candidates = new List<ConsumableItemData>();
        HashSet<string> registered = new HashSet<string>();
        foreach (ConsumableItemData item in settings.Items)
        {
            // 구매 시 획득 가능한 Catalog 항목만 허용. 중복 ID는 설정 오류로 처리
            if (item == null || string.IsNullOrWhiteSpace(item.Id) ||
                !_catalog.Contains(item) || !registered.Add(item.Id))
            {
                return false;
            }
            candidates.Add(item);
        }

        while (slots.Count < settings.SlotCount && candidates.Count > 0)
        {
            int index = _random.Next(candidates.Count);
            ConsumableItemData item = candidates[index];
            candidates.RemoveAt(index);

            // 최대 가격 포함. int.MaxValue에서도 범위 계산이 넘치지 않도록 long 사용
            long priceCount = (long)settings.MaxPrice - settings.MinPrice + 1;
            int price = (int)(settings.MinPrice + (long)(_random.NextDouble() * priceCount));
            slots.Add(new ShopConsumableSlot(item, _table.PurchaseCurrency, price));
        }
        return true;
    }
}
