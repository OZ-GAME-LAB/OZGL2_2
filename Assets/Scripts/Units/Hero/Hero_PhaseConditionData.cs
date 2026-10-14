using System;
using UnityEngine;
using Units.Skills;
namespace Units
{
    [Serializable]
    public abstract class Hero_PhaseConditionData
    { public abstract bool Evaluate(Unit_Core core, float elapsed); }

    [Serializable]
    public sealed class Hero_HealthCondition : Hero_PhaseConditionData
    {
        [SerializeField, Range(0, 1)] private float _ratio = .5f;
        [SerializeField] private SkillTargetComparison _comparison = SkillTargetComparison.LessOrEqual;
        public override bool Evaluate(Unit_Core core, float elapsed) => core.RuntimeStatus.MaxHp > 0 &&
            SkillNumericCondition.Compare(core.CurrentHp / core.RuntimeStatus.MaxHp, _ratio, _comparison);
    }
    [Serializable]
    public sealed class Hero_ElapsedCondition : Hero_PhaseConditionData
    {
        [SerializeField, Min(0)] private float _seconds = 10;
        public override bool Evaluate(Unit_Core core, float elapsed) => elapsed >= _seconds;
    }
    [Serializable]
    public sealed class Hero_AlwaysCondition : Hero_PhaseConditionData
    { public override bool Evaluate(Unit_Core core, float elapsed) => true; }
    [Serializable]
    public sealed class Hero_CommonCondition : Hero_PhaseConditionData
    {
        [SerializeReference] private SkillConditionData _condition;
        public override bool Evaluate(Unit_Core core, float elapsed) => _condition != null &&
            _condition.Evaluate(new SkillConditionContext(core.CombatTarget, new CombatTargetSnapshot(core.CombatTarget), new TargetResolver()));
    }
}
