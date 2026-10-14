using System;
using UnityEngine;

// 아티팩트·제단·토템에서 공통으로 사용하는 소모성 아이템 슬롯 증감 효과
[Serializable]
public struct ConsumableSlotEffectData
{
    public int AdditionalSlots => _additionalSlots;

    [Tooltip("소모성 아이템 슬롯 보정 수입니다. 1이면 1칸 증가, -1이면 1칸 감소, 0이면 변경 없음입니다.")]
    [SerializeField] private int _additionalSlots;
}
