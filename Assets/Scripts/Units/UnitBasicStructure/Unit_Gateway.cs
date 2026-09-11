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
        // Events
        // ============================================================

        public event Action<Unit_Gateway> Died;

        public event Action<Unit_Gateway> RequestFullAssignmentEvent;

        public event Action<Unit_Gateway> RequestPositionAssignmentEvent;


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


            _core =
                core;
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
            _core?.StartAI();
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
    }
}