// Current date KDH 2026-09-29
using System;
using System.Collections.Generic;
using Units;

/// <summary>
/// 토템별 기본 적용 대상과 스탯 종류입니다. 수치는 TotemData만 관리합니다.
/// 배열은 한 번만 만들어 두고, 런마다 새로 만들지 않습니다.
/// </summary>
public static class TotemBuiltinEffects
{
    // 격노: 적 전체 공격력 보정입니다.
    private static readonly TotemStatEffect[] EnemyDamage =
    {
        new TotemStatEffect(
            UnitTeam.Enemy, UnitModifierApplyType.All,
            UnitStatType.AttackPower, UnitStatModifierType.Percent)
    };

    private static readonly TotemStatEffect[] EnemyHealth =
    {
        new TotemStatEffect(
            UnitTeam.Enemy, UnitModifierApplyType.All,
            UnitStatType.MaxHp, UnitStatModifierType.Percent)
    };

    private static readonly TotemStatEffect[] EnemyDefense =
    {
        new TotemStatEffect(
            UnitTeam.Enemy, UnitModifierApplyType.All,
            UnitStatType.Defense, UnitStatModifierType.Flat)
    };

    private static readonly TotemStatEffect[] EnemyAttackSpeed =
    {
        new TotemStatEffect(
            UnitTeam.Enemy, UnitModifierApplyType.All,
            UnitStatType.AttackSpeed, UnitStatModifierType.Percent)
    };

    private static readonly TotemStatEffect[] EnemyMoveSpeed =
    {
        new TotemStatEffect(
            UnitTeam.Enemy, UnitModifierApplyType.All,
            UnitStatType.MoveSpeed, UnitStatModifierType.Percent)
    };

    // 취약: 아군 전체가 받는 피해 보정입니다.
    private static readonly TotemStatEffect[] DamageTaken =
    {
        new TotemStatEffect(
            UnitTeam.Ally, UnitModifierApplyType.All,
            UnitStatType.DamageTakenMultiplier, UnitStatModifierType.Percent)
    };

    private static readonly TotemStatEffect[] Empty = Array.Empty<TotemStatEffect>();

    public static IReadOnlyList<TotemStatEffect> Get(TotemId id)
    {
        switch (id)
        {
            case TotemId.EnemyDamage:
                return EnemyDamage;
            case TotemId.EnemyHealth:
                return EnemyHealth;
            case TotemId.EnemyDefense:
                return EnemyDefense;
            case TotemId.EnemyAttackSpeed:
                return EnemyAttackSpeed;
            case TotemId.EnemyMoveSpeed:
                return EnemyMoveSpeed;
            case TotemId.DamageTaken:
                return DamageTaken;
            default:
                return Empty;
        }
    }

}
