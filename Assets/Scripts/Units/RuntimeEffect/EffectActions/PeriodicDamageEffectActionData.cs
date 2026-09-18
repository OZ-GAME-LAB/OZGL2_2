using System;
using UnityEngine;


namespace Units.Effects
{
    [Serializable]
    public class PeriodicDamageEffectActionData
        : EffectActionData
    {
        // ============================================================
        // Data
        // ============================================================

        [SerializeField]
        [Min(0.01f)]
        private float _interval = 1f;

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
    }
}