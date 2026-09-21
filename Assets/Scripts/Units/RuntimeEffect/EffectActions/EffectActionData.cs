using System;


namespace Units.Effects
{
    [Serializable]
    public abstract class EffectActionData
    {
#if UNITY_EDITOR
        public virtual void Validate()
        {
        }
#endif
    }
}