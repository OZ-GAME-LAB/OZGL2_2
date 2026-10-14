using UnityEngine;

public interface IConsumableItemUser
{
    bool IsUsing { get; }
    // 특정 아이템의 대상 유효성과 별개인 공통 사용 가능 상태입니다.
    bool CanUse { get; }

    // Area: selectedPosition, All: 추가 입력 없음
    bool TryUse(int slotIndex, Vector2? selectedPosition = null);
}
