using System;
using System.Collections.Generic;
using UnityEngine;
using Units.Effects;

// 효과 실행 조건을 조회 전용으로 평가하고 액티브·패시브에서 같은 조건을 재사용한다.
namespace Units.Skills
{
    // 조건은 조회만 한다. 스택 소비/이벤트 발행은 효과 실행 경계에서 처리한다.
    [Serializable]
    public abstract class SkillConditionData
    {

        // ============================================================
        // Execution
        // ============================================================

        public abstract bool Evaluate(SkillConditionContext context);

        // ============================================================
        // Properties
        // ============================================================

        public virtual CombatStateChange OwnerDependencies => CombatStateChange.All;

        public virtual CombatStateChange TargetDependencies => CombatStateChange.All;

        public virtual bool UsesSpatialQuery => false;

        // 양쪽 주체를 읽는 조건이 저장된 공격자 정보로도 평가 가능한지 명시한다.
        public virtual bool SupportsSourceSnapshot => false;

        // ============================================================
        // Execution
        // ============================================================

        public static bool All(
            IReadOnlyList<SkillConditionData> conditions,
            SkillConditionContext context)
        {
            if (conditions == null)
                return true;

            foreach (var condition in conditions)
                if (condition != null && !condition.Evaluate(context))
                    return false;

            return true;
        }
    }

    public readonly struct SkillConditionContext
    {

        // ============================================================
        // Properties
        // ============================================================

        public CombatTargetSnapshot Owner { get; }

        public CombatSourceSnapshot SourceSnapshot { get; }

        public CombatSourceSnapshot FrozenTarget { get; }

        public bool CanApply => SourceSnapshot != null || Owner.IsTargetable;

        public CombatTargetSnapshot Target { get; }

        public TargetResolver Resolver { get; }

        public CombatEventMetadata Metadata { get; }

        public bool IsActiveSkill { get; }

        public int ActionIndex { get; }

        public ActionExecutionResult PreviousResult { get; }

        public Vector2 Position { get; }

        // ============================================================
        // Constructor
        // ============================================================

        public SkillConditionContext(
            ICombatTarget owner,
            CombatTargetSnapshot target,
            TargetResolver resolver,
            CombatEventMetadata metadata = default,
            bool isActiveSkill = false,
            int actionIndex = -1,
            ActionExecutionResult previousResult = null,
            Vector2 position = default,
            CombatSourceSnapshot sourceSnapshot = null,
            CombatSourceSnapshot frozenTarget = null)
        {
            FrozenTarget = frozenTarget;

            SourceSnapshot = sourceSnapshot;

            Owner = sourceSnapshot?.Owner ?? new CombatTargetSnapshot(owner);

            Target = frozenTarget?.Owner ?? target;

            Resolver = resolver;

            Metadata = metadata;

            IsActiveSkill = isActiveSkill;

            ActionIndex = actionIndex;

            PreviousResult = previousResult;

            Position = position;
        }

        // ============================================================
        // Execution
        // ============================================================

        public PassiveContext AsPassive() => new PassiveContext(
            Owner.IsTargetable ? Owner.Target : null,
            Target.IsTargetable ? Target.Target : null,
            Resolver,
            frozenTarget: FrozenTarget
        );
    }

    public enum SkillConditionSubject
    {
        Owner,
        Target
    }
    public enum SkillConditionValue
    {
        Hp,
        HpRatio,
        MissingHpRatio,
        Shield,
        Defense
    }

    [Serializable]
    public sealed class SkillNumericCondition : SkillConditionData
    {

        // ============================================================
        // Data / Runtime State
        // ============================================================

        [SerializeField]
        private SkillConditionSubject _subject;

        [SerializeField]
        private SkillConditionValue _metric = SkillConditionValue.HpRatio;

        [SerializeField]
        private SkillTargetComparison _comparison = SkillTargetComparison.LessOrEqual;

        [SerializeField]
        private float _value = 0.5f;

        [SerializeField]
        private bool _allowDeathSnapshot;

        // ============================================================
        // Properties
        // ============================================================

        public override CombatStateChange OwnerDependencies => _subject == SkillConditionSubject.Owner ? CombatStateChange.Health | CombatStateChange.Shield | CombatStateChange.Stats : CombatStateChange.None;

        public override CombatStateChange TargetDependencies => _subject == SkillConditionSubject.Target ? CombatStateChange.Health | CombatStateChange.Shield | CombatStateChange.Stats : CombatStateChange.None;

        // ============================================================
        // Execution
        // ============================================================

        public override bool Evaluate(SkillConditionContext context)
        {
            if (_subject == SkillConditionSubject.Target && context.FrozenTarget != null)
                return EvaluateSnapshot(context.FrozenTarget.Owner);

            var snapshot = _subject == SkillConditionSubject.Owner ? context.Owner : context.Target;

            if (!snapshot.IsTargetable && (!_allowDeathSnapshot || snapshot.ObjectId == 0))
                return false;

            var value = snapshot.IsTargetable ? new CombatTargetSnapshot(snapshot.Target) : snapshot;

            return EvaluateSnapshot(value);
        }

        private bool EvaluateSnapshot(CombatTargetSnapshot value)
        {
            float ratio = value.MaxHp > 0f ? value.Hp / value.MaxHp : 0f;

            float actual = _metric switch
            {
                SkillConditionValue.Hp => value.Hp,
                SkillConditionValue.HpRatio => ratio,
                SkillConditionValue.MissingHpRatio => 1f - ratio,
                SkillConditionValue.Shield => value.Shield,
                _ => value.Defense
            };

            return Compare(
                actual,
                _value,
                _comparison
            );
        }

