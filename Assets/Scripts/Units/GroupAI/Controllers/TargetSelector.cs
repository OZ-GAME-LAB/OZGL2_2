using System.Collections.Generic;
using UnityEngine;



namespace Units
{
    public class TargetSelector
    {
        // =========================
        // Constants
        // =========================

        private const float UnassignedDistanceRatio = 1.25f;

        // 현재 타겟이 안정적인 상태라면
        // 훨씬 가까운 적이 있을 때만 타겟 변경을 허용한다.
        private const float StableSwitchDistanceRatio = 0.4f;

        // 현재 타겟과의 전투 관계가 약해진 상태라면
        // 비교적 완화된 기준으로 타겟 변경을 허용한다.
        private const float WeakSwitchDistanceRatio = 0.8f;


        // =========================
        // Target State
        // =========================

        private enum CurrentTargetState
        {
            Invalid,
            Stable,
            Weak
        }


        // =========================
        // References
        // =========================

        private readonly List<Unit_GroupAI> _enemyGroups;

        private readonly Dictionary<Unit_Gateway, UnitAssignmentContext>
            _assignmentContexts;


        // =========================
        // Constructor
        // =========================

        public TargetSelector(
            List<Unit_GroupAI> enemyGroups,
            Dictionary<Unit_Gateway, UnitAssignmentContext> assignmentContexts)
        {
            _enemyGroups = enemyGroups;
            _assignmentContexts = assignmentContexts;
        }


        // =========================
        // Target Assignment
        // =========================

        public bool TryAssignTarget(Unit_Gateway unit)
        {
            if (unit == null)
                return false;

            if (!_assignmentContexts.TryGetValue(
                unit,
                out UnitAssignmentContext context))
            {
                return false;
            }

            ICombatTarget target =
                SelectTarget(unit);

            if (target == null)
            {
                context.ClearTarget();

                return false;
            }

            context.SetTarget(target);

            return true;
        }

        public bool TryReevaluateTarget(Unit_Gateway unit)
        {
            if (unit == null)
                return false;

            if (!_assignmentContexts.TryGetValue(
                unit,
                out UnitAssignmentContext context))
            {
                return false;
            }

            if (!context.HasTarget)
                return false;

            ICombatTarget currentTarget =
                context.AssignedTarget;

            CurrentTargetState currentTargetState =
                EvaluateCurrentTarget(
                    unit,
                    currentTarget
                );

            // Invalid 상태는 TargetReevaluation의 책임이 아니다.
            // 기존 FullAssignment 경로에서 새로운 타겟을 배정한다.
            if (currentTargetState ==
                CurrentTargetState.Invalid)
            {
                return false;
            }

            ICombatTarget switchTarget =
                SelectSwitchTarget(
                    unit,
                    currentTarget
                );

            if (switchTarget == null)
                return false;

            if (!ShouldSwitchTarget(
                unit,
                currentTarget,
                switchTarget,
                currentTargetState))
            {
                return false;
            }

            context.SetTarget(switchTarget);

            return true;
        }


        // =========================
        // Target Selection
        // =========================

        private ICombatTarget SelectTarget(
            Unit_Gateway unit)
        {
            ICombatTarget nearestTarget = null;
            ICombatTarget nearestUnassignedTarget = null;

            float nearestDistance = float.MaxValue;
            float nearestUnassignedDistance = float.MaxValue;

            Vector2 unitPosition =
                unit.transform.position;

            for (int i = 0; i < _enemyGroups.Count; i++)
            {
                Unit_GroupAI enemyGroup =
                    _enemyGroups[i];

                if (enemyGroup == null)
                    continue;

                IReadOnlyList<Unit_Gateway> enemies =
                    enemyGroup.Members;

                for (int j = 0; j < enemies.Count; j++)
                {
                    ICombatTarget target =
                        enemies[j];

                    if (!IsValidTarget(target))
                        continue;

                    float sqrDistance =
                        GetSqrDistance(
                            unitPosition,
                            target
                        );

                    EvaluateTargetCandidate(
                        unit,
                        target,
                        sqrDistance,
                        ref nearestTarget,
                        ref nearestDistance,
                        ref nearestUnassignedTarget,
                        ref nearestUnassignedDistance
                    );
                }
            }

            return SelectDistributedTarget(
                nearestTarget,
                nearestDistance,
                nearestUnassignedTarget,
                nearestUnassignedDistance
            );
        }

        private ICombatTarget SelectSwitchTarget(
            Unit_Gateway unit,
            ICombatTarget currentTarget)
        {
            ICombatTarget nearestTarget = null;
            ICombatTarget nearestUnassignedTarget = null;

            float nearestDistance = float.MaxValue;
            float nearestUnassignedDistance = float.MaxValue;

            Vector2 unitPosition =
                unit.transform.position;

            float currentTargetDistance =
                Vector2.Distance(
                    unitPosition,
                    currentTarget.Transform.position
                );

            for (int i = 0; i < _enemyGroups.Count; i++)
            {
                Unit_GroupAI enemyGroup =
                    _enemyGroups[i];

                if (enemyGroup == null)
                    continue;

                IReadOnlyList<Unit_Gateway> enemies =
                    enemyGroup.Members;

                for (int j = 0; j < enemies.Count; j++)
                {
                    ICombatTarget target =
                        enemies[j];

                    if (!IsValidTarget(target))
                        continue;

                    if (ReferenceEquals(
                        target,
                        currentTarget))
                    {
                        continue;
                    }

                    float candidateDistance =
                        Vector2.Distance(
                            unitPosition,
                            target.Transform.position
                        );

                    if (!IsSwitchCandidate(
                        currentTargetDistance,
                        candidateDistance))
                    {
                        continue;
                    }

                    float sqrDistance =
                        candidateDistance *
                        candidateDistance;

                    EvaluateTargetCandidate(
                        unit,
                        target,
                        sqrDistance,
                        ref nearestTarget,
                        ref nearestDistance,
                        ref nearestUnassignedTarget,
                        ref nearestUnassignedDistance
                    );
                }
            }

            return SelectDistributedTarget(
                nearestTarget,
                nearestDistance,
                nearestUnassignedTarget,
                nearestUnassignedDistance
            );
        }


        // =========================
        // Target Validation
        // =========================

        private bool IsValidTarget(
            ICombatTarget target)
        {
            if (target == null)
                return false;

            if (!target.IsTargetable)
                return false;

            if (target.Transform == null)
                return false;

            return true;
        }


        // =========================
        // Current Target Evaluation
        // =========================

        private CurrentTargetState EvaluateCurrentTarget(
            Unit_Gateway unit,
            ICombatTarget currentTarget)
        {
            if (!IsValidTarget(currentTarget))
                return CurrentTargetState.Invalid;

            float preferredCombatRange =
                unit.PreferredCombatRange;

            if (preferredCombatRange <= 0f)
                return CurrentTargetState.Weak;

            float currentDistance =
                Vector2.Distance(
                    unit.transform.position,
                    currentTarget.Transform.position
                );

            if (currentDistance <= preferredCombatRange)
                return CurrentTargetState.Stable;

            return CurrentTargetState.Weak;
        }


        // =========================
        // Switch Candidate
        // =========================

        private bool IsSwitchCandidate(
            float currentTargetDistance,
            float candidateDistance)
        {
            // 현재 타겟보다 더 먼 적은
            // 변경 대상으로 검토하지 않는다.
            if (candidateDistance >=
                currentTargetDistance)
            {
                return false;
            }

            return true;
        }


        // =========================
        // Candidate Evaluation
        // =========================

        private void EvaluateTargetCandidate(
            Unit_Gateway requester,
            ICombatTarget target,
            float sqrDistance,
            ref ICombatTarget nearestTarget,
            ref float nearestDistance,
            ref ICombatTarget nearestUnassignedTarget,
            ref float nearestUnassignedDistance)
        {
            if (sqrDistance < nearestDistance)
            {
                nearestDistance = sqrDistance;
                nearestTarget = target;
            }

            if (IsAssignedTarget(
                requester,
                target))
            {
                return;
            }

            if (sqrDistance <
                nearestUnassignedDistance)
            {
                nearestUnassignedDistance =
                    sqrDistance;

                nearestUnassignedTarget =
                    target;
            }
        }

        private ICombatTarget SelectDistributedTarget(
            ICombatTarget nearestTarget,
            float nearestDistance,
            ICombatTarget nearestUnassignedTarget,
            float nearestUnassignedDistance)
        {
            if (nearestTarget == null)
                return null;

            if (nearestUnassignedTarget == null)
                return nearestTarget;

            float allowedDistance =
                nearestDistance *
                UnassignedDistanceRatio *
                UnassignedDistanceRatio;

            if (nearestUnassignedDistance <=
                allowedDistance)
            {
                return nearestUnassignedTarget;
            }

            return nearestTarget;
        }


        // =========================
        // Target Switch
        // =========================

        private bool ShouldSwitchTarget(
            Unit_Gateway unit,
            ICombatTarget currentTarget,
            ICombatTarget switchTarget,
            CurrentTargetState currentTargetState)
        {
            if (!IsValidTarget(currentTarget))
                return false;

            if (!IsValidTarget(switchTarget))
                return false;

            Vector2 unitPosition =
                unit.transform.position;

            float currentDistance =
                Vector2.Distance(
                    unitPosition,
                    currentTarget.Transform.position
                );

            float switchDistance =
                Vector2.Distance(
                    unitPosition,
                    switchTarget.Transform.position
                );

            switch (currentTargetState)
            {
                case CurrentTargetState.Stable:
                    return IsStrongSwitch(
                        currentDistance,
                        switchDistance
                    );

                case CurrentTargetState.Weak:
                    return IsNormalSwitch(
                        currentDistance,
                        switchDistance
                    );
            }

            return false;
        }

        private bool IsStrongSwitch(
            float currentDistance,
            float switchDistance)
        {
            float allowedDistance =
                currentDistance *
                StableSwitchDistanceRatio;

            return switchDistance <=
                allowedDistance;
        }

        private bool IsNormalSwitch(
            float currentDistance,
            float switchDistance)
        {
            float allowedDistance =
                currentDistance *
                WeakSwitchDistanceRatio;

            return switchDistance <=
                allowedDistance;
        }


        // =========================
        // Assignment Check
        // =========================

        private bool IsAssignedTarget(
            Unit_Gateway requester,
            ICombatTarget target)
        {
            foreach (KeyValuePair<Unit_Gateway, UnitAssignmentContext>
                pair in _assignmentContexts)
            {
                if (pair.Key == requester)
                    continue;

                UnitAssignmentContext context =
                    pair.Value;

                if (!context.HasTarget)
                    continue;

                if (ReferenceEquals(
                    context.AssignedTarget,
                    target))
                {
                    return true;
                }
            }

            return false;
        }


        // =========================
        // Utility
        // =========================

        private float GetSqrDistance(
            Vector2 origin,
            ICombatTarget target)
        {
            Vector2 targetPosition =
                target.Transform.position;

            return (targetPosition - origin)
                .sqrMagnitude;
        }
    }
}