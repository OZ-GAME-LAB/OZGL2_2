using System;



namespace Units.Skills
{
    [Serializable]
    public abstract class PassiveSkillActionData
    {
#if UNITY_EDITOR
        public virtual void Validate()
        {
        }
#endif
    }
}