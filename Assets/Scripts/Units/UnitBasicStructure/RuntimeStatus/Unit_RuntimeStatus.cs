using System;
using System.Collections.Generic;
using Units.Skills;
using Units.UnitDatas;
using UnityEngine;


namespace Units
{
    public partial class Unit_RuntimeStatus : MonoBehaviour
    {
        // ============================================================
        // Data
        // ============================================================

        [SerializeField]
        private UnitData _unitData;

        private AdjustedStatus _adjustedStatus;
        private FinalStatus _finalStatus;


        // ============================================================
        // Data Properties
        // ============================================================

        public UnitData UnitData =>
            _unitData;

        public BasicAttackData BasicAttackData =>
            _unitData != null
                ? _unitData.BasicAttackData
                : null;

        public ActiveSkillData ActiveSkillData =>
            _unitData != null
                ? _unitData.ActiveSkillData
                : null;


        // ============================================================
        // Stat Properties
        // ============================================================

        public float MaxHp =>
            GetStat(UnitStatType.MaxHp);

        public float AttackPower =>
            GetStat(UnitStatType.AttackPower);

        public float BasicAttackMultiplier =>
            GetStat(UnitStatType.BasicAttackMultiplier);

        public float SkillDamageMultiplier =>
            GetStat(UnitStatType.SkillDamageMultiplier);

        public float Defense =>
            GetStat(UnitStatType.Defense);

        public float AttackSpeed =>
            GetStat(UnitStatType.AttackSpeed);

        public float CooldownReduction =>
            GetStat(UnitStatType.CooldownReduction);

        public float CriticalChance =>
            GetStat(UnitStatType.CriticalChance);

        public float CriticalDamage =>
            GetStat(UnitStatType.CriticalDamage);

        public float MoveSpeed =>
            GetStat(UnitStatType.MoveSpeed);

        public float DetectionRange =>
            GetStat(UnitStatType.DetectionRange);


        // ============================================================
        // Events
        // ============================================================

        public event Action<UnitStatType, float, float> StatChanged;

        public event Action<float, float> MaxHpChanged;


        // ============================================================
        // Initialize
        // ============================================================

        public void Initialize(
            IEnumerable<UnitStatModifier> spawnModifiers = null)
        {
            if (_unitData == null)
            {
                Debug.LogError(
                    $"[Unit_RuntimeStatus] {name} : UnitData가 없습니다."
                );

                return;
            }

            _adjustedStatus =
                new AdjustedStatus(
                    _unitData,
                    spawnModifiers
                );

            _finalStatus =
                new FinalStatus(
                    _adjustedStatus
                );
        }


        // ============================================================
        // Public Methods
        // ============================================================

        public float GetStat(
            UnitStatType statType)
        {
            if (_finalStatus == null)
                return 0f;

            return _finalStatus.Get(
                statType
            );
        }


        public void AddCombatModifier(
            UnitStatModifier modifier)
        {
            if (_finalStatus == null)
                return;

            float previousValue =
                _finalStatus.Get(
                    modifier.StatType
                );

            _finalStatus.AddModifier(
                modifier
            );

            NotifyStatChanged(
                modifier.StatType,
                previousValue
            );
        }


        public void RemoveCombatModifiers(
            object source)
        {
            if (_finalStatus == null || source == null)
                return;

            Dictionary<UnitStatType, float> previousValues =
                _finalStatus.GetAffectedValues(
                    source
                );

            if (previousValues.Count == 0)
                return;

            _finalStatus.RemoveModifiers(
                source
            );

            foreach (
                KeyValuePair<UnitStatType, float> pair
                in previousValues)
            {
                NotifyStatChanged(
                    pair.Key,
                    pair.Value
                );
            }
        }


        public void ClearCombatModifiers()
        {
            if (_finalStatus == null)
                return;

            Dictionary<UnitStatType, float> previousValues =
                _finalStatus.GetCurrentValues();

            _finalStatus.ClearModifiers();

            foreach (
                KeyValuePair<UnitStatType, float> pair
                in previousValues)
            {
                NotifyStatChanged(
                    pair.Key,
                    pair.Value
                );
            }
        }


        // ============================================================
        // Private Methods
        // ============================================================

        private void NotifyStatChanged(
            UnitStatType statType,
            float previousValue)
        {
            float currentValue =
                _finalStatus.Get(
                    statType
                );

            if (Mathf.Approximately(
                    previousValue,
                    currentValue))
            {
                return;
            }

            StatChanged?.Invoke(
                statType,
                previousValue,
                currentValue
            );

            if (statType == UnitStatType.MaxHp)
            {
                MaxHpChanged?.Invoke(
                    previousValue,
                    currentValue
                );
            }
        }
    }
}