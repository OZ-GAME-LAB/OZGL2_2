using System;
using System.Collections.Generic;
using UnityEngine;



namespace Units.Skills
{
    [Serializable]
    public class PassiveUnitCountConditionData
        : PassiveSkillConditionData
    {
        // ============================================================
        // Target
        // ============================================================

        [Header("Target")]
        [SerializeField]
        private SkillTargetRelation _targetRelation =
            SkillTargetRelation.Hostile;


        // ============================================================
        // Range
        // ============================================================

        [Header("Range")]
        [SerializeField]
        [Min(0f)]
        private float _range =
            5f;


        // ============================================================
        // Condition
        // ============================================================

        [Header("Condition")]
        [SerializeField]
        private PassiveValueComparisonType _comparisonType =
            PassiveValueComparisonType.GreaterOrEqual;

        [SerializeField]
        [Min(0)]
        private int _count =
            1;


        // ============================================================
        // Properties
        // ============================================================

        public SkillTargetRelation TargetRelation =>
            _targetRelation;

        public float Range =>
            _range;

        public PassiveValueComparisonType ComparisonType =>
            _comparisonType;

        public int Count =>
            _count;


        // ============================================================
        // Evaluate
        // ============================================================

        public override bool Evaluate(
            PassiveContext context)
        {
            if (context.Owner == null ||
                context.Owner.Transform == null ||
                context.TargetResolver == null)
            {
                return false;
            }

            UnitTeam targetTeam =
                GetTargetTeam(
                    context.Owner.Team
                );

            IReadOnlyList<ICombatTarget> targets =
                context.TargetResolver.ResolveCandidates(
                    new TargetCandidateRequest(
                        context.Owner.Transform.position,
                        _range,
                        targetTeam
                    )
                );

            int targetCount =
                CountTargets(
                    targets,
                    context.Owner
                );

            return Compare(
                targetCount,
                _count
            );
        }


        // ============================================================
        // Target
        // ============================================================

        private UnitTeam GetTargetTeam(
            UnitTeam ownerTeam)
        {
            switch (_targetRelation)
            {
                case SkillTargetRelation.Friendly:
                    return ownerTeam;

                case SkillTargetRelation.Hostile:
                    return ownerTeam ==
                           UnitTeam.Ally
                        ? UnitTeam.Enemy
                        : UnitTeam.Ally;

                default:
                    return ownerTeam;
            }
        }


        private int CountTargets(
            IReadOnlyList<ICombatTarget> targets,
            ICombatTarget owner)
        {
            int count =
                0;

            for (int i = 0;
                 i < targets.Count;
                 i++)
            {
                ICombatTarget target =
                    targets[i];

                if (target == null)
                    continue;

                // 주변 아군 수에서는 패시브 보유자 자신을 제외한다.
                if (_targetRelation ==
                    SkillTargetRelation.Friendly &&
                    ReferenceEquals(
                        target,
                        owner
                    ))
                {
                    continue;
                }

                count++;
            }

            return count;
        }


        // ============================================================
        // Compare
        // ============================================================

        private bool Compare(
            int currentValue,
            int targetValue)
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
                    return currentValue ==
                           targetValue;

                default:
                    return false;
            }
        }


        // ============================================================
        // Validation
        // ============================================================

#if UNITY_EDITOR
        public void Validate()
        {
            _range =
                Mathf.Max(
                    0f,
                    _range
                );

            _count =
                Mathf.Max(
                    0,
                    _count
                );
        }
#endif
    }
}