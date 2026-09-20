using System;
using UnityEngine;


namespace Units.Effects
{
    [Serializable]
    public class PeriodicHealEffectActionData
        : EffectActionData
    {
        // ============================================================
        // Constants
        // ============================================================

        private const float MinInterval =
            0.01f;

        private const float MinHealRatio =
            0f;

        private const float MaxHealRatio =
            10f;


        // ============================================================
        // Data
        // ============================================================

        [SerializeField]
        [Min(MinInterval)]
        private float _interval =
            1f;


        [SerializeField]
        [Tooltip("대상의 현재 최대 체력을 기준으로 한 Tick당 회복 비율")]
        private float _healRatio =
            1f;


        // ============================================================
        // Properties
        // ============================================================

        public float Interval =>
            _interval;


        public float HealRatio =>
            _healRatio;


        // ============================================================
        // Validation
        // ============================================================

#if UNITY_EDITOR
        public override void Validate()
        {
            _interval =
                Mathf.Max(
                    MinInterval,
                    _interval
                );

            _healRatio =
                Mathf.Clamp(
                    _healRatio,
                    MinHealRatio,
                    MaxHealRatio
                );
        }
#endif
    }
}