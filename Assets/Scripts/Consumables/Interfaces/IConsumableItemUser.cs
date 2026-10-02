using UnityEngine;

public interface IConsumableItemUser
{
    bool IsUsing { get; }

    // Area: selectedPosition, All: 추가 입력 없음
    bool TryUse(int slotIndex, Vector2? selectedPosition = null);
}
