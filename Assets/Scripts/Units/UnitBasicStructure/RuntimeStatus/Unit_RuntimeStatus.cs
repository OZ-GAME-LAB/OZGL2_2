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

        private IReadOnlyList<PassiveSkillData> _spawnPassiveSkills = Array.Empty<PassiveSkillData>();

        private AdjustedStatus _adjustedStatus;

        private FinalStatus _finalStatus;

        private EffectStatus _effectStatus;

        private int _effectTransactionDepth;

        private int _statusGeneration;

        private readonly Dictionary<UnitStatType, float> _deferredStats = new();

        private readonly Dictionary<UnitStatusEffectType, bool> _beforeStatuses = new();

        public event Action EffectsChanged;

        internal void BeginEffectTransaction()
        {
            if (_effectTransactionDepth++ != 0)
                return;

            _deferredStats.Clear();

            _beforeStatuses.Clear();

            foreach (UnitStatusEffectType type in Enum.GetValues(typeof(UnitStatusEffectType)))
                _beforeStatuses[type] = HasStatus(type);
        }

        internal void EndEffectTransaction(bool changed)
        {
            if (--_effectTransactionDepth != 0)
                return;

            int generation = _statusGeneration;

            var stats = new Dictionary<UnitStatType, float>(_deferredStats);

            var statuses = new Dictionary<UnitStatusEffectType, bool>(_beforeStatuses);

            _deferredStats.Clear();

            _beforeStatuses.Clear();

            foreach (var pair in stats)
            {
                if (generation != _statusGeneration)
                    return;

                NotifyStatChanged(pair.Key, pair.Value);
            }

            foreach (var pair in statuses)
            {
                if (generation != _statusGeneration)
                    return;

                if (HasStatus(pair.Key) != pair.Value)
                    StatusChanged?.Invoke(pair.Key, HasStatus(pair.Key));
            }

            if (changed && generation == _statusGeneration)
                EffectsChanged?.Invoke();
        }

        public int GetEffectStackCount(EffectStackQuery query)
        {
            if (query == null || !query.IsValid)
                return 0;

            int count = 0;

            foreach (var instance in ActiveEffects)
                if (query.Matches(instance.Definition))
                    count += instance.StackCount;

            return count;
        }

        private readonly List<CombatStatModifier> _effectModifierBuffer = new();


        // ============================================================
        // Data Properties
        // ============================================================

        public UnitData UnitData => _unitData;

        public BasicAttackData BasicAttackData => _unitData != null ? _unitData.BasicAttackData : null;

        public ActiveSkillData ActiveSkillData => _unitData != null ? _unitData.ActiveSkillData : null;

        public IReadOnlyList<PassiveSkillData> PassiveSkillDatas => _spawnPassiveSkills;


        // ============================================================
        // Stat Properties
        // ============================================================

        // Life

        public float MaxHp => GetStat(UnitStatType.MaxHp);

        public float Defense => GetStat(UnitStatType.Defense);

        public float DamageTakenMultiplier => GetStat(UnitStatType.DamageTakenMultiplier);

        public float HealingTakenMultiplier => GetStat(UnitStatType.HealingTakenMultiplier);


        // Combat

        public float AttackPower => GetStat(UnitStatType.AttackPower);

        public float DamageMultiplier => GetStat(UnitStatType.DamageMultiplier);

        public float BasicAttackMultiplier => GetStat(UnitStatType.BasicAttackMultiplier);

        public float SkillDamageMultiplier => GetStat(UnitStatType.SkillDamageMultiplier);

        public float DefenseIgnore => GetStat(UnitStatType.DefenseIgnore);

        public float LifeSteal => GetStat(UnitStatType.LifeSteal);

        public float HealingMultiplier => GetStat(UnitStatType.HealingMultiplier);

        public float AttackSpeed => GetStat(UnitStatType.AttackSpeed);

        public float CooldownReduction => GetStat(UnitStatType.CooldownReduction);


        // Critical

        public float CriticalChance => GetStat(UnitStatType.CriticalChance);

        public float CriticalDamage => GetStat(UnitStatType.CriticalDamage);


        // Movement

        public float MoveSpeed => GetStat(UnitStatType.MoveSpeed);


        // Detection

        public float DetectionRange => GetStat(UnitStatType.DetectionRange);


        // ============================================================
        // Effect Properties
        // ============================================================

        public IReadOnlyList<RuntimeEffectInstance> ActiveEffects => _effectStatus != null ? _effectStatus.ActiveEffects : Array.Empty<RuntimeEffectInstance>();


        // ============================================================
        // Events
        // ============================================================

        public event Action<UnitStatType, float, float> StatChanged;

        public event Action<float, float> MaxHpChanged;

        public event Action<UnitStatusEffectType, bool> StatusChanged;


        // ============================================================
        // Initialize
        // ============================================================

        public void Initialize(FinalStatModifier spawnModifier)
        {
            Initialize(spawnModifier, null);
        }


        // 외부 패시브는 이 수명의 초기화 시점에만 받는다.
        public void Initialize(
            FinalStatModifier spawnModifier,
            IReadOnlyList<PassiveSkillData> spawnPassiveSkills)
        {
            _spawnPassiveSkills = Array.Empty<PassiveSkillData>();

            if (_unitData == null)
            {
                Debug.LogError($"[Unit_RuntimeStatus] {name} : UnitData가 없습니다.");

                return;
            }

            _statusGeneration++;

            _effectTransactionDepth = 0;

            _deferredStats.Clear();

            _beforeStatuses.Clear();

            InitializePassiveSkills(spawnPassiveSkills);

            _adjustedStatus = new AdjustedStatus(_unitData, spawnModifier);

            _finalStatus = new FinalStatus(_adjustedStatus);

            _effectStatus = new EffectStatus(IsImmuneToStatus);

            _effectStatus.StatusChanged += OnStatusChanged;
        }


        private void InitializePassiveSkills(IReadOnlyList<PassiveSkillData> spawnPassiveSkills)
        {
            List<PassiveSkillData> result = new();

            HashSet<PassiveSkillData> included = new();

            // 기본 목록의 기존 실행 순서와 중복 설정은 보존한다.
            if (_unitData.PassiveSkillDatas != null)
            {
                foreach (PassiveSkillData passiveSkill in _unitData.PassiveSkillDatas)
                {
                    if (passiveSkill == null)
                        continue;

                    result.Add(passiveSkill);

                    included.Add(passiveSkill);
                }
            }

            if (spawnPassiveSkills != null)
            {
                foreach (PassiveSkillData passiveSkill in spawnPassiveSkills)
                {
                    if (passiveSkill != null && included.Add(passiveSkill))
                    {
                        result.Add(passiveSkill);
                    }
                }
            }

            _spawnPassiveSkills = result.AsReadOnly();
        }


        // ============================================================
        // Stat Methods
        // ============================================================

        public float GetStat(UnitStatType statType)
        {
            if (_finalStatus == null)
                return 0f;

            return _finalStatus.Get(statType);
        }

        public void AddCombatModifier(CombatStatModifier modifier)
        {
            if (_finalStatus == null)
                return;

            float previousValue = _finalStatus.Get(modifier.StatType);

            _finalStatus.AddModifier(modifier);

            NotifyStatChanged(modifier.StatType, previousValue);
        }

        public void RemoveCombatModifiers(object source)
        {
            if (_finalStatus == null || source == null)
            {
                return;
            }

            Dictionary<UnitStatType, float> previousValues = _finalStatus.GetAffectedValues(source);

            if (previousValues.Count == 0)
                return;

            _finalStatus.RemoveModifiers(source);

            foreach (KeyValuePair<UnitStatType, float> pair in previousValues)
            {
                NotifyStatChanged(pair.Key, pair.Value);
            }
        }

        public void ClearCombatModifiers()
        {
            if (_finalStatus == null)
                return;

            Dictionary<UnitStatType, float> previousValues = _finalStatus.GetCurrentValues();

            _finalStatus.ClearModifiers();

            foreach (KeyValuePair<UnitStatType, float> pair in previousValues)
            {
                NotifyStatChanged(pair.Key, pair.Value);
            }
        }


        // ============================================================
        // Effect Methods
        // ============================================================

        public bool HasEffect(string effectId)
        {
            if (_effectStatus == null)
                return false;

            return _effectStatus.HasEffect(effectId);
        }

        public RuntimeEffectInstance GetEffect(string effectId)
        {
            if (_effectStatus == null)
                return null;

            return _effectStatus.GetEffect(effectId);
        }

        public bool HasStatus(UnitStatusEffectType statusType)
        {
            if (_effectStatus == null)
                return false;

            return _effectStatus.HasStatus(statusType);
        }

        public bool AddRuntimeEffect(RuntimeEffectInstance instance)
        {
            if (_effectStatus == null || instance == null)
            {
                return false;
            }

            if (!_effectStatus.AddEffect(instance))
            {
                return false;
            }

            ApplyEffectActions(instance);

            return true;
        }

        public bool RemoveRuntimeEffect(RuntimeEffectInstance instance)
        {
            if (_effectStatus == null || instance == null)
            {
                return false;
            }

            if (!_effectStatus.RemoveEffect(instance))
            {
                return false;
            }

            RemoveEffectActions(instance);

            return true;
        }

        public void RefreshRuntimeEffect(RuntimeEffectInstance instance)
        {
            if (_effectStatus == null || _finalStatus == null || instance == null)
            {
                return;
            }

            Dictionary<UnitStatType, float> previousValues = _finalStatus.GetAffectedValues(instance);

            BuildEffectModifiers(instance, _effectModifierBuffer);

            _finalStatus.ReplaceModifiers(instance, _effectModifierBuffer);

            foreach (KeyValuePair<UnitStatType, float> pair in previousValues)
            {
                NotifyStatChanged(pair.Key, pair.Value);
            }
        }


        // ============================================================
        // Effect Action
        // ============================================================

        private void ApplyEffectActions(RuntimeEffectInstance instance)
        {
            BuildEffectModifiers(instance, _effectModifierBuffer);

            for (int i = 0; i < _effectModifierBuffer.Count; i++)
            {
                AddCombatModifier(_effectModifierBuffer[i]);
            }
        }

        private void BuildEffectModifiers(
            RuntimeEffectInstance instance,
            List<CombatStatModifier> modifiers)
        {
            modifiers.Clear();

            IReadOnlyList<EffectActionData> actions = instance.Definition.Actions;

            float stackMultiplier = GetEffectStackMultiplier(instance);

            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i] is not StatEffectActionData statAction)
                {
                    continue;
                }

                float value = statAction.Value * stackMultiplier;

                modifiers.Add(new CombatStatModifier(instance, statAction.StatType, statAction.ModifierType, value));
            }
        }

        private void RemoveEffectActions(RuntimeEffectInstance instance)
        {
            RemoveCombatModifiers(instance);
        }

        private float GetEffectStackMultiplier(RuntimeEffectInstance instance)
        {
            if (instance.Definition.StackType != EffectStackType.Stack)
            {
                return 1f;
            }

            return instance.StackCount;
        }


        // ============================================================
        // Status Methods
        // ============================================================

        public bool IsImmuneToStatus(UnitStatusEffectType statusType)
        {
            if (_unitData == null)
                return false;

            return _unitData.IsImmuneToStatus(statusType);
        }

        private void OnStatusChanged(
            UnitStatusEffectType statusType,
            bool isActive)
        {
            if (_effectTransactionDepth > 0)
                return;

            StatusChanged?.Invoke(statusType, isActive);
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

            if (_effectTransactionDepth > 0)
            {
                if (!_deferredStats.ContainsKey(statType))
                    _deferredStats[statType] = previousValue;

                return;
            }

            float currentValue = _finalStatus.Get(statType);

            if (Mathf.Approximately(previousValue, currentValue))
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
                MaxHpChanged?.Invoke(previousValue, currentValue);
            }
        }
    }
}
