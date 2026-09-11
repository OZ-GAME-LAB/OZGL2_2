using System;
using System.Collections.Generic;
using UnityEngine;



namespace Units
{
    public class Unit_Gateway :
        MonoBehaviour,
        ICombatTarget
    {
        // ============================================================
        // Reference
        // ============================================================

        private Unit_Core _core;

        private Unit_GroupAI _groupAI;


        // ============================================================
        // Runtime State
        // ============================================================

        private bool _isRallyMoving;

        private Vector2 _rallyPosition;


        // ============================================================
        // Events
        // ============================================================

        public event Action<Unit_Gateway> Died;

        public event Action<Unit_Gateway> RequestFullAssignmentEvent;

        public event Action<Unit_Gateway> RequestPositionAssignmentEvent;

        public event Action<Unit_Gateway> RallyCompleted;


        // ============================================================
        // Properties
        // ============================================================

        public UnitTeam Team
            => _core != null
                ? _core.Team
                : default;


        public Unit_GroupAI GroupAI
            => _groupAI;


        public Transform Transform
            => transform;


        public bool IsAlive
            => _core != null
            && _core.IsAlive;


        public bool IsTargetable
            => IsAlive;


        public bool CanUseActiveSkill
            => _core != null
            && _core.CanUseActiveSkill;


        public float PreferredCombatRange
            => _core != null
                ? _core.PreferredCombatRange
                : 0f;


        // ============================================================
        // Initialize
        // ============================================================

        internal void Initialize(
            Unit_Core core)
        {
            if (core == null)
            {
                Debug.LogError(
                    $"[Unit_Gateway] {name} : Unit_Core가 없습니다."
                );

                return;
            }


            UnbindEvents();


            _core =
                core;


            _isRallyMoving =
                false;


            BindEvents();
        }


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void OnDestroy()
        {
            UnbindEvents();
        }


        // ============================================================
        // Events
        // ============================================================

        private void BindEvents()
        {
            if (_core == null)
                return;


            _core.MovementCompleted +=
                OnMovementCompleted;

            _core.MovementFailed +=
                OnMovementFailed;
        }


        private void UnbindEvents()
        {
            if (_core == null)
                return;


            _core.MovementCompleted -=
                OnMovementCompleted;

            _core.MovementFailed -=
                OnMovementFailed;
        }


        private void OnMovementCompleted()
        {
            if (!_isRallyMoving)
                return;


            _isRallyMoving =
                false;


            _core?.HoldMovementPosition(
                _rallyPosition
            );


            RallyCompleted?.Invoke(
                this
            );
        }


        private void OnMovementFailed()
        {
            if (!_isRallyMoving)
                return;


            _isRallyMoving =
                false;

            RallyCompleted?.Invoke(
                this
            );

            Debug.LogError($"[Unit_Gateway] {gameObject.name} Rally movement failed.");
        }


        // ============================================================
        // Group
        // ============================================================

        internal void SetGroupAI(
            Unit_GroupAI groupAI)
        {
            _groupAI =
                groupAI;
        }


        internal void ClearGroupAI()
        {
            _groupAI =
                null;
        }


        // ============================================================
        // Assignment
        // ============================================================

        public void SetUnitAssignment(
            UnitAssignment assignment)
        {
            if (_core == null)
                return;


            _core.SetUnitAssignment(
                assignment
            );
        }


        public void ClearUnitAssignment()
        {
            if (_core == null)
                return;


            _core.ClearUnitAssignment();
        }


        public void RequestFullAssignment()
        {
            RequestFullAssignmentEvent?.Invoke(this);
        }


        public void RequestPositionAssignment()
        {
            RequestPositionAssignmentEvent?.Invoke(this);
        }


        // ============================================================
        // AI
        // ============================================================

        public void StartAI()
        {
            if (_core == null)
                return;


            _core.ReleaseMovementPosition();

            _core.StartAI();
        }


        public void PauseAI()
        {
            _core?.PauseAI();
        }


        // ============================================================
        // Life
        // ============================================================

        public void TakeDamage(
            DamageResult result)
        {
            _core?.TakeDamage(
                result
            );
        }


        internal void NotifyDeath()
        {
            Died?.Invoke(
                this
            );
        }


        // ============================================================
        // Advance
        // ============================================================

        public void MoveTo(
            Vector2 destination)
        {
            _core?.MoveTo(
                destination
            );
        }


        public void StopMovement()
        {
            _core?.StopMovement();
        }


        // ============================================================
        // Rally
        // ============================================================

        public void MoveToRally(
            Vector2 destination)
        {
            if (_core == null)
                return;


            _rallyPosition =
                destination;

            _isRallyMoving =
                true;


            _core.MoveTo(
                destination
            );
        }
    }
}