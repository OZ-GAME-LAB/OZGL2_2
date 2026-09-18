using System;
using System.Collections.Generic;
using UnityEngine;


namespace Units
{
    public class AdvanceController
    {
        // =========================
        // Settings
        // =========================

        private readonly float _advanceDistance;

        private readonly float _advanceThreshold;


        // =========================
        // Reference
        // =========================

        private readonly IReadOnlyList<Unit_Gateway>
            _members;

        private Unit_GroupAI
            _advanceReference;


        // =========================
        // Runtime
        // =========================

        private bool _isAdvancing;

        private Vector2 _advancePosition;

        private Vector2 _advanceDirection;

        private Vector2 _stepStartPosition;


        // =========================
        // Events
        // =========================

        public event Action
            AdvanceReferenceRequired;


        // =========================
        // Constructor
        // =========================

        public AdvanceController(
            IReadOnlyList<Unit_Gateway> members,
            float advanceDistance,
            float arrivalThreshold)
        {
            _members =
                members;

            _advanceDistance =
                advanceDistance;

            _advanceThreshold =
                arrivalThreshold;
        }


        // =========================
        // Reference
        // =========================

        public void SetAdvanceReference(
            Unit_GroupAI reference)
        {
            _advanceReference =
                reference;
        }


        public void ClearAdvanceReference()
        {
            _advanceReference =
                null;
        }


        // =========================
        // Advance
        // =========================

        public void StartAdvance()
        {
            if (_members.Count == 0)
                return;


            if (!IsAdvanceReferenceValid())
            {
                HandleInvalidAdvanceReference();

                return;
            }


            _isAdvancing =
                true;


            MoveNextStep();
        }


        public void UpdateAdvance()
        {
            if (!_isAdvancing)
                return;


            if (_members.Count == 0)
                return;


            if (!IsAdvanceReferenceValid())
            {
                HandleInvalidAdvanceReference();

                return;
            }


            Vector2 currentCenter =
                CalculateCenterPosition();


            Vector2 movedVector =
                currentCenter
                - _stepStartPosition;


            float movedDistance =
                Vector2.Dot(
                    movedVector,
                    _advanceDirection
                );


            if (movedDistance <
                _advanceDistance
                - _advanceThreshold)
            {
                return;
            }


            MoveNextStep();
        }


        public void StopAdvance()
        {
            if (!_isAdvancing)
                return;


            _isAdvancing =
                false;


            for (int i = 0;
                 i < _members.Count;
                 i++)
            {
                Unit_Gateway unit =
                    _members[i];


                if (unit == null)
                    continue;


                unit.StopMovement();
            }
        }


        // =========================
        // Movement
        // =========================

        private void MoveNextStep()
        {
            if (_members.Count == 0)
                return;


            if (!IsAdvanceReferenceValid())
            {
                HandleInvalidAdvanceReference();

                return;
            }


            Vector2 centerPosition =
                CalculateCenterPosition();


            _advancePosition =
                _advanceReference.CenterPosition;


            Vector2 direction =
                _advancePosition
                - centerPosition;


            if (direction.sqrMagnitude
                <= Mathf.Epsilon)
            {
                return;
            }


            _advanceDirection =
                direction.normalized;


            _stepStartPosition =
                centerPosition;


            for (int i = 0;
                 i < _members.Count;
                 i++)
            {
                Unit_Gateway unit =
                    _members[i];


                if (unit == null)
                    continue;


                Vector2 currentPosition =
                    unit.Transform.position;


                Vector2 destination =
                    currentPosition
                    + _advanceDirection
                    * _advanceDistance;


                unit.MoveTo(
                    destination
                );
            }
        }


        // =========================
        // Reference Validation
        // =========================

        private bool IsAdvanceReferenceValid()
        {
            if (_advanceReference == null)
                return false;


            return _advanceReference.Members.Count > 0;
        }


        private void HandleInvalidAdvanceReference()
        {
            _isAdvancing =
                false;


            _advanceReference =
                null;


            AdvanceReferenceRequired?.Invoke();
        }


        // =========================
        // Position
        // =========================

        private Vector2 CalculateCenterPosition()
        {
            if (_members.Count == 0)
                return Vector2.zero;


            Vector2 sum =
                Vector2.zero;

            int count =
                0;


            for (int i = 0;
                 i < _members.Count;
                 i++)
            {
                Unit_Gateway unit =
                    _members[i];


                if (unit == null)
                    continue;


                if (unit.Transform == null)
                    continue;


                sum +=
                    (Vector2)unit.Transform.position;

                count++;
            }


            if (count == 0)
                return Vector2.zero;


            return sum / count;
        }
    }
}