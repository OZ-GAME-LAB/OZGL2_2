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

        // Recovery belongs to the assignment lifetime, not a shared selector buffer.
        private const float MeleeRangeLimit = 3f;
        private const float ProgressDistanceSqr = 0.04f;
        private const float StalledSeconds = 1f;
        private const float RecoverySeconds = 5f;
        private ICombatTarget _observedTarget;
        private Vector2 _progressPosition;
        private float _progressTime;
        private float _recoveryUntil;
        private readonly ICombatTarget[] _blockedTargets = new ICombatTarget[4];
        private readonly float[] _blockedUntil = new float[4];
        private int _blockedIndex;
        public int DetourSide { get; set; }
        public Vector2? DetourWaypoint { get; set; }
        public bool IsRecovering => Time.time < _recoveryUntil;

        // Position retries and target reevaluation can arrive in the same Think.
        // Keep this cache separate from ClearPosition, which is part of those retries.
        private ICombatTarget _positionCacheTarget;
        private Vector2 _positionCacheOrigin;
        private Vector2 _positionCacheTargetOrigin;
        private Vector2 _positionCacheDestination;
        private float _positionCacheTolerance;
        private bool _positionCacheWaiting;
        private float _positionCacheUntil;
        private int _positionCacheFrame = -1;
        public float NextPositionSearchTime { get; private set; }

        public bool TryReusePosition(Unit_Gateway unit)
        {
            if (!HasTarget || !ReferenceEquals(_positionCacheTarget, AssignedTarget)
                || AssignedTarget.Transform == null) return false;
            bool sameFrame = _positionCacheFrame == Time.frameCount;
            if (!sameFrame && (Time.time >= _positionCacheUntil
                || (_positionCacheWaiting && ((Vector2)unit.Transform.position - _positionCacheOrigin).sqrMagnitude > 0.04f)
                || ((Vector2)AssignedTarget.Transform.position - _positionCacheTargetOrigin).sqrMagnitude > 0.25f))
                return false;
            SetPosition(_positionCacheDestination, _positionCacheTolerance, _positionCacheWaiting);
            return true;
        }

        public void CachePosition(Unit_Gateway unit, float lifetime)
        {
            if (!HasPosition || !HasTarget) return;
            _positionCacheTarget = AssignedTarget;
            _positionCacheOrigin = unit.Transform.position;
            _positionCacheTargetOrigin = AssignedTarget.Transform.position;
            _positionCacheDestination = AssignedPosition.Value;
            _positionCacheTolerance = PositionTolerance;
            _positionCacheWaiting = WaitingForPosition;
            _positionCacheFrame = Time.frameCount;
            _positionCacheUntil = Time.time + lifetime;
        }

        public void DelayPositionSearch()
        {
            NextPositionSearchTime = Time.time + 0.5f;
        }

        public bool IsTemporarilyBlocked(ICombatTarget target)
        {
            for (int i = 0; i < _blockedTargets.Length; i++)
                if (ReferenceEquals(target, _blockedTargets[i]) && Time.time < _blockedUntil[i])
                    return true;
            return false;
        }

        public void ObserveApproach(Unit_Gateway unit, float range)
        {
            Vector2 position = unit.Transform.position;
            // Only short-range attackers recover. Cooldowns and disabled movement are not jams.
            if (!HasTarget || range > MeleeRangeLimit || (unit.Core != null &&
                (!unit.Core.CanMove || unit.Core.IsCombatBusy)) ||
                CombatRangeUtility.Distance(unit, AssignedTarget) <= range)
            {
                _observedTarget = null;
                _progressTime = Time.time;
                _recoveryUntil = 0f;
                DetourWaypoint = null;
                return;
            }
            if (!ReferenceEquals(_observedTarget, AssignedTarget) ||
                (position - _progressPosition).sqrMagnitude >= ProgressDistanceSqr)
            {
                _observedTarget = AssignedTarget;
                _progressPosition = position;
                _progressTime = Time.time;
                return;
            }
            if (Time.time - _progressTime < StalledSeconds) return;

            _blockedTargets[_blockedIndex] = AssignedTarget;
            _blockedUntil[_blockedIndex] = Time.time + RecoverySeconds;
            _blockedIndex = (_blockedIndex + 1) % _blockedTargets.Length;
            _recoveryUntil = Time.time + RecoverySeconds;
            _progressTime = Time.time;
            DetourWaypoint = null;
        }

        private void ResetRecovery()
        {
            _positionCacheTarget = null;
            _positionCacheFrame = -1;
            _positionCacheUntil = 0f;
            NextPositionSearchTime = 0f;
            _observedTarget = null;
            _recoveryUntil = 0f;
            DetourSide = 0;
            DetourWaypoint = null;
            Array.Clear(_blockedTargets, 0, _blockedTargets.Length);
            Array.Clear(_blockedUntil, 0, _blockedUntil.Length);
            _blockedIndex = 0;
        }

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
            DetourWaypoint = null;

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
            ResetRecovery();
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
            ResetRecovery();
            AssignedTarget = null;
            WaitingForPosition = false;
            AssignedPosition = null;
            PositionTolerance = 0f;
            Assignment = null;

            Changed?.Invoke(this);
        }
    }
}
