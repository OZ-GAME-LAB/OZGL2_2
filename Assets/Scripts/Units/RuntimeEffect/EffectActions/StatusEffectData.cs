using System;
using UnityEngine;


namespace Units
{
    [Serializable]
    public class StatusEffectActionData : EffectActionData
    {
        [SerializeField]
        private UnitStatusEffectType _statusType;


        public UnitStatusEffectType StatusType =>
            _statusType;
    }
}