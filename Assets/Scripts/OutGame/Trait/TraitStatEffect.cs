// Current date KDH 2026-09-28
using System;
using Units;
using UnityEngine;

/// <summary>
/// 특성 전용 유닛 스탯 효과입니다.
/// 공통 UnitStatEffectData에는 Tier/Faction 대상 필드가 없어 별도로 둡니다.
/// </summary>
[Serializable]
public struct TraitStatEffect
{
    public UnitTeam TargetTeam => _targetTeam;
    public UnitModifierApplyType ApplyType => _applyType;
    public AllyUnitClass AllyClass => _allyClass;
    public AllyUnitType AllyType => _allyType;
    public AllyUnitTier AllyTier => _allyTier;
    public EnemyUnitClass EnemyClass => _enemyClass;
    public EnemyUnitType EnemyType => _enemyType;
    public EnemyUnitFaction EnemyFaction => _enemyFaction;
    public UnitStatType StatType => _statType;
    public UnitStatModifierType ModifierType => _modifierType;
    public float ValuePerLevel => _valuePerLevel;

    [SerializeField] private UnitTeam _targetTeam;

    [Tooltip("All/Class/Type은 공통, Tier는 아군 전용, Faction은 적 전용입니다.")]
    [SerializeField] private UnitModifierApplyType _applyType;

    [Tooltip("Target Team이 Ally이고 Apply Type이 Class일 때만 사용합니다.")]
    [SerializeField] private AllyUnitClass _allyClass;
    [Tooltip("Target Team이 Ally이고 Apply Type이 Type일 때만 사용합니다.")]
    [SerializeField] private AllyUnitType _allyType;
    [Tooltip("Target Team이 Ally이고 Apply Type이 Tier일 때만 사용합니다.")]
    [SerializeField] private AllyUnitTier _allyTier;

    [Tooltip("Target Team이 Enemy이고 Apply Type이 Class일 때만 사용합니다.")]
    [SerializeField] private EnemyUnitClass _enemyClass;
    [Tooltip("Target Team이 Enemy이고 Apply Type이 Type일 때만 사용합니다.")]
    [SerializeField] private EnemyUnitType _enemyType;
    [Tooltip("Target Team이 Enemy이고 Apply Type이 Faction일 때만 사용합니다.")]
    [SerializeField] private EnemyUnitFaction _enemyFaction;

    [SerializeField] private UnitStatType _statType;
    [SerializeField] private UnitStatModifierType _modifierType;

    [Tooltip("1레벨당 값입니다. Flat은 고정 수치, Percent는 0.05 = +5%. 음수는 감소 효과입니다.")]
    [SerializeField] private float _valuePerLevel;

    // StatModifierOrganizer는 잘못된 조합을 경고 없이 버리므로 여기서 먼저 걸러냅니다.
    public bool IsValidTarget()
    {
        if (_targetTeam == UnitTeam.Ally)
        {
            return _applyType != UnitModifierApplyType.Faction;
        }

        if (_targetTeam == UnitTeam.Enemy)
        {
            return _applyType != UnitModifierApplyType.Tier;
        }

        return false;
    }
}
