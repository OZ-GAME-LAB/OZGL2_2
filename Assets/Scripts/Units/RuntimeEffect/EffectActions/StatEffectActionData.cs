using System;
using UnityEngine;



namespace Units
{
    [Serializable]
    public class StatEffectActionData : EffectActionData
    {
        [SerializeField]
        private UnitStatType _statType;

        [SerializeField]
        private UnitStatModifierType _modifierType;

        [SerializeField]
        private float _value;


        public UnitStatType StatType => _statType;

        public UnitStatModifierType ModifierType => _modifierType;

        public float Value => _value;
    }
}