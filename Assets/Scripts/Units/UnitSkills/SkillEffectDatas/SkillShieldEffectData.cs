using System;
using UnityEngine;



namespace Units.Skills
{
    [Serializable]
    public class SkillShieldEffectData
        : SkillEffectData
    {
        [SerializeField]
        private ShieldScalingStatType _scalingStatType =
            ShieldScalingStatType.MaxHp;

        [SerializeField]
        [Tooltip("시전자 능력치를 기준으로 적용할 보호막 계수")]
        private float _shieldRatio =
            1f;


        public ShieldScalingStatType ScalingStatType =>
            _scalingStatType;

        public float ShieldRatio =>
            _shieldRatio;
    }
}