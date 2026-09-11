using System;
using System.Collections.Generic;
using Units.Skills;
using UnityEngine;



namespace Units.UnitDatas
{
    [CreateAssetMenu(
        fileName = "UnitData",
        menuName = "Units/Unit Data"
    )]
    public class UnitData : ScriptableObject
    {
        // ============================================================
        // Identity
        // ============================================================

        [SerializeField]
        private string _unitId;

        [SerializeField]
        private string _unitName;


        // ============================================================
        // Team
        // ============================================================

        [SerializeField]
        private UnitTeam _team;


        // ============================================================
        // Unit Identity
        // ============================================================

        [SerializeField]
        private AllyUnitIdentity _allyIdentity;

        [SerializeField]
        private EnemyUnitIdentity _enemyIdentity;


        // ============================================================
        // Stats
        // ============================================================

        [SerializeField]
        private List<UnitStatEntry> _stats = new();


        // ============================================================
        // Combat
        // ============================================================

        [SerializeField]
        private BasicAttackData _basicAttackData;

        [SerializeField]
        private ActiveSkillData _activeSkillData;


        // ============================================================
        // Properties
        // ============================================================

        public string UnitId =>
            _unitId;

        public string UnitName =>
            _unitName;

        public UnitTeam Team =>
            _team;

        public IReadOnlyList<UnitStatEntry> Stats =>
            _stats;

        public BasicAttackData BasicAttackData =>
            _basicAttackData;

        public ActiveSkillData ActiveSkillData =>
            _activeSkillData;


        // ============================================================
        // Public Methods - Identity
        // ============================================================

        public AllyUnitClass GetAllyClass()
        {
            return _allyIdentity.UnitClass;
        }


        public AllyUnitType GetAllyType()
        {
            return _allyIdentity.UnitType;
        }


        public EnemyUnitClass GetEnemyClass()
        {
            return _enemyIdentity.UnitClass;
        }


        public EnemyUnitType GetEnemyType()
        {
            return _enemyIdentity.UnitType;
        }


        // ============================================================
        // Public Methods - Stats
        // ============================================================

        public float GetStat(
            UnitStatType statType)
        {
            foreach (UnitStatEntry stat in _stats)
            {
                if (stat.StatType == statType)
                {
                    return stat.Value;
                }
            }

            return 0f;
        }
    }


    // ============================================================
    // Ally Unit Identity
    // ============================================================

    [Serializable]
    public struct AllyUnitIdentity
    {
        // ============================================================
        // Data
        // ============================================================

        [SerializeField]
        private AllyUnitClass _unitClass;

        [SerializeField]
        private AllyUnitType _unitType;


        // ============================================================
        // Properties
        // ============================================================

        public AllyUnitClass UnitClass =>
            _unitClass;

        public AllyUnitType UnitType =>
            _unitType;
    }


    // ============================================================
    // Enemy Unit Identity
    // ============================================================

    [Serializable]
    public struct EnemyUnitIdentity
    {
        // ============================================================
        // Data
        // ============================================================

        [SerializeField]
        private EnemyUnitClass _unitClass;

        [SerializeField]
        private EnemyUnitType _unitType;


        // ============================================================
        // Properties
        // ============================================================

        public EnemyUnitClass UnitClass =>
            _unitClass;

        public EnemyUnitType UnitType =>
            _unitType;
    }


    // ============================================================
    // Unit Stat Entry
    // ============================================================

    [Serializable]
    public struct UnitStatEntry
    {
        // ============================================================
        // Data
        // ============================================================

        [SerializeField]
        private UnitStatType _statType;

        [SerializeField]
        private float _value;


        // ============================================================
        // Properties
        // ============================================================

        public UnitStatType StatType =>
            _statType;

        public float Value =>
            _value;


        // ============================================================
        // Constructor
        // ============================================================

        public UnitStatEntry(
            UnitStatType statType,
            float value)
        {
            _statType =
                statType;

            _value =
                value;
        }
    }
}