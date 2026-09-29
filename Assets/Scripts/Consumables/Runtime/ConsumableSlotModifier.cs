// 출처와 중첩 수를 반영한 슬롯 보정값. 음수는 슬롯 감소
public struct ConsumableSlotModifier
{
    public object Source { get; }
    public int AdditionalSlots { get; }

    public ConsumableSlotModifier(object source, int additionalSlots)
    {
        Source = source;
        AdditionalSlots = additionalSlots;
    }
}
