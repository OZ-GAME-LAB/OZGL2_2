using System;

// 유지 조건이 어떤 상태 변화에 반응해야 하는지 구독 범위를 구분한다.
namespace Units
{
    [Flags]
    public enum CombatStateChange
    {
        None = 0,
        Health = 1,
        Shield = 2,
        Stats = 4,
        Effects = 8,
        Status = 16,
        Lifetime = 32,
        All = 63
    }
}
