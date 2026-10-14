using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Units.Skills;
using Units.Effects;
using UnityEngine;

// 발사 시 공격자 계산값·조건·정의를 고정하고 명중 시 대상 계산값과 결합한다.
namespace Units
{
    // Unity 객체 참조는 신원으로 유지하고, 직렬화된 관리형 정의는 실행별로 복사한다.
    internal static class SkillDefinitionCopy
    {

        // ============================================================
        // Execution
        // ============================================================

        public static T Copy<T>(T value) => (T)CopyObject(value);

        private static object CopyObject(object value)
        {
            if (value == null)
                return null;

            var type = value.GetType();

            if (type.IsValueType || value is string || value is UnityEngine.Object)
                return value;

            if (value is IList list)
            {
                var copy = (IList)Activator.CreateInstance(type);

                foreach (var item in list)
                    copy.Add(CopyObject(item));

                return copy;
            }

            var result = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);

            for (var current = type; current != null; current = current.BaseType)
                foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (!field.IsStatic && (field.IsPublic || field.IsDefined(typeof(SerializeField)) || field.IsDefined(typeof(SerializeReference))))
                        field.SetValue(result, CopyObject(field.GetValue(value)));

            if (result is SkillRuntimeEffectData runtime)
                runtime.FreezeDefinition();

            return result;
        }
    }

    // 계산값만 고정한다. 실제 적용/이벤트는 원래 ICombatTarget의 Gateway/Core 경로를 사용한다.
    public sealed class CombatSourceSnapshot
    {

        // ============================================================
        // Properties
        // ============================================================

        public CombatTargetSnapshot Owner { get; }

        public float AttackPower { get; }

        public float MaxHp { get; }

        public float BasicMultiplier { get; }

        public float SkillMultiplier { get; }

        public float DamageMultiplier { get; }

        public float DefenseIgnore { get; }

        public float CriticalChance { get; }

        public float CriticalDamage { get; }

        public float HealingMultiplier { get; }

        public IReadOnlyList<PassiveDamageModifier> Modifiers { get; }

        // ============================================================
        // Data / Runtime State
        // ============================================================

        private readonly Dictionary<string, int> _ids = new();

        private readonly HashSet<UnitStatusEffectType> _statuses = new();

        private readonly Dictionary<UnitStatusEffectType, int> _statusStacks = new();

        // ============================================================
        // Execution
        // ============================================================

        public int StackCount(Units.Effects.EffectStackQuery query)
        {
            if (query == null || !query.IsValid)
                return 0;

            if (query.Kind == EffectStackQueryKind.Status)
                return _statusStacks.TryGetValue(query.StatusType, out var stacks) ? stacks : 0;

            return _ids.TryGetValue(query.Key, out var count) ? count : 0;
        }

        public bool HasEffect(string id) => id != null && _ids.ContainsKey(id);

        public bool HasStatus(UnitStatusEffectType status) => _statuses.Contains(status);

        // ============================================================
        // Data / Runtime State
        // ============================================================

        private readonly Dictionary<RuntimePassiveSkill, IReadOnlyList<SkillConditionData>> _conditions = new();

        // ============================================================
        // Constructor
        // ============================================================

        public CombatSourceSnapshot(ICombatTarget owner)
        {
            Owner = new CombatTargetSnapshot(owner);

            var stats = owner.RuntimeStatus;

            foreach (var effect in stats.ActiveEffects)
            {
                _ids[effect.Definition.EffectId] = (_ids.TryGetValue(effect.Definition.EffectId, out var existing) ? existing : 0) + effect.StackCount;

                foreach (UnitStatusEffectType status in Enum.GetValues(typeof(UnitStatusEffectType)))
                    if (effect.Definition.HasStatus(status))
                        _statusStacks[status] = (_statusStacks.TryGetValue(status, out var stacks) ? stacks : 0) + effect.StackCount;

            }

            foreach (UnitStatusEffectType status in Enum.GetValues(typeof(UnitStatusEffectType)))
                if (stats.HasStatus(status))
                    _statuses.Add(status);

            AttackPower = stats.AttackPower;

            MaxHp = stats.MaxHp;

            BasicMultiplier = stats.BasicAttackMultiplier;

            SkillMultiplier = stats.SkillDamageMultiplier;

            DamageMultiplier = stats.DamageMultiplier;

            DefenseIgnore = stats.DefenseIgnore;

            CriticalChance = stats.CriticalChance;

            CriticalDamage = stats.CriticalDamage;

            HealingMultiplier = stats.HealingMultiplier;

            var modifiers = new List<PassiveDamageModifier>();

            owner.CollectDamageModifiers(PassiveDamageOwnerType.Attacker, modifiers);

            var copies = new List<PassiveDamageModifier>();

            var context = new SkillConditionContext(
                owner,
                default,
                new TargetResolver()
            );

            foreach (var modifier in modifiers)
            {
                copies.Add(new PassiveDamageModifier(modifier.RuntimePassive, SkillDefinitionCopy.Copy(modifier.Action)));

                if (!_conditions.ContainsKey(modifier.RuntimePassive))
                    _conditions.Add(modifier.RuntimePassive, FreezeConditions(modifier.RuntimePassive.Data.Conditions, context));
            }

            Modifiers = copies.AsReadOnly();
        }

        // ============================================================
        // Execution
        // ============================================================

        public AttackerContext Build(DamageRequest request) => new(
            request.Attacker,
            request.SourceType,
            request.DamageType,
            AttackPower,
            request.DamageMultiplier,
            request.SourceType == DamageSourceType.BasicAttack ? BasicMultiplier : SkillMultiplier,
            DamageMultiplier,
            DefenseIgnore,
            CriticalChance,
            CriticalDamage,
            Modifiers,
            this
        );

        public bool Evaluate(
            PassiveDamageModifier modifier,
            ICombatTarget target) => _conditions.TryGetValue(modifier.RuntimePassive, out var conditions) && SkillConditionData.All(conditions, new SkillConditionContext(Owner.Target, new CombatTargetSnapshot(target), new TargetResolver(), sourceSnapshot: this));

        internal static IReadOnlyList<SkillConditionData> FreezeConditions<T>(
            IReadOnlyList<T> conditions,
            SkillConditionContext context)
            where T : SkillConditionData
        {
            var result = new List<SkillConditionData>();

            foreach (var original in conditions)
            {
                if (original == null)
                    continue;

                var condition = SkillDefinitionCopy.Copy<SkillConditionData>(original);

                if (condition.TargetDependencies == CombatStateChange.None)
                    condition = new FrozenCondition(condition.Evaluate(context));

                else if (condition.OwnerDependencies != CombatStateChange.None && !condition.SupportsSourceSnapshot)
                {
                    Debug.LogWarning("[SkillSnapshot] Mixed/unknown condition requires a snapshot adapter: " + condition.GetType().Name);

                    condition = new FrozenCondition(false);
                }

                result.Add(condition);
            }

            return result.AsReadOnly();
        }

        private sealed class FrozenCondition:SkillConditionData
        {

            // ============================================================
            // Data / Runtime State
            // ============================================================

            private readonly bool _value;

            // ============================================================
            // Constructor
            // ============================================================

            public FrozenCondition(bool value)
            {
                _value = value;
            }

            // ============================================================
            // Execution
            // ============================================================

            public override bool Evaluate(SkillConditionContext context) => _value;
        }
    }
}
