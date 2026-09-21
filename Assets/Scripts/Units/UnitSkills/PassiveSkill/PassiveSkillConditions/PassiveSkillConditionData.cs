using System;



namespace Units.Skills
{
    [Serializable]
    public abstract class PassiveSkillConditionData
    {
        // 패시브 실행 조건을 검사
        public abstract bool Evaluate(
            PassiveContext context);
    }
}