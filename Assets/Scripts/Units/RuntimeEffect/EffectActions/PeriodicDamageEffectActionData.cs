using System;
using UnityEngine;



namespace Units
{
    [Serializable]
    public class PeriodicDamageEffectActionData : EffectActionData
    {
        [SerializeField]
        private float _interval;

        [SerializeField]
        private float _damage;


        public float Interval => _interval;

        public float Damage => _damage;
    }
}