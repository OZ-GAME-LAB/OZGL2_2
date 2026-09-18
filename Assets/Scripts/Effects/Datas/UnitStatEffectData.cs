using System;
using Units;
using UnityEngine;

[Serializable]
public struct UnitStatEffectData
{
    public UnitTeam TargetTeam => _targetTeam;
    public UnitModifierApplyType ApplyType => _applyType;
    public AllyUnitClass AllyClass => _allyClass;
    public AllyUnitType AllyType => _allyType;
    public EnemyUnitClass EnemyClass => _enemyClass;
    public EnemyUnitType EnemyType => _enemyType;
    public UnitStatType StatType => _statType;
    public UnitStatModifierType ModifierType => _modifierType;
    public float Value => _value;

    [SerializeField] private UnitTeam _targetTeam;

    [Tooltip("All: 대상 팀 전체, Class: 해당 클래스, Type: 해당 유닛 타입에 적용합니다.")]
    [SerializeField] private UnitModifierApplyType _applyType;

    [Tooltip("Target Team이 Ally이고 Apply Type이 Class일 때만 사용합니다.")]
    [SerializeField] private AllyUnitClass _allyClass;
    [Tooltip("Target Team이 Ally이고 Apply Type이 Type일 때만 사용합니다.")]
    [SerializeField] private AllyUnitType _allyType;

    [Tooltip("Target Team이 Enemy이고 Apply Type이 Class일 때만 사용합니다.")]
    [SerializeField] private EnemyUnitClass _enemyClass;
    [Tooltip("Target Team이 Enemy이고 Apply Type이 Type일 때만 사용합니다.")]
    [SerializeField] private EnemyUnitType _enemyType;

    [SerializeField] private UnitStatType _statType;
    [SerializeField] private UnitStatModifierType _modifierType;

    [Tooltip("Flat은 고정 수치, Percent는 0.2 = +20%입니다. 음수는 감소 효과입니다.")]
    [SerializeField] private float _value;
}
