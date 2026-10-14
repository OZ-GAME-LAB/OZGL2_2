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

    private static readonly TotemStatEffect[] EnemyHealth =
    {
        new TotemStatEffect(
            UnitTeam.Enemy, UnitModifierApplyType.All,
            UnitStatType.MaxHp, UnitStatModifierType.Percent, 0.15f)
    };

    private static readonly TotemStatEffect[] EnemyDefense =
    {
        new TotemStatEffect(
            UnitTeam.Enemy, UnitModifierApplyType.All,
            UnitStatType.Defense, UnitStatModifierType.Flat, 2f)
    };

    private static readonly TotemStatEffect[] EnemyAttackSpeed =
    {
        new TotemStatEffect(
            UnitTeam.Enemy, UnitModifierApplyType.All,
            UnitStatType.AttackSpeed, UnitStatModifierType.Percent, 0.08f)
    };

    private static readonly TotemStatEffect[] EnemyMoveSpeed =
    {
        new TotemStatEffect(
            UnitTeam.Enemy, UnitModifierApplyType.All,
            UnitStatType.MoveSpeed, UnitStatModifierType.Percent, 0.10f)
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

    public static TotemEffectCategory GetCategory(TotemId id)
    {
        switch (id)
        {
            case TotemId.GoldReduction:
                return TotemEffectCategory.Currency;
            case TotemId.EnemyCount:
                return TotemEffectCategory.Flow;
            default:
                return TotemEffectCategory.Stat;
        }
    }
}
