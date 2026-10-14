using UnityEngine;



namespace Units
{
    public readonly struct UnitAssignment
    {
        public ICombatTarget Target { get; }

        public Vector2 PreferredPosition { get; }

        public float PositionTolerance { get; }

        public bool WaitingForPosition { get; }

        public bool HasTarget =>
            Target != null && Target.IsTargetable;

        public UnitAssignment(
            ICombatTarget target,
            Vector2 preferredPosition,
            float positionTolerance, bool waitingForPosition = false)
        {
            WaitingForPosition = waitingForPosition;
            Target = target;
            PreferredPosition = preferredPosition;
            PositionTolerance = Mathf.Max(0f, positionTolerance);
        }
    }
}