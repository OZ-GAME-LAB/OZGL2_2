using System;
using UnityEngine;


namespace Units.Effects
{
    [Serializable]
    public class StatEffectActionData
        : EffectActionData
    {
        // ============================================================
        // Data
        // ============================================================

        [SerializeField]
        private UnitStatType _statType;

        [SerializeField]
        private UnitStatModifierType _modifierType;

        [SerializeField]
        private float _value;


        // ============================================================
        // Properties
        // ============================================================

        public UnitStatType StatType =>
            _statType;

        public UnitStatModifierType ModifierType =>
            _modifierType;

        public float Value =>
            _value;
    }
}