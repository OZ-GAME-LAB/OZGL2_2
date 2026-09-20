using System;
using UnityEngine;



namespace Units.Skills
{
    [Serializable]
    public class PassiveHealthConditionData
        : PassiveSkillConditionData
    {
        // ============================================================
        // Subject
        // ============================================================

        [Header("Subject")]
        [SerializeField]
        private PassiveConditionSubjectType _subjectType =
            PassiveConditionSubjectType.Owner;


        // ============================================================
        // Condition
        // ============================================================

        [Header("Condition")]
        [SerializeField]
        private PassiveValueComparisonType _comparisonType =
            PassiveValueComparisonType.LessOrEqual;

        [SerializeField]
        [Range(0f, 1f)]
        private float _healthRatio =
            0.5f;


        // ============================================================
        // Properties
        // ============================================================

        public PassiveConditionSubjectType SubjectType =>
            _subjectType;

        public PassiveValueComparisonType ComparisonType =>
            _comparisonType;

        public float HealthRatio =>
            _healthRatio;


        // ============================================================
        // Evaluate
        // ============================================================

        public override bool Evaluate(
            PassiveContext context)
        {
            ICombatTarget subject =
                GetSubject(
                    context
                );

            if (subject == null ||
                subject.RuntimeStatus == null)
            {
                return false;
            }

            float maxHp =
                subject.RuntimeStatus.MaxHp;

            if (maxHp <= 0f)
                return false;

            float currentRatio =
                subject.CurrentHp /
                maxHp;

            return Compare(
                currentRatio,
                _healthRatio
            );
        }


        // ============================================================
        // Subject
        // ============================================================

        private ICombatTarget GetSubject(
            PassiveContext context)
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
            float targetValue)
        {
            switch (_comparisonType)
            {
                case PassiveValueComparisonType.Less:
                    return currentValue <
                           targetValue;

                case PassiveValueComparisonType.LessOrEqual:
                    return currentValue <=
                           targetValue;

                case PassiveValueComparisonType.Greater:
                    return currentValue >
                           targetValue;

                case PassiveValueComparisonType.GreaterOrEqual:
                    return currentValue >=
                           targetValue;

                case PassiveValueComparisonType.Equal:
                    return Mathf.Approximately(
                        currentValue,
                        targetValue
                    );

                default:
                    return false;
            }
        }
    }
}