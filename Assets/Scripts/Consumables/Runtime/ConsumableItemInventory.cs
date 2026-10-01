using System;
using System.Collections.Generic;

// 소모성 아이템을 슬롯마다 1개씩 보관
public class ConsumableItemInventory
{
    public const int DefaultSlotCount = 3;

    public IReadOnlyList<ConsumableItemSlot> Slots => _slots;
    public int SlotCount => _slots.Count;
    public int ItemCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (!_slots[i].IsEmpty)
                {
                    count++;
                }
            }
            return count;
        }
    }
    public int OverflowCount => Math.Max(0, ItemCount - Capacity);
    public bool HasOverflow => OverflowCount > 0;

    // 새 아이템을 넣을 수 있는 슬롯 범위. 초과 슬롯은 보관 중인 아이템만 유지
    public int Capacity { get; private set; }
    public bool HasEmptySlot
    {
        get
        {
            if (ItemCount >= Capacity)
            {
                return false;
            }

            for (int i = 0; i < Capacity; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public event Action InventoryChanged;

    private readonly List<ConsumableItemSlot> _slots = new List<ConsumableItemSlot>();

    public ConsumableItemInventory()
    {
        Capacity = DefaultSlotCount;
        for (int i = 0; i < Capacity; i++)
        {
            _slots.Add(new ConsumableItemSlot());
        }
    }

    // 빈 슬롯이거나 범위를 벗어난 경우 false 반환
    public bool TryGetItem(int slotIndex, out ConsumableItemData item)
    {
        item = null;
        if (slotIndex < 0 || slotIndex >= _slots.Count)
        {
            return false;
        }

        item = _slots[slotIndex].Item;
        return item != null;
    }

    // 앞쪽의 빈 슬롯부터 추가. 동일한 아이템도 별도 슬롯에 보관
    public bool TryAdd(ConsumableItemData item)
    {
        if (item == null || !HasEmptySlot)
        {
            return false;
        }

        for (int i = 0; i < Capacity; i++)
        {
            if (_slots[i].TryAdd(item))
            {
                InventoryChanged?.Invoke();
                return true;
            }
        }

        return false;
    }

    // 지정 슬롯 비우기. 초과가 해소되면 허용 범위 밖의 아이템만 빈칸으로 이동
    public bool TryRemove(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count || _slots[slotIndex].IsEmpty)
        {
            return false;
        }

        _slots[slotIndex].Clear();
        TrimEmptyOverflowSlots();
        InventoryChanged?.Invoke();
        return true;
    }

    // 기본 슬롯과 추가 슬롯을 합한 최종 개수 전달
    public bool TrySetCapacity(int capacity)
    {
        if (capacity < 0)
        {
            return false;
        }

        if (Capacity == capacity)
        {
            return true;
        }

        Capacity = capacity;
        while (_slots.Count < Capacity)
        {
            _slots.Add(new ConsumableItemSlot());
        }

        TrimEmptyOverflowSlots();
        InventoryChanged?.Invoke();
        return true;
    }

    // 모든 아이템을 제거하고 현재 허용된 슬롯 개수는 유지
    public void Clear()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            _slots[i].Clear();
        }

        TrimEmptyOverflowSlots();
        InventoryChanged?.Invoke();
    }

    // 뒤쪽의 빈 초과 슬롯만 제거. 중간 빈칸은 뒤에 남은 아이템의 번호 유지를 위해 보관
    private void TrimEmptyOverflowSlots()
    {
        if (!HasOverflow)
        {
            int emptyIndex = 0;
            for (int i = Capacity; i < _slots.Count; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    continue;
                }

                while (emptyIndex < Capacity && !_slots[emptyIndex].IsEmpty)
                {
                    emptyIndex++;
                }

                _slots[emptyIndex].TryAdd(_slots[i].Item);
                _slots[i].Clear();
            }
        }

        while (_slots.Count > Capacity && _slots[_slots.Count - 1].IsEmpty)
        {
            _slots.RemoveAt(_slots.Count - 1);
        }
    }
}
