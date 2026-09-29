// 상품 생성 시 확정한 소모성 아이템과 가격
public class ShopConsumableSlot
{
    public ConsumableItemData Item { get; }
    public CurrencyType Currency { get; }
    public int Price { get; }
    public bool IsPurchased { get; private set; }

    public ShopConsumableSlot(ConsumableItemData item, CurrencyType currency, int price)
    {
        Item = item;
        Currency = currency;
        Price = price;
    }

    // 아이템 지급과 재화 차감 성공 후 호출
    public void MarkPurchased()
    {
        IsPurchased = true;
    }
}
