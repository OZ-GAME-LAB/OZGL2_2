using System;
using UnityEngine;


namespace Units.Effects
{
    [Serializable]
    public class PeriodicDamageEffectActionData
        : EffectActionData
    {
        // ============================================================
        // Constants
        // ============================================================

        private const float MinInterval =
            0.01f;

        private const float MaxDamage =
            100000000f;


        // ============================================================
        // Data
        // ============================================================

        [SerializeField]
        [Min(MinInterval)]
        private float _interval =
            1f;

        [SerializeField]
        [Min(0f)]
        private float _damage;


        // ============================================================
        // Properties
        // ============================================================

        public float Interval =>
            _interval;

        public float Damage =>
            _damage;


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

            _damage =
                Mathf.Clamp(
                    _damage,
                    0f,
                    MaxDamage
                );
        }
#endif
    }
}