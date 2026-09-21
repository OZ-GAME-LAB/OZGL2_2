using System;
using Units.Effects;
using UnityEngine;



namespace Units.Skills
{
    [Serializable]
    public class PassiveRuntimeEffectConditionData
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
        // Effect
        // ============================================================

        [Header("Effect")]
        [SerializeField]
        private PassiveRuntimeEffectConditionType _conditionType =
            PassiveRuntimeEffectConditionType.Effect;

        [SerializeField]
        private string _effectId;

        [SerializeField]
        private UnitStatusEffectType _statusType;


        // ============================================================
        // Condition
        // ============================================================

        [Header("Condition")]
        [SerializeField]
        private bool _shouldExist =
            true;


        // ============================================================
        // Properties
        // ============================================================

        public PassiveConditionSubjectType SubjectType =>
            _subjectType;

        public PassiveRuntimeEffectConditionType ConditionType =>
            _conditionType;

        public string EffectId =>
            _effectId;

        public UnitStatusEffectType StatusType =>
            _statusType;

        public bool ShouldExist =>
            _shouldExist;


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

            bool exists =
                EvaluateEffect(
                    subject.RuntimeStatus
                );

            return exists ==
                   _shouldExist;
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
        // Effect
        // ============================================================

        private bool EvaluateEffect(
            Unit_RuntimeStatus runtimeStatus)
        {
            switch (_conditionType)
            {
                case PassiveRuntimeEffectConditionType.Effect:

                    if (string.IsNullOrEmpty(
                            _effectId))
                    {
                        return false;
                    }

                    return runtimeStatus.HasEffect(
                        _effectId
                    );


                case PassiveRuntimeEffectConditionType.Status:

                    return runtimeStatus.HasStatus(
                        _statusType
                    );


                default:
                    return false;
            }
        }
    }
}