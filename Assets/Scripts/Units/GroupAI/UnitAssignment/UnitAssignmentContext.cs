using UnityEngine;



namespace Units
{
    public class UnitAssignmentContext
    {
        // =========================
        // Assignment State
        // =========================

        public ICombatTarget AssignedTarget { get; private set; }

        public Vector2? AssignedPosition { get; private set; }

        public float PositionTolerance { get; private set; }

        public UnitAssignment? Assignment { get; private set; }


        // =========================
        // State
        // =========================

        public bool HasTarget =>
            AssignedTarget != null && AssignedTarget.IsTargetable;

        public bool HasPosition =>
            AssignedPosition.HasValue;

        public bool HasAssignment =>
            Assignment.HasValue;


        // =========================
        // Assignment Management
        // =========================

        public void SetTarget(ICombatTarget target)
        {
            if (ReferenceEquals(AssignedTarget, target))
                return;

            AssignedTarget = target;

            AssignedPosition = null;
            PositionTolerance = 0f;
            Assignment = null;
        }

        public void SetPosition(
            Vector2 position,
            float positionTolerance)
        {
            AssignedPosition = position;
            PositionTolerance = Mathf.Max(0f, positionTolerance);

            Assignment = null;
        }

        public void SetAssignment(UnitAssignment assignment)
        {
            Assignment = assignment;
        }

        public void ClearTarget()
        {
            AssignedTarget = null;
            AssignedPosition = null;
            PositionTolerance = 0f;
            Assignment = null;
        }

        public void ClearPosition()
        {
            AssignedPosition = null;
            PositionTolerance = 0f;
            Assignment = null;
        }

        public void ClearAssignment()
        {
            Assignment = null;
        }

        public void Clear()
        {
            AssignedTarget = null;
            AssignedPosition = null;
            PositionTolerance = 0f;
            Assignment = null;
        }
    }
}