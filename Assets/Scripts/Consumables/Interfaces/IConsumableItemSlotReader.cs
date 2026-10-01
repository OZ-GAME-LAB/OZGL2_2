// 슬롯 내용 변경은 매니저를 통해 처리
public interface IConsumableItemSlotReader
{
    ConsumableItemData Item { get; }
    bool IsEmpty { get; }
}
