// 슬롯 하나에 아이템 1개 보관. 같은 아이템도 서로 다른 슬롯에 보관 가능
public class ConsumableItemSlot : IConsumableItemSlotReader
{
    public ConsumableItemData Item { get; private set; }
    public bool IsEmpty => Item == null;

    // 빈 슬롯에만 아이템 추가
    public bool TryAdd(ConsumableItemData item)
    {
        if (item == null || !IsEmpty)
        {
            return false;
        }

        Item = item;
        return true;
    }

    // 아이템 효과를 실행하지 않고 슬롯만 비우기
    public void Clear()
    {
        Item = null;
    }
}
