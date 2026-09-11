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

            ICombatTarget target = SelectTarget(unit);

            if (target == null)
            {
                context.ClearTarget();

                return false;
            }

            context.SetTarget(target);

            return true;
        }


        // =========================
        // Target Selection
        // =========================

        private ICombatTarget SelectTarget(Unit_Gateway unit)
        {
            ICombatTarget nearestTarget = null;
            ICombatTarget nearestUnassignedTarget = null;

            float nearestDistance = float.MaxValue;
            float nearestUnassignedDistance = float.MaxValue;

            Vector2 unitPosition = unit.transform.position;

            for (int i = 0; i < _enemyGroups.Count; i++)
            {
                Unit_GroupAI enemyGroup = _enemyGroups[i];

                if (enemyGroup == null)
                    continue;

                IReadOnlyList<Unit_Gateway> enemies =
                    enemyGroup.Members;

                for (int j = 0; j < enemies.Count; j++)
                {
                    Unit_Gateway enemy = enemies[j];

                    if (enemy == null)
                        continue;

                    ICombatTarget target = enemy;

                    if (!target.IsTargetable)
                        continue;

                    Vector2 targetPosition =
                        target.Transform.position;

                    float sqrDistance =
                        (targetPosition - unitPosition).sqrMagnitude;

                    if (sqrDistance < nearestDistance)
                    {
                        nearestDistance = sqrDistance;
                        nearestTarget = target;
                    }

                    if (IsAssignedTarget(unit, target))
                        continue;

                    if (sqrDistance < nearestUnassignedDistance)
                    {
                        nearestUnassignedDistance = sqrDistance;
                        nearestUnassignedTarget = target;
                    }
                }
            }

            if (nearestTarget == null)
                return null;

            if (nearestUnassignedTarget == null)
                return nearestTarget;

            float allowedDistance =
                nearestDistance *
                UnassignedDistanceRatio *
                UnassignedDistanceRatio;

            if (nearestUnassignedDistance <= allowedDistance)
                return nearestUnassignedTarget;

            return nearestTarget;
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

                UnitAssignmentContext context = pair.Value;

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
    }
}