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

        private const float ProgressCheckInterval = 1.5f;
        private float _nextProgressCheckTime;
        private readonly Dictionary<Unit_Gateway, Vector2> _progressPositions = new();


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
            _progressPositions.Clear();
            _nextProgressCheckTime = Time.time + ProgressCheckInterval;


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


            RecoverStoppedMembers();

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
            _progressPositions.Clear();


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
                _progressPositions[unit] = currentPosition;
            }
        }


        // =========================
        // Reference Validation
        // =========================

        // 전진 중에는 개별 전투 AI가 일시정지되므로 이동 실패 복구를 그룹이 담당한다.
        // 움직이고 있는 구성원은 건드리지 않고 정체된 구성원만 현재 적 방향으로 재요청한다.
        private void RecoverStoppedMembers()
        {
            if (Time.time < _nextProgressCheckTime) return;
            _nextProgressCheckTime = Time.time + ProgressCheckInterval;
            for (int i = 0; i < _members.Count; i++)
            {
                var unit = _members[i];
                if (unit == null || unit.Transform == null) continue;
                Vector2 position = unit.Transform.position;
                if (!_progressPositions.TryGetValue(unit, out var previous)
                    || Vector2.Distance(position, previous) < 0.1f)
                {
                    Vector2 direction = _advanceReference.CenterPosition - position;
                    if (direction.sqrMagnitude > Mathf.Epsilon)
                        unit.MoveTo(position + direction.normalized * _advanceDistance);
                }
                _progressPositions[unit] = position;
            }
        }

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