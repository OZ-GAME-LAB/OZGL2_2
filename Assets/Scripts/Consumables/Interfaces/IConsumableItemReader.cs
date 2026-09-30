using System;
using System.Collections.Generic;

// UI에서 보유 아이템과 슬롯 상태 조회에 사용
public interface IConsumableItemReader
{
    bool IsInitialized { get; }
    IReadOnlyList<IConsumableItemSlotReader> Slots { get; }
    int Capacity { get; }
    int SlotCount { get; }
    int ItemCount { get; }
    bool HasEmptySlot { get; }
    bool HasOverflow { get; }
    int OverflowCount { get; }

    event Action InventoryChanged;

    bool TryGetItem(int slotIndex, out ConsumableItemData item);
}
