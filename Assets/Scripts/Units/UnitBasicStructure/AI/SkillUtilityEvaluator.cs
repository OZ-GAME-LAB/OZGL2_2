using System.Collections.Generic;
using Units.Skills;
using UnityEngine;

namespace Units
{
    // 전투 상태를 변경하지 않는 현재 시점 추정이다. 미래 이동·패시브 연쇄 결과를 보장하지 않는다.
    public sealed class SkillUtilityEvaluator
    {
        private readonly Unit_Core _core;
        private readonly TargetResolver _resolver;
        public SkillUtilityEvaluator(Unit_Core core, TargetResolver resolver) { _core = core; _resolver = resolver; }

        public float Evaluate(ActiveSkillData data, ICombatTarget initial, SkillUsePolicyData policy)
        {
            float score = 0;
            var actions = SkillActionPlan.Create(data);
            var initialSnapshot = new CombatTargetSnapshot(initial);
            var current = initialSnapshot;
            var previous = default(CombatTargetSnapshot);
            var counted = new HashSet<int>();
            Vector2 predictedPosition = _core.transform.position;
            for (int index = 0; index < actions.Count; index++)
            {
                var action = actions[index];
                if (action == null || !action.IsConfigured) break;
                // 이전 실행 결과는 미리 알 수 없으므로 기본 효용값으로 보완한다.
                if (action.Origin == SkillAreaOrigin.PreviousResult) break;
                Vector2 origin = action.Origin == SkillAreaOrigin.Target && current.IsTargetable ?
                    (Vector2)current.Target.Transform.position : predictedPosition;
                Vector2 direction = initialSnapshot.IsTargetable ? (Vector2)initial.Transform.position - origin : _core.FacingDirection;
                var selected = _resolver.ResolveSkillTargets(new SkillTargetRequest(_core.CombatTarget, action.Target,
                    origin, direction, initialTarget: initialSnapshot, currentTarget: current, previousTarget: previous));
                if (!selected.Success) break;
                var context = new SkillConditionContext(_core.CombatTarget, selected.PrimaryTarget, _resolver,
                    isActiveSkill: true, actionIndex: index, position: origin);
                var hits = new List<CombatTargetSnapshot>(selected.Targets);
                if (action is SkillAttackActionData attack && attack.Delivery == ActiveSkillDeliveryType.Direct)
                {
                    hits.Clear();
                    if (attack.Area == ActiveSkillAreaType.Single)
                    { if (selected.PrimaryTarget.IsTargetable) hits.Add(selected.PrimaryTarget); }
                    else
                    {
                        Vector2 center = attack.Area == ActiveSkillAreaType.TargetCircle && selected.PrimaryTarget.IsTargetable ?
                            (Vector2)selected.PrimaryTarget.Target.Transform.position : origin;
                        UnitTeam team = action.Target.Relation == SkillTargetRelation.Hostile ?
                            (_core.Team == UnitTeam.Ally ? UnitTeam.Enemy : UnitTeam.Ally) : _core.Team;
                        foreach (var target in _resolver.ResolveHitTargets(new TargetHitRequest(center, direction,
                            attack.Radius, attack.Angle, attack.MaxEffectTargets,
                            attack.Area == ActiveSkillAreaType.SelfCone ? HitAreaType.Cone : HitAreaType.Circle, team,
                            target => action.Target.Relation == SkillTargetRelation.Self
                                ? ReferenceEquals(target, _core.CombatTarget)
                                : action.Target.Relation != SkillTargetRelation.Friendly || action.Target.IncludeSelf
                                    || !ReferenceEquals(target, _core.CombatTarget))))
                        {
                            bool self = ReferenceEquals(target, _core.CombatTarget);
                            if (action.Target.Relation == SkillTargetRelation.Self && !self ||
                                action.Target.Relation == SkillTargetRelation.Friendly && !action.Target.IncludeSelf && self) continue;
                            hits.Add(new CombatTargetSnapshot(target));
                        }
                    }
                }
                foreach (var entry in action.BaseEffects)
                    if (entry != null && (entry.Timing != SkillEffectTiming.OnHit || action is SkillAttackActionData))
                        score += EntryScore(entry, entry.Timing == SkillEffectTiming.OnHit ? hits : new List<CombatTargetSnapshot>(selected.Targets), context, action.Conditions, policy, counted);
                foreach (var entry in action.ConditionalEffects)
                    if (entry != null && (entry.Timing != SkillEffectTiming.OnHit || action is SkillAttackActionData))
                        score += EntryScore(entry, entry.Timing == SkillEffectTiming.OnHit ? hits : new List<CombatTargetSnapshot>(selected.Targets), context, action.Conditions, policy, counted);
                previous = selected.PrimaryTarget; current = selected.PrimaryTarget;
                if (action is SkillDashActionData dash && selected.PrimaryTarget.IsTargetable)
                    predictedPosition = DashController.PredictEndPosition(predictedPosition,
                        selected.PrimaryTarget.Target.Transform.position, dash.Distance, dash.Speed);
            }
            return Mathf.Max(0, score);
        }

