using System;
using UnityEngine;



namespace Units.Skills
{
    [Serializable]
    public class SkillDamageEffectData
        : SkillEffectData
    {
        [SerializeField]
        private DamageType _damageType =
            DamageType.Physical;

        [SerializeField]
        private float _damageMultiplier =
            1f;


        public DamageType DamageType =>
            _damageType;

        public float DamageMultiplier =>
            _damageMultiplier;
    }
}