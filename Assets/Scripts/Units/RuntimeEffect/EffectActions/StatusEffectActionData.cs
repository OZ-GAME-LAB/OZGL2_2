using System;
using UnityEngine;


namespace Units.Effects
{
    [Serializable]
    public class StatusEffectActionData
        : EffectActionData
    {
        // ============================================================
        // Data
        // ============================================================

        [SerializeField]
        private UnitStatusEffectType _statusType;


        // ============================================================
        // Properties
        // ============================================================

        public UnitStatusEffectType StatusType =>
            _statusType;
    }
}