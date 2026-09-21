using System;



namespace Units.Skills
{
    [Serializable]
    public class PassiveAlwaysConditionData
        : PassiveSkillConditionData
    {
        public override bool Evaluate(
            PassiveContext context)
        {
            return true;
        }
    }
}