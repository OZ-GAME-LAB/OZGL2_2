using System;
using UnityEngine;


namespace Units.Skills
{
    [Serializable]
    public class PassiveDefenseModifierActionData
        : PassiveDamageModifierActionData
    {
        // ============================================================
        // Constants
        // ============================================================

        private const float MinValue =
            -1f;

        private const float MaxValue =
            1f;


        // ============================================================
        // Modifier
        // ============================================================

        [Header("Modifier")]
        [SerializeField]
        private float _value;


        // ============================================================
        // Properties
        // ============================================================

        public override PassiveDamageCalculationType CalculationType =>
            PassiveDamageCalculationType.Defense;

        // 공격자는 방어력 관통, 피격자는 받는 피해량에 적용
        public float Value =>
            _value;


        // ============================================================
        // Validation
        // ============================================================

#if UNITY_EDITOR
        public override void Validate()
        {
            _value =
                Mathf.Clamp(
                    _value,
                    MinValue,
                    MaxValue
                );
        }
#endif
    }
}