using System.Collections.Generic;
using UnityEngine;



namespace Units
{
    public class PositionAssigner
    {
        // =========================
        // Constants
        // =========================

        private const float DefaultPositionTolerance =
            0.25f;

        private const float PositionCheckRadius =
            0.5f;

        private const float AngleStep =
            30f;

        private const int MaxPositionAttempts =
            12;

        private const float PreferredRangeRatio =
            0.9f;


        // =========================
        // References
        // =========================

        private readonly Dictionary<
            Unit_Gateway,
            UnitAssignmentContext>
            _assignmentContexts;


        // =========================
        // Constructor
        // =========================

        public PositionAssigner(
            Dictionary<
                Unit_Gateway,
                UnitAssignmentContext>
                assignmentContexts)
        {
            _assignmentContexts =
                assignmentContexts;
        }


        // =========================
        // Position Assignment
        // =========================

        public bool TryAssignPosition(
            Unit_Gateway unit)
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
            {
                context.ClearPosition();

                return false;
            }


            ICombatTarget target =
                context.AssignedTarget;


            if (target.Transform == null)
            {
                context.ClearPosition();

                return false;
            }


            float preferredRange =
                CalculatePreferredRange(
                    unit
                );


            Vector2 position =
                FindPreferredPosition(
                    unit,
                    target,
                    preferredRange
                );


            context.SetPosition(
                position,
                DefaultPositionTolerance
            );


            return true;
        }


        // =========================
        // Preferred Range
        // =========================

        private float CalculatePreferredRange(
            Unit_Gateway unit)
        {
            float combatRange =
                Mathf.Max(
                    0f,
                    unit.PreferredCombatRange
                );


            return combatRange
                * PreferredRangeRatio;
        }


        // =========================
        // Position Selection
        // =========================

        private Vector2 FindPreferredPosition(
            Unit_Gateway unit,
            ICombatTarget target,
            float preferredRange)
        {
            Vector2 unitPosition =
                unit.transform.position;

            Vector2 targetPosition =
                target.Transform.position;


            Vector2 direction =
                unitPosition
                - targetPosition;


            if (direction.sqrMagnitude
                <= Mathf.Epsilon)
            {
                direction =
                    Vector2.left;
            }
            else
            {
                direction.Normalize();
            }


            Vector2 preferredPosition =
                targetPosition
                + direction
                * preferredRange;


            if (IsPositionAvailable(
                unit,
                preferredPosition))
            {
                return preferredPosition;
            }


            return FindAlternativePosition(
                unit,
                targetPosition,
                direction,
                preferredRange,
                preferredPosition
            );
        }


        // =========================
        // Alternative Position
        // =========================

        private Vector2 FindAlternativePosition(
            Unit_Gateway unit,
            Vector2 targetPosition,
            Vector2 baseDirection,
            float preferredRange,
            Vector2 fallbackPosition)
        {
            for (int i = 1;
                 i <= MaxPositionAttempts;
                 i++)
            {
                float angle =
                    GetSearchAngle(
                        i
                    );


                Vector2 direction =
                    Rotate(
                        baseDirection,
                        angle
                    );


                Vector2 candidatePosition =
                    targetPosition
                    + direction
                    * preferredRange;


                if (IsPositionAvailable(
                    unit,
                    candidatePosition))
                {
                    return candidatePosition;
                }
            }


            return fallbackPosition;
        }


        private float GetSearchAngle(
            int attempt)
        {
            int step =
                (attempt + 1) / 2;


            float direction =
                attempt % 2 == 1
                    ? 1f
                    : -1f;


            return AngleStep
                * step
                * direction;
        }


        // =========================
        // Position Validation
        // =========================

        private bool IsPositionAvailable(
            Unit_Gateway requester,
            Vector2 position)
        {
            float checkRadiusSqr =
                PositionCheckRadius
                * PositionCheckRadius;


            foreach (
                KeyValuePair<
                    Unit_Gateway,
                    UnitAssignmentContext> pair
                in _assignmentContexts)
            {
                if (pair.Key == requester)
                    continue;


                UnitAssignmentContext context =
                    pair.Value;


                if (!context.HasPosition)
                    continue;


                Vector2 assignedPosition =
                    context.AssignedPosition.Value;


                float sqrDistance =
                    (
                        assignedPosition
                        - position
                    ).sqrMagnitude;


                if (sqrDistance
                    < checkRadiusSqr)
                {
                    return false;
                }
            }


            return true;
        }


        // =========================
        // Utility
        // =========================

        private Vector2 Rotate(
            Vector2 direction,
            float angle)
        {
            float radian =
                angle
                * Mathf.Deg2Rad;


            float cos =
                Mathf.Cos(
                    radian
                );

            float sin =
                Mathf.Sin(
                    radian
                );


            return new Vector2(
                direction.x * cos
                - direction.y * sin,

                direction.x * sin
                + direction.y * cos
            );
        }
    }
}