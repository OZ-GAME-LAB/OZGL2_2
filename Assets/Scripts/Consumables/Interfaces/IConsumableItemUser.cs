using Units;
using UnityEngine;

public interface IConsumableItemUser
{
    bool IsUsing { get; }

    // Single: selectedTarget, Area: selectedPosition, All: 추가 입력 없음
    bool TryUse(int slotIndex, ICombatTarget selectedTarget = null, Vector2? selectedPosition = null);
}
