// 상점과 보상에서 아이템을 추가하거나 지정 슬롯을 비울 때 사용
public interface IConsumableItemInventory
{
    bool TryAdd(ConsumableItemData item);

    // 사용 효과를 실행하지 않고 아이템만 제거
    bool TryRemove(int slotIndex);
}
