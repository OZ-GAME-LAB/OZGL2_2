using System;
using System.Collections.Generic;
using Units.Effects;
using Units.Skills;
using Units.UnitDatas;
using UnityEngine;


namespace Units
{
    public class Unit_RuntimeStatus : MonoBehaviour
    {
        // ============================================================
        // Data
        // ============================================================

        [SerializeField]
        private UnitData _unitData;

        private AdjustedStatus _adjustedStatus;

        private FinalStatus _finalStatus;

        private EffectStatus _effectStatus;

        private readonly List<CombatStatModifier> _effectModifierBuffer =
            new();


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
        // Effect Properties
        // ============================================================

        public IReadOnlyList<RuntimeEffectInstance> ActiveEffects =>
            _effectStatus != null
                ? _effectStatus.ActiveEffects
                : Array.Empty<RuntimeEffectInstance>();


        // ============================================================
        // Events
        // ============================================================

        public event Action<UnitStatType, float, float> StatChanged;

        public event Action<float, float> MaxHpChanged;

        public event Action<UnitStatusEffectType, bool> StatusChanged;


        // ============================================================
        // Initialize
        // ============================================================

        public void Initialize(
            FinalStatModifier spawnModifier)
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
                    spawnModifier
                );

            _finalStatus =
                new FinalStatus(
                    _adjustedStatus
                );

            _effectStatus =
                new EffectStatus(
                    IsImmuneToStatus
                );


            _effectStatus.StatusChanged += OnStatusChanged;
        }


        // ============================================================
        // Stat Methods
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
            CombatStatModifier modifier)
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
            if (_finalStatus == null ||
                source == null)
            {
                return;
            }

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
        // Effect Methods
        // ============================================================

        public bool HasEffect(
            string effectId)
        {
            if (_effectStatus == null)
                return false;

            return _effectStatus.HasEffect(
                effectId
            );
        }


        public RuntimeEffectInstance GetEffect(
            string effectId)
        {
            if (_effectStatus == null)
                return null;

            return _effectStatus.GetEffect(
                effectId
            );
        }


        public bool HasStatus(
            UnitStatusEffectType statusType)
        {
            if (_effectStatus == null)
                return false;

            return _effectStatus.HasStatus(
                statusType
            );
        }


        public bool AddRuntimeEffect(
            RuntimeEffectInstance instance)
        {
            if (_effectStatus == null ||
                instance == null)
            {
                return false;
            }

            if (!_effectStatus.AddEffect(
                    instance))
            {
                return false;
            }

            ApplyEffectActions(
                instance
            );

            return true;
        }


        public bool RemoveRuntimeEffect(
            RuntimeEffectInstance instance)
        {
            if (_effectStatus == null ||
                instance == null)
            {
                return false;
            }

            if (!_effectStatus.RemoveEffect(
                    instance))
            {
                return false;
            }

            RemoveEffectActions(
                instance
            );

            return true;
        }


        public void RefreshRuntimeEffect(
            RuntimeEffectInstance instance)
        {
            if (_effectStatus == null ||
                _finalStatus == null ||
                instance == null)
            {
                return;
            }

            Dictionary<UnitStatType, float> previousValues =
                _finalStatus.GetAffectedValues(
                    instance
                );

            BuildEffectModifiers(
                instance,
                _effectModifierBuffer
            );

            _finalStatus.ReplaceModifiers(
                instance,
                _effectModifierBuffer
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


        // ============================================================
        // Effect Action
        // ============================================================

        private void ApplyEffectActions(
            RuntimeEffectInstance instance)
        {
            BuildEffectModifiers(
                instance,
                _effectModifierBuffer
            );

            for (int i = 0;
                 i < _effectModifierBuffer.Count;
                 i++)
            {
                AddCombatModifier(
                    _effectModifierBuffer[i]
                );
            }
        }


        private void BuildEffectModifiers(
            RuntimeEffectInstance instance,
            List<CombatStatModifier> modifiers)
        {
            modifiers.Clear();

            IReadOnlyList<EffectActionData> actions =
                instance.Data.Actions;

            float stackMultiplier =
                GetEffectStackMultiplier(
                    instance
                );

            for (int i = 0;
                 i < actions.Count;
                 i++)
            {
                if (actions[i]
                    is not StatEffectActionData statAction)
                {
                    continue;
                }

                float value =
                    statAction.Value *
                    stackMultiplier;

                modifiers.Add(
                    new CombatStatModifier(
                        instance,
                        statAction.StatType,
                        statAction.ModifierType,
                        value
                    )
                );
            }
        }


        private void RemoveEffectActions(
            RuntimeEffectInstance instance)
        {
            RemoveCombatModifiers(
                instance
            );
        }


        private float GetEffectStackMultiplier(
            RuntimeEffectInstance instance)
        {
            if (instance.Data.StackType !=
                EffectStackType.Stack)
            {
                return 1f;
            }

            return instance.StackCount;
        }


        // ============================================================
        // Status Methods
        // ============================================================

        public bool IsImmuneToStatus(
            UnitStatusEffectType statusType)
        {
            if (_unitData == null)
                return false;


            return _unitData.IsImmuneToStatus(
                statusType
            );
        }


        private void OnStatusChanged(
            UnitStatusEffectType statusType,
            bool isActive)
        {
            StatusChanged?.Invoke(
                statusType,
                isActive
            );
        }


        // ============================================================
        // Event Methods
        // ============================================================

        private void NotifyStatChanged(
            UnitStatType statType,
            float previousValue)
        {
            if (_finalStatus == null)
                return;

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