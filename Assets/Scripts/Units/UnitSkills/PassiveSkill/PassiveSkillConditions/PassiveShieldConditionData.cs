
using System;
using UnityEngine;

namespace Units.Skills
{
    [Serializable]
    public class PassiveShieldConditionData
        : PassiveSkillConditionData
    {
        // ============================================================
        // Subject
        // ============================================================

        [SerializeField]
        private PassiveConditionSubjectType _subjectType = PassiveConditionSubjectType.Owner;


        // ============================================================
        // Condition
        // ============================================================

        [SerializeField]
        private PassiveValueComparisonType _comparisonType = PassiveValueComparisonType.Greater;

        [SerializeField, Min(0f)]
        private float _shieldValue = 0f;


        // ============================================================
        // Properties
        // ============================================================

        public PassiveConditionSubjectType SubjectType => _subjectType;

        public PassiveValueComparisonType ComparisonType => _comparisonType;

        public float ShieldValue => _shieldValue;


        // ============================================================
        // Evaluate
        // ============================================================

        public override CombatStateChange OwnerDependencies => SubjectType == PassiveConditionSubjectType.Owner ? CombatStateChange.Shield : CombatStateChange.None;

        public override CombatStateChange TargetDependencies => SubjectType == PassiveConditionSubjectType.Target ? CombatStateChange.Shield : CombatStateChange.None;

        public override bool Evaluate(PassiveContext context)
        {
            if (_subjectType == PassiveConditionSubjectType.Target && context.FrozenTarget != null)
                return Compare(context.FrozenTarget.Owner.Shield, Mathf.Max(0f, _shieldValue));

            ICombatTarget subject = GetSubject(context);

            if (subject == null)
                return false;

            return Compare(subject.CurrentShield, Mathf.Max(0f, _shieldValue));
        }


        // ============================================================
        // Subject
        // ============================================================

        private ICombatTarget GetSubject(PassiveContext context)
        {
            switch (_subjectType)
            {
                case PassiveConditionSubjectType.Owner:
                    return context.Owner;

                case PassiveConditionSubjectType.Target:
                    return context.Target;

                default:
                    return null;
            }
        }


        // ============================================================
        // Compare
        // ============================================================

        private bool Compare(
            float currentValue,
            float comparisonValue)
        {
            switch (_comparisonType)
            {
                case PassiveValueComparisonType.Less:
                    return currentValue < comparisonValue;

                case PassiveValueComparisonType.LessOrEqual:
                    return currentValue <= comparisonValue;

                case PassiveValueComparisonType.Greater:
                    return currentValue > comparisonValue;

                case PassiveValueComparisonType.GreaterOrEqual:
                    return currentValue >= comparisonValue;

                case PassiveValueComparisonType.Equal:
                    return Mathf.Approximately(currentValue, comparisonValue);

                default:
                    return false;
            }
        }
    }
}