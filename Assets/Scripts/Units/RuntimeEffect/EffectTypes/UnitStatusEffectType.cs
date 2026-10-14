using UnityEngine;

namespace Units.Effects
{
    // 직렬화된 기존 값은 유지한다. 상태 분류와 실제 피해/행동 제한은 구분한다.
    public enum UnitStatusEffectType
    {
        [InspectorName("기절 (Stun)")] Stun = 0,
        [InspectorName("침묵 (Silence)")] Silence = 1,
        [InspectorName("속박 (Root)")] Root = 2,
        [InspectorName("화상 (Burn)")] Burn = 3,
        [InspectorName("중독 (Poison)")] Poison = 4,
        [InspectorName("빙결 (Freeze)")] Freeze = 5,
        [InspectorName("출혈 (Bleed)")] Bleed = 6,
        [InspectorName("전자 (Electric)")] Electric = 7
    }
}