        internal static bool Compare(
            float a,
            float b,
            SkillTargetComparison comparison) => comparison switch
        {
            SkillTargetComparison.Less => a < b,
            SkillTargetComparison.LessOrEqual => a <= b,
            SkillTargetComparison.Greater => a > b,
            SkillTargetComparison.GreaterOrEqual => a >= b,
            SkillTargetComparison.Equal => Mathf.Approximately(a, b),
            _ => false
        };
    }

    [Serializable]
    public sealed class SkillStackCondition : SkillConditionData
    {

        // ============================================================
        // Data / Runtime State
        // ============================================================

        [SerializeField]
        private SkillConditionSubject _subject = SkillConditionSubject.Target;

        [SerializeField]
        private EffectStackQuery _query = new();

        [SerializeField]
        private SkillTargetComparison _comparison = SkillTargetComparison.GreaterOrEqual;

        [SerializeField, Min(0)]
        private int _count = 1;

        [SerializeField]
        private bool _consumeOnApply;

        [SerializeField, Min(1)]
        private int _consumeCount = 1;

        public SkillConditionSubject Subject => _subject;
        public EffectStackQuery Query => _query;
        public bool ConsumeOnApply => _consumeOnApply;
        public int ConsumeCount => _consumeCount;


        // ============================================================
        // Properties
        // ============================================================

        public override CombatStateChange OwnerDependencies => _subject == SkillConditionSubject.Owner ? CombatStateChange.Effects : CombatStateChange.None;

        public override CombatStateChange TargetDependencies => _subject == SkillConditionSubject.Target ? CombatStateChange.Effects : CombatStateChange.None;

        // ============================================================
        // Execution
        // ============================================================

        public override bool Evaluate(SkillConditionContext context)
        {
            if (_subject == SkillConditionSubject.Target && context.FrozenTarget != null && _query != null && _query.IsValid)
                return SkillNumericCondition.Compare(
                    context.FrozenTarget.StackCount(_query),
                    _count,
                    _comparison
                );

            var subject = _subject == SkillConditionSubject.Owner ? context.Owner : context.Target;

            return subject.IsTargetable && _query != null && _query.IsValid && SkillNumericCondition.Compare(
                subject.Target.RuntimeStatus.GetEffectStackCount(_query),
                _count,
                _comparison
            );
        }
    }

    public enum SkillPreviousResultCondition
    {
        ActionIndex,
        SynchronousHitCount,
        AppliedCount,
        HasResultPosition
    }
    [Serializable]
    public sealed class SkillExecutionCondition : SkillConditionData
    {

        // ============================================================
        // Properties
        // ============================================================

        public override CombatStateChange OwnerDependencies => CombatStateChange.None;

        public override CombatStateChange TargetDependencies => CombatStateChange.None;

        // ============================================================
        // Data / Runtime State
        // ============================================================

        [SerializeField]
        private SkillPreviousResultCondition _kind;

        [SerializeField]
        private SkillTargetComparison _comparison = SkillTargetComparison.GreaterOrEqual;

        [SerializeField]
        private int _value = 1;

        // ============================================================
        // Execution
        // ============================================================

        public override bool Evaluate(SkillConditionContext context)
        {
            if (!context.IsActiveSkill)
                return false;

            if (_kind == SkillPreviousResultCondition.ActionIndex)
                return SkillNumericCondition.Compare(
                    context.ActionIndex,
                    _value,
                    _comparison
                );

            var result = context.PreviousResult;

            if (result == null)
                return false;

            if (_kind == SkillPreviousResultCondition.HasResultPosition)
                return SkillNumericCondition.Compare(
                    result.HasPosition ? 1 : 0,
                    _value,
                    _comparison
                );

            if (!result.HitsObserved)
                return false; // 비행 중인 투사체의 늦은 결과는 관찰하지 않는다.
            int count = _kind == SkillPreviousResultCondition.SynchronousHitCount ? result.HitTargets.Count : result.AppliedCount;

            return SkillNumericCondition.Compare(
                count,
                _value,
                _comparison
            );
        }
    }

    // 기존 패시브 목록에도 공통 조건을 넣을 수 있는 직렬화 호환 어댑터.
    [Serializable]
    public sealed class PassiveCommonConditionData : PassiveSkillConditionData
    {

        // ============================================================
        // Data / Runtime State
        // ============================================================

        [SerializeReference]
        private SkillConditionData _condition;

        // ============================================================
        // Properties
        // ============================================================

        public override CombatStateChange OwnerDependencies => _condition?.OwnerDependencies ?? CombatStateChange.None;

        public override CombatStateChange TargetDependencies => _condition?.TargetDependencies ?? CombatStateChange.None;

        public override bool UsesSpatialQuery => _condition?.UsesSpatialQuery ?? false;

        public override bool SupportsSourceSnapshot => _condition?.SupportsSourceSnapshot ?? false;

        // ============================================================
        // Execution
        // ============================================================

        public override bool Evaluate(SkillConditionContext context) => _condition != null && _condition.Evaluate(context);

        public override bool Evaluate(PassiveContext context) => _condition != null && _condition.Evaluate(new SkillConditionContext(context.Owner, context.TargetSnapshot, context.TargetResolver, context.Metadata, frozenTarget: context.FrozenTarget));
    }
}
