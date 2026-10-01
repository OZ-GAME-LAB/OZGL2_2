using System;
using System.Collections.Generic;

[Serializable]
public class ConsumableItemSaveData
{
    // 효과에 의한 증감을 포함한 최종 허용 슬롯 수입니다. 보유 아이템 수와는 다릅니다.
    public int Capacity;
    // 목록 인덱스가 슬롯 번호이며 빈 문자열은 빈 슬롯입니다. 초과 슬롯도 보존합니다.
    public List<string> SlotItemIds = new();
}
