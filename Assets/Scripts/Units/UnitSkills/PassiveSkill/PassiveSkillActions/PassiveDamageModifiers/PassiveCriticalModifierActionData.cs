using System;
using UnityEngine;


namespace Units.Skills
{
    [Serializable]
    public class PassiveCriticalModifierActionData
        : PassiveDamageModifierActionData
    {
        // ============================================================
        // Constants
        // ============================================================

        private const float MinChanceValue =
            -1f;

        private const float MaxChanceValue =
            1f;

        private const float MinDamageValue =
            -10f;

        private const float MaxDamageValue =
            10f;


        // ============================================================
        // Critical
        // ============================================================

        [Header("Critical")]
        [SerializeField]
        private PassiveCriticalModifierType _modifierType =
            PassiveCriticalModifierType.Chance;

        [SerializeField]
        private float _value;


        // ============================================================
        // Properties
        // ============================================================

        public override PassiveDamageCalculationType CalculationType =>
            PassiveDamageCalculationType.Critical;

        public PassiveCriticalModifierType ModifierType =>
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
                case PassiveCriticalModifierType.Chance:

                    _value =
                        Mathf.Clamp(
                            _value,
                            MinChanceValue,
                            MaxChanceValue
                        );

                    break;


                case PassiveCriticalModifierType.Damage:

                    _value =
                        Mathf.Clamp(
                            _value,
                            MinDamageValue,
                            MaxDamageValue
                        );

                    break;
            }
        }
#endif
    }
}