using System;
using UnityEngine;



namespace Units.Skills
{
    [Serializable]
    public class SkillDamageEffectData
        : SkillEffectData
    {
        [SerializeField]
        private float _damageMultiplier =
            1f;

        public float DamageMultiplier =>
            _damageMultiplier;
    }
}