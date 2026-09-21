using System;
using UnityEngine;
using Units.Effects;



namespace Units.Skills
{
    [Serializable]
    public class SkillRuntimeEffectData
        : SkillEffectData
    {
        [SerializeField]
        private EffectData _effectData;

        public EffectData EffectData =>
            _effectData;
    }
}