using System;
using UnityEngine;



namespace Units
{
    public class UnitAssignmentContext
    {
        // =========================
        // Assignment State
        // =========================

        internal event Action<UnitAssignmentContext> Changed;

        internal Unit_Gateway Owner { get; }

        public ICombatTarget AssignedTarget { get; private set; }

        public Vector2? AssignedPosition { get; private set; }

        public float PositionTolerance { get; private set; }

        public bool WaitingForPosition { get; private set; }

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
        // Constructor
        // =========================

        public UnitAssignmentContext(Unit_Gateway owner = null)
        {
            Owner = owner;
        }


        // =========================
        // Assignment Management
        // =========================

        public void SetTarget(ICombatTarget target)
        {
            if (ReferenceEquals(AssignedTarget, target))
                return;

            AssignedTarget = target;

            WaitingForPosition = false;
            AssignedPosition = null;
            PositionTolerance = 0f;
            Assignment = null;

            Changed?.Invoke(this);
        }

        public void SetPosition(
            Vector2 position,
            float positionTolerance, bool waitingForPosition = false)
        {
            WaitingForPosition = waitingForPosition;
            AssignedPosition = position;
            PositionTolerance = Mathf.Max(0f, positionTolerance);

            Assignment = null;

            Changed?.Invoke(this);
        }

        public void SetAssignment(UnitAssignment assignment)
        {
            Assignment = assignment;
        }

        public void ClearTarget()
        {
            AssignedTarget = null;
            WaitingForPosition = false;
            AssignedPosition = null;
            PositionTolerance = 0f;
            Assignment = null;

            Changed?.Invoke(this);
        }

        public void ClearPosition()
        {
            WaitingForPosition = false;
            AssignedPosition = null;
            PositionTolerance = 0f;
            Assignment = null;

            Changed?.Invoke(this);
        }

        public void ClearAssignment()
        {
            Assignment = null;
        }

        public void Clear()
        {
            AssignedTarget = null;
            WaitingForPosition = false;
            AssignedPosition = null;
            PositionTolerance = 0f;
            Assignment = null;

            Changed?.Invoke(this);
        }
    }
}