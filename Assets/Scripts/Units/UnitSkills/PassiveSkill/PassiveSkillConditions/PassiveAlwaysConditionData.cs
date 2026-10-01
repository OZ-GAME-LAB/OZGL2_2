using System;



namespace Units.Skills
{
    [Serializable]
    public class PassiveAlwaysConditionData
        : PassiveSkillConditionData
    {

        public override CombatStateChange OwnerDependencies => CombatStateChange.None;

        public override CombatStateChange TargetDependencies => CombatStateChange.None;

        public override bool UsesSpatialQuery => false;

        public override bool Evaluate(PassiveContext context)
        {
            return true;
        }
    }
}