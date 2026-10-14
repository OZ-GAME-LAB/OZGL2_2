using System;
using Units;
using Units.Skills;
using UnityEngine;

// 이후 생성되는 유닛에게 패시브를 추가 부여하기 위한 설정입니다.
[Serializable]
public struct PassiveSkillEffectData
{
    public PassiveSkillData PassiveSkill => _passiveSkill;
    public UnitTeam TargetTeam => _targetTeam;
    public UnitModifierApplyType ApplyType => _applyType;
    public AllyUnitClass AllyClass => _allyClass;
    public AllyUnitType AllyType => _allyType;
    public AllyUnitTier AllyTier => _allyTier;
    public EnemyUnitClass EnemyClass => _enemyClass;
    public EnemyUnitType EnemyType => _enemyType;
    public EnemyUnitFaction EnemyFaction => _enemyFaction;

    [Tooltip("부여할 패시브입니다. 같은 S.O는 중복 부여되지 않으며 아티팩트 중첩에 따라 수치가 증가하지 않습니다.")]
    [SerializeField] private PassiveSkillData _passiveSkill;
    [SerializeField] private UnitTeam _targetTeam;
    [Tooltip("아군: All/Class/Type/Tier, 적: All/Class/Type/Faction. 하나의 적용 조건을 선택합니다.")]
    [SerializeField] private UnitModifierApplyType _applyType;
    [SerializeField] private AllyUnitClass _allyClass;
    [SerializeField] private AllyUnitType _allyType;
    [SerializeField] private AllyUnitTier _allyTier;
    [SerializeField] private EnemyUnitClass _enemyClass;
    [SerializeField] private EnemyUnitType _enemyType;
    [SerializeField] private EnemyUnitFaction _enemyFaction;
}
