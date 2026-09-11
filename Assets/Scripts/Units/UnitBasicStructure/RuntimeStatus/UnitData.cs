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

        [SerializeField] private string _unitId;
        [SerializeField] private string _unitName;


        // ============================================================
        // Team
        // ============================================================

        [SerializeField]
        private UnitTeam _team;

        // ============================================================
        // Team
        // ============================================================

        [SerializeField]
        private UnitType _unitType;


        // ============================================================
        // Stats
        // ============================================================

        [SerializeField] private List<UnitStatEntry> _stats = new();


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

        public UnitType UnitType => 
            _unitType;

        public IReadOnlyList<UnitStatEntry> Stats =>
            _stats;

        public BasicAttackData BasicAttackData =>
            _basicAttackData;

        public ActiveSkillData ActiveSkillData =>
            _activeSkillData;


        // ============================================================
        // Public Methods
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