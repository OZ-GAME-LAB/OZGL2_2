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

        private readonly Dictionary<Unit_Gateway, float> _advanceRows = new();

        private readonly Dictionary<Unit_Gateway, Vector2> _moveDestinations = new();

        private Vector2? _openingPoint;
        private float _openingCenterY;
        private readonly HashSet<Unit_Gateway> _openingArrivals = new();

        public void SetOpeningAdvance(Vector2 point)
        {
            _openingPoint = point;
            _openingCenterY = CalculateCenterPosition().y;
            _openingArrivals.Clear();
        }

        public void ClearOpeningAdvance()
        {
            _openingPoint = null;
            _openingArrivals.Clear();
        }


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


            // 목표 그룹이 바뀌어도 전진 중의 위아래 기준은 유지한다.
            // 새 전진을 시작할 때만 현재 배치를 기준으로 다시 잡는다.
            if (!_isAdvancing)
                _advanceRows.Clear();

            _isAdvancing =
                true;
            _progressPositions.Clear();
            _moveDestinations.Clear();
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
                - _advanceThreshold && !ReachedAssignedStep())
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
            _advanceRows.Clear();
            _moveDestinations.Clear();


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

            _moveDestinations.Clear();


            for (int i = 0;
                 i < _members.Count;
                 i++)
            {
                Unit_Gateway unit =
                    _members[i];


                if (unit == null)
                    continue;


                MoveMemberTowardFront(unit);

                _progressPositions[unit] = unit.Transform.position;
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
                    MoveMemberTowardFront(unit);
                }
                _progressPositions[unit] = position;
            }
        }

        // =========================
        // Front Approach
        // =========================

        private bool ReachedAssignedStep()
        {
            if (_moveDestinations.Count == 0)
                return false;

            float tolerance = Mathf.Max(0.01f, Mathf.Min(0.1f, _advanceThreshold));

            foreach (var pair in _moveDestinations)
            {
                if (pair.Key == null || !pair.Key.IsTargetable)
                    continue;

                if (((Vector2)pair.Key.Transform.position - pair.Value).sqrMagnitude
                    > tolerance * tolerance)
                {
                    return false;
                }
            }

            // Y 보정으로 짧아진 이동도 도착 즉시 이어간다. 정체 복구 주기를 기다리지 않는다.
            return true;
        }


        private void MoveMemberTowardFront(Unit_Gateway unit)
        {
            if (unit == null || !unit.IsTargetable || _advanceReference == null)
                return;

            Vector2 origin = unit.Transform.position;

            if (!_advanceRows.TryGetValue(unit, out float row))
            {
                row = origin.y;
                _advanceRows.Add(unit, row);
            }

            float sourceRadius = unit.BodyRadius;

            float basicRange =
                unit.RuntimeStatus?.BasicAttackData?.BasicAttackRange ?? 0f;

            float stopRange = Mathf.Max(
                sourceRadius,
                basicRange > 0f ? basicRange : unit.PreferredCombatRange
            );

            float nearestDistance = float.PositiveInfinity;

            float bestCost = float.PositiveInfinity;

            Vector2 targetPosition = origin;

            float targetRadius = 0f;

            for (int i = 0; i < _advanceReference.Members.Count; i++)
            {
                Unit_Gateway target = _advanceReference.Members[i];

                if (target == null || !target.IsTargetable)
                    continue;

                Vector2 position = target.Transform.position;

                float radius = target.BodyRadius;

                float distance = Mathf.Max(0f, Vector2.Distance(origin, position)
                    - CombatRangeUtility.Correction(sourceRadius, radius));

                nearestDistance = Mathf.Min(nearestDistance, distance);

                // 같은 적 그룹 안에서도 처음 배치된 높이의 상대를 우선한다.
                float cost = distance + Mathf.Abs(position.y - row) * 1.5f;

                if (cost >= bestCost)
                    continue;

                bestCost = cost;
                targetPosition = position;
                targetRadius = radius;
            }

            // 합류가 늦어도 사거리 안의 적을 지나쳐 더 깊이 전진하지 않는다.
            if (float.IsPositiveInfinity(bestCost) || nearestDistance <= stopRange + 0.01f)
            {
                _moveDestinations.Remove(unit);
                unit.StopMovement();

                return;
            }

            // 첫 접촉 전에는 개별 높이가 아닌 두 집단의 공통 지점으로 약진한다.
            // 이미 공격할 수 있으면 위에서 멈추므로 집결을 위해 적을 지나치지 않는다.
            if (_openingPoint.HasValue && !_openingArrivals.Contains(unit))
            {
                Vector2 point = _openingPoint.Value;
                // 소규모 편성 내부 간격은 조금 남겨 동일 좌표에 겹치는 것을 줄인다.
                point.y += Mathf.Clamp(row - _openingCenterY, -0.75f, 0.75f);
                Vector2 approach = point - origin;
                if (approach.magnitude > Mathf.Max(1f, sourceRadius * 2f))
                {
                    float step = Mathf.Min(_advanceDistance,
                        Mathf.Min(approach.magnitude, nearestDistance - stopRange));
                    Vector2 openingDestination = origin + approach.normalized * step;
                    _moveDestinations[unit] = openingDestination;
                    unit.MoveTo(openingDestination);
                    return;
                }
                _openingArrivals.Add(unit);
                // 집단 약진을 마쳤으면 새 높이를 유지한다. 최초 랠리 높이로 되돌아가지 않는다.
                row = point.y;
                _advanceRows[unit] = row;
            }

            float centerRange = stopRange
                + CombatRangeUtility.Correction(sourceRadius, targetRadius);

            // 적에게 닿을 수 있는 범위 안에서 기존 Y 위치를 최대한 보존한다.
            // 담당 높이에 적이 없을 때만 인접 전선 쪽으로 이동한다.
            float destinationY = Mathf.Clamp(
                row,
                targetPosition.y - centerRange * 0.75f,
                targetPosition.y + centerRange * 0.75f
            );

            float vertical = destinationY - targetPosition.y;

            float horizontal = Mathf.Sqrt(Mathf.Max(0f,
                centerRange * centerRange - vertical * vertical));

            float offsetX = targetPosition.x - origin.x;

            float destinationX = Mathf.Abs(offsetX) <= horizontal
                ? origin.x
                : targetPosition.x - Mathf.Sign(offsetX) * horizontal;

            Vector2 offset = new Vector2(destinationX, destinationY) - origin;

            // 먼 전선을 지원할 때도 한 번에 대각선으로 중앙을 가로지르지 않는다.
            float verticalStep = Mathf.Max(0.5f, Mathf.Abs(offset.x) * 0.35f);

            offset.y = Mathf.Clamp(offset.y, -verticalStep, verticalStep);

            float distanceToMove = Mathf.Min(
                _advanceDistance,
                Mathf.Min(offset.magnitude, nearestDistance - stopRange)
            );

            if (distanceToMove <= 0.01f)
            {
                _moveDestinations.Remove(unit);
                unit.StopMovement();

                return;
            }

            Vector2 destination = origin + offset.normalized * distanceToMove;

            _moveDestinations[unit] = destination;
            unit.MoveTo(destination);
        }


        // =========================
        // Reference Validation
        // =========================

        private bool IsAdvanceReferenceValid()
        {
            if (_advanceReference == null)
                return false;


            for (int i = 0; i < _advanceReference.Members.Count; i++)
            {
                Unit_Gateway member = _advanceReference.Members[i];

                if (member != null && member.IsTargetable)
                    return true;
            }

            return false;
        }


        private void HandleInvalidAdvanceReference()
        {
            StopAdvance();


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