        private float EntryScore(SkillEffectEntry entry, List<CombatTargetSnapshot> triggers, SkillConditionContext actionContext,
            IReadOnlyList<SkillConditionData> actionConditions, SkillUsePolicyData policy, HashSet<int> counted)
        {
            bool once = entry.Subject == SkillEffectSubject.Self && entry.Frequency == SkillEffectFrequency.OncePerExecution;
            if (once && counted.Contains(entry.EntryId)) return 0;
            // None 대상으로도 시작/완료 시 자기 자신에게 효과를 적용할 수 있다.
            if (triggers.Count == 0 && entry.Timing != SkillEffectTiming.OnHit)
                triggers = new List<CombatTargetSnapshot> { default };
            float score = 0;
            foreach (var trigger in triggers)
            {
                var context = new SkillConditionContext(_core.CombatTarget, trigger, _resolver,
                    isActiveSkill: true, actionIndex: actionContext.ActionIndex, position: actionContext.Position);
                if (!SkillConditionData.All(actionConditions, context)) continue;
                if (entry is SkillConditionalEffectEntry conditional)
                {
                    if (!SkillConditionData.All(conditional.Conditions, context)) continue;
                    var subject = conditional.ConsumeSubject == SkillConditionSubject.Owner ? _core.CombatTarget : trigger.Target;
                    if (conditional.ConsumeStacks && (!CombatTargetUtility.IsValid(subject) || conditional.StackQuery == null ||
                        subject.RuntimeStatus.GetEffectStackCount(conditional.StackQuery) < conditional.StackCount)) continue;
                }
                var destinations = new List<CombatTargetSnapshot>();
                if (entry.Subject == SkillEffectSubject.Self) destinations.Add(new CombatTargetSnapshot(_core.CombatTarget));
                else if (entry.Subject == SkillEffectSubject.HitTarget) destinations.Add(trigger);
                else if (trigger.ObjectId != 0 && entry.SnapshotAreaTarget != null)
                    destinations.AddRange(_resolver.ResolveSkillTargets(new SkillTargetRequest(_core.CombatTarget,
                        entry.SnapshotAreaTarget.WithSource(SkillTargetSource.Search), trigger.Position, Vector2.right)).Targets);
                float appliedScore = 0;
                foreach (var destination in destinations)
                    if (destination.IsTargetable)
                        foreach (var effect in entry.Effects) appliedScore += EffectScore(effect, destination.Target, policy);
                score += appliedScore;
                if (once && appliedScore > 0) { counted.Add(entry.EntryId); break; }
            }
            return score;
        }

        private float EffectScore(SkillEffectData effect, ICombatTarget target, SkillUsePolicyData policy)
        {
            var owner = _core.RuntimeStatus;
            var stats = target.RuntimeStatus;
            if (stats == null || owner == null) return 0;
            bool friendly = target.Team == _core.Team;
            if (effect is SkillDamageEffectData damage)
            {
                if (friendly) return 0;
                float defense = Mathf.Max(0, stats.Defense) * (1 - Mathf.Clamp01(owner.DefenseIgnore));
                if (damage.DamageType == DamageType.Magic) defense *= .5f;
                float value = Mathf.Max(0, owner.AttackPower * owner.DamageMultiplier * owner.SkillDamageMultiplier * damage.DamageMultiplier - defense);
                return (friendly ? -1 : 1) * Mathf.Min(target.CurrentHp + target.CurrentShield, value * Mathf.Max(0, stats.DamageTakenMultiplier));
            }
            if (effect is SkillHealEffectData heal)
            {
                if (!friendly) return 0;
                float value = (heal.ScalingStatType == HealScalingStatType.MaxHp ? owner.MaxHp : owner.AttackPower) * heal.HealRatio * owner.HealingMultiplier * stats.HealingTakenMultiplier;
                return (friendly ? 1 : -1) * Mathf.Min(Mathf.Max(0, stats.MaxHp - target.CurrentHp), Mathf.Max(0, value));
            }
            if (effect is SkillShieldEffectData shield)
            {
                if (!friendly) return 0;
                float value = (shield.ScalingStatType == ShieldScalingStatType.MaxHp ? owner.MaxHp : owner.AttackPower) * shield.ShieldRatio;
                return (friendly ? 1 : -1) * Mathf.Min(Mathf.Max(0, stats.MaxHp * 2 - target.CurrentShield), Mathf.Max(0, value));
            }
            if (effect is SkillRuntimeEffectData runtime && runtime.EffectData != null)
                return policy.RuntimeEffectUtility;
            return 0;
        }
    }
}
