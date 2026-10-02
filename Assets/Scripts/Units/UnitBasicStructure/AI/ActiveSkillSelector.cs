using System.Collections.Generic;
using UnityEngine;
using Units.Skills;

namespace Units
{
    internal sealed class ActiveSkillSelector
    {
        private readonly Unit_Core _core;
        private readonly TargetResolver _resolver;
        private readonly SkillUtilityEvaluator _evaluator;
        internal ActiveSkillSelector(Unit_Core core, TargetResolver resolver)
        { _core = core; _resolver = resolver; _evaluator = new SkillUtilityEvaluator(core, resolver); }

        internal bool Select(IReadOnlyList<RuntimeActiveSkill> skills, ICombatTarget current, out UnitDecision decision)
        {
            decision = default;
            bool found = false;
            int bestPriority = int.MinValue;
            float bestScore = float.NegativeInfinity;
            foreach (var skill in skills)
            {
                if (skill.Cooldown > 0 || skill.Retry > 0) continue;
                if (!Preview(skill, current, out var initial, out var targets, out var policy)) continue;
                var context = new SkillConditionContext(_core.CombatTarget, targets.PrimaryTarget, _resolver,
                    isActiveSkill: true, actionIndex: 0, position: targets.Origin);
                float score = policy.BaseUtility + policy.UtilityWeight * _evaluator.Evaluate(skill.Entry.Skill, initial, policy);
                if (float.IsNaN(score) || float.IsInfinity(score) || score < policy.MinimumUtility) continue;
                int priority = policy.Priority(context);
                if (found && (priority < bestPriority || priority == bestPriority && score <= bestScore)) continue;
                found = true; bestPriority = priority; bestScore = score;
                decision = new UnitDecision(UnitAIActionType.ActiveSkill, initial, skill.Entry.Id, _core.HeroPhaseVersion, score, _core.CombatTarget);
            }
            return found;
        }

        internal bool Preview(RuntimeActiveSkill skill, ICombatTarget current, out ICombatTarget initial,
            out SkillTargetResult targets, out SkillUsePolicyData policy)
        {
            initial = null; targets = null;
            policy = _core.GetSkillPolicy(skill.Entry);
            if (policy == null || !policy.Enabled) return false;
            var data = skill.Entry.Skill;
            UnitTeam team = data.TargetSide == SkillTargetRelation.Hostile ?
                (_core.Team == UnitTeam.Ally ? UnitTeam.Enemy : UnitTeam.Ally) : _core.Team;
            var candidates = _resolver.ResolveCandidates(new TargetCandidateRequest(_core.transform.position, data.SkillRange, team));
            initial = skill.Selector.SelectTarget(current, candidates);
            // 새 Action의 Current/Initial은 GroupAI 배정을 출발점으로 사용할 수 있다.
            if (!data.UsesLegacyTargetSelection && initial == null && CombatTargetUtility.IsValid(current)) initial = current;
            if (!skill.Executor.TryPreview(initial, out targets)) return false;
            return policy.Allows(new SkillConditionContext(_core.CombatTarget, targets.PrimaryTarget, _resolver,
                isActiveSkill: true, actionIndex: 0, position: targets.Origin));
        }
    }
}
