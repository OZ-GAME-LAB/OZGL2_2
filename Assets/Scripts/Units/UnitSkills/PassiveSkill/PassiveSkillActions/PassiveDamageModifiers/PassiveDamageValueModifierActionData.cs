using System;
using UnityEngine;


namespace Units.Skills
{
    [Serializable]
    public class PassiveDamageValueModifierActionData
        : PassiveDamageModifierActionData
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
        // Modifier
        // ============================================================

        [Header("Modifier")]
        [SerializeField]
        private UnitStatModifierType _modifierType =
            UnitStatModifierType.Percent;

        [SerializeField]
        private float _value;


        // ============================================================
        // Properties
        // ============================================================

        public override PassiveDamageCalculationType CalculationType =>
            PassiveDamageCalculationType.Damage;

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