using System;
using UnityEngine;

namespace Units.Skills
{
    [Serializable]
    public class SkillHealEffectData
        : SkillEffectData
    {
        [SerializeField]
        private HealScalingStatType _scalingStatType =
            HealScalingStatType.AttackPower;

        [SerializeField]
        [Tooltip("시전자 능력치를 기준으로 적용할 회복 계수 (%)")]
        private float _healRatio =
            100f;


        public HealScalingStatType ScalingStatType =>
            _scalingStatType;

        public float HealRatio =>
            _healRatio;
    }
}