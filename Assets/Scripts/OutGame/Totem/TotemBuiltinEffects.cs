// Current date KDH 2026-09-29
using System;
using System.Collections.Generic;
using Units;

/// <summary>
/// 카탈로그에 따로 적지 않아도 적용되는 토템 스탯입니다.
/// 배열은 한 번만 만들어 두고, 런마다 새로 만들지 않습니다.
/// </summary>
public static class TotemBuiltinEffects
{
    // 격노: 적 공격력 +15%/레벨. 유닛이 가진 공격력에 퍼센트가 곱해집니다.
    private static readonly TotemStatEffect[] EnemyDamage =
    {
        new TotemStatEffect(
            UnitTeam.Enemy, UnitModifierApplyType.All,
            UnitStatType.AttackPower, UnitStatModifierType.Percent, 0.15f)
    };

    // 취약: 아군이 받는 피해 +10%/레벨. 기본 배율이 1이라 0.10은 +10%입니다.
    private static readonly TotemStatEffect[] DamageTaken =
    {
        new TotemStatEffect(
            UnitTeam.Ally, UnitModifierApplyType.All,
            UnitStatType.DamageTakenMultiplier, UnitStatModifierType.Percent, 0.10f)
    };

    private static readonly TotemStatEffect[] Empty = Array.Empty<TotemStatEffect>();

    public static IReadOnlyList<TotemStatEffect> Get(TotemId id)
    {
        switch (id)
        {
            case TotemId.EnemyDamage:
                return EnemyDamage;
            case TotemId.DamageTaken:
                return DamageTaken;
            default:
                return Empty;
        }
    }
}
