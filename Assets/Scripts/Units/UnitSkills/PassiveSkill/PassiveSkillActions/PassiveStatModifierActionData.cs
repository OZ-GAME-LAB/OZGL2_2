using System;
using UnityEngine;


namespace Units.Skills
{
    [Serializable]
    public class PassiveStatModifierActionData
        : PassiveSkillActionData
    {
        // ============================================================
        // Constants
        // ============================================================

        private const float MinFlatValue =
            -100000000f;

        private const float MaxFlatValue =
            100000000f;

        private const float MinPercentValue =
            -1f;

        private const float MaxPercentValue =
            10f;


        // ============================================================
        // Stat
        // ============================================================

        [Header("Stat")]
        [SerializeField]
        private UnitStatType _statType =
            UnitStatType.AttackPower;

        [SerializeField]
        private UnitStatModifierType _modifierType =
            UnitStatModifierType.Flat;

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


        // ============================================================
        // Validation
        // ============================================================

#if UNITY_EDITOR
        public override void Validate()
        {
            switch (_modifierType)
            {
                case UnitStatModifierType.Flat:

                    _value =
                        Mathf.Clamp(
                            _value,
                            MinFlatValue,
                            MaxFlatValue
                        );

                    break;


                case UnitStatModifierType.Percent:

                    _value =
                        Mathf.Clamp(
                            _value,
                            MinPercentValue,
                            MaxPercentValue
                        );

                    break;
            }
        }
#endif
    }
}