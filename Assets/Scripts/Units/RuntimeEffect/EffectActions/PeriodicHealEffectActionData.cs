using System;
using Units.Skills;
using UnityEngine;

namespace Units.Effects
{
    [Serializable]
    public class PeriodicHealEffectActionData
        : EffectActionData
    {
        // ============================================================
        // Data
        // ============================================================

        [SerializeField]
        [Min(0.01f)]
        private float _interval =
            1f;


        [SerializeField]
        [Tooltip("대상의 현재 최대 체력을 기준으로 한 Tick당 회복 비율 (%)")]
        private float _healRatio =
            100f;


        // ============================================================
        // Properties
        // ============================================================

        public float Interval =>
            _interval;


        public float HealRatio =>
            _healRatio;
    }
}