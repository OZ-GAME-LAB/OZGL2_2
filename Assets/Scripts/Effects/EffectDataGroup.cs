using System.Collections.Generic;

// 변환할 효과 목록을 전달하는 묶음. 생략한 목록은 빈 목록으로 처리합니다.
public sealed class EffectDataGroup
{
    public IReadOnlyList<UnitStatEffectData> StatEffects { get; set; }
    public IReadOnlyList<CurrencyEffectData> CurrencyEffects { get; set; }
    public IReadOnlyList<ConsumableSlotEffectData> SlotEffects { get; set; }
    public IReadOnlyList<PassiveSkillEffectData> PassiveEffects { get; set; }
}
