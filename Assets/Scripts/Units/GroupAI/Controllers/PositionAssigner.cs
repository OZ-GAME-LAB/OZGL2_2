using System.Collections.Generic;
using UnityEngine;



namespace Units
{
    public class PositionAssigner
    {
        // =========================
        // Constants
        // =========================

        private const float DefaultPositionTolerance = 0.25f;

        private const int MaxPathQueries = 4;

        private const int PhysicsBufferSize = 32;

        private static readonly float[] SearchAngles =
        {
            0f, 15f, -15f, 30f, -30f, 60f, -60f, 90f, -90f
        };

        private static readonly float[] RangeRatios =
        {
            0.9f, 0.75f, 0.6f
        };


        // =========================
        // Position State
        // =========================

        private struct PositionCandidate
        {
            public Vector2 Position;

            public float Distance;
        }


        private struct PositionReservation
        {
            public Vector2 Position;

            public float Spacing;
        }


        // =========================
        // References
        // =========================

        private readonly Dictionary<Unit_Gateway, UnitAssignmentContext>
            _assignmentContexts;


        // =========================
        // Runtime Buffer
        // =========================

        private readonly PositionCandidate[] _positions =
            new PositionCandidate[SearchAngles.Length * RangeRatios.Length];

        private readonly List<PositionReservation> _reservations = new();

        private readonly Collider2D[] _occupants =
            new Collider2D[PhysicsBufferSize];

        private readonly RaycastHit2D[] _pathHits =
            new RaycastHit2D[PhysicsBufferSize];

        private ContactFilter2D _contactFilter;

        private int _positionCount;

        private float _bodyRadius;


        // =========================
        // Constructor
        // =========================

        public PositionAssigner(
            Dictionary<Unit_Gateway, UnitAssignmentContext> assignmentContexts)
        {
            _assignmentContexts = assignmentContexts;

            _contactFilter.SetLayerMask(
                LayerMask.GetMask("Unit", "Building")
            );

            _contactFilter.useTriggers = false;
        }


        // =========================
        // Position Assignment
        // =========================

        public bool TryAssignPosition(Unit_Gateway unit)
        {
            if (unit == null)
                return false;

            if (!_assignmentContexts.TryGetValue(
                unit,
                out UnitAssignmentContext context))
            {
                return false;
            }

            if (!context.HasTarget || context.AssignedTarget.Transform == null)
            {
                context.ClearPosition();

                return false;
            }

            ICombatTarget target = context.AssignedTarget;

            _bodyRadius = unit.BodyRadius;

            float basicRange =
                unit.RuntimeStatus?.BasicAttackData?.BasicAttackRange ?? 0f;

            float range = basicRange > 0f
                ? basicRange
                : unit.PreferredCombatRange;

            if (CombatRangeUtility.Distance(unit, target) <= range)
            {
                context.SetPosition(
                    unit.transform.position,
                    DefaultPositionTolerance
                );

                return true;
            }

            GatherReservations(unit);

            GatherPositions(unit, target, range);

            Vector2 bestPosition = unit.transform.position;

            float bestCost = float.PositiveInfinity;

            int pathQueries = 0;

            for (int i = 0; i < _positionCount; i++)
            {
                PositionCandidate candidate = _positions[i];

                // 혼잡 비용은 음수가 아니다. 거리만으로 이미 불리하면
                // 해당 후보와 그보다 먼 후보의 물리 검사를 생략한다.
                if (candidate.Distance >= bestCost || pathQueries >= MaxPathQueries)
                    break;

                if (!IsPositionAvailable(unit, candidate.Position))
                    continue;

                pathQueries++;

                float cost = candidate.Distance + PathCongestion(unit, candidate.Position);

                if (cost >= bestCost)
                    continue;

                bestCost = cost;
                bestPosition = candidate.Position;
            }

            // 빈자리가 없으면 막힌 위치로 밀어 넣지 않는다.
            // 타겟을 유지한 채 Unit_AI의 짧은 위치 재탐색 주기를 사용한다.
            context.SetPosition(
                bestPosition,
                DefaultPositionTolerance,
                float.IsPositiveInfinity(bestCost)
            );

            return true;
        }


        // =========================
        // Position Selection
        // =========================

        private void GatherPositions(
            Unit_Gateway unit,
            ICombatTarget target,
            float range)
        {
            _positionCount = 0;

            Vector2 origin = unit.transform.position;

            Vector2 center = target.Transform.position;

            Vector2 direction = (origin - center).normalized;

            if (direction == Vector2.zero)
                direction = Vector2.left;

            float targetRadius = target.BodyRadius;

            float bodyDistance = _bodyRadius + targetRadius;

            float correction = CombatRangeUtility.Correction(_bodyRadius, targetRadius);

            // 대상의 가까운 반원에서 각도와 깊이를 함께 탐색한다.
            // 실제 위치 탐색은 타겟 선정 후 선택된 한 대상에게만 수행한다.
            for (int i = 0; i < RangeRatios.Length; i++)
            {
                float radius = Mathf.Max(
                    bodyDistance,
                    range * RangeRatios[i] + correction
                );

                for (int j = 0; j < SearchAngles.Length; j++)
                {
                    Vector2 position = center + Rotate(direction, SearchAngles[j]) * radius;

                    AddPosition(position, Vector2.Distance(origin, position));
                }
            }
        }


        private void AddPosition(
            Vector2 position,
            float distance)
        {
            // 큰 몸체는 여러 사거리 비율이 같은 반지름으로 보정될 수 있다.
            // 같은 위치가 물리 검사 예산을 중복 소비하지 않게 한다.
            for (int i = 0; i < _positionCount; i++)
            {
                if ((_positions[i].Position - position).sqrMagnitude < 0.0001f)
                    return;
            }

            int index = _positionCount++;

            while (index > 0 && distance < _positions[index - 1].Distance)
            {
                _positions[index] = _positions[index - 1];
                index--;
            }

            _positions[index] = new PositionCandidate
            {
                Position = position,
                Distance = distance
            };
        }


        // =========================
        // Position Reservation
        // =========================

        private void GatherReservations(Unit_Gateway requester)
        {
            _reservations.Clear();

            IReadOnlyList<Unit_GroupAI> groups = requester.GroupAI?.EngagementAllies;

            if (groups == null)
            {
                AddReservations(requester, _assignmentContexts);

                return;
            }

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i] == null)
                    continue;

                AddReservations(requester, groups[i].AssignmentContexts);
            }
        }


        private void AddReservations(
            Unit_Gateway requester,
            IReadOnlyDictionary<Unit_Gateway, UnitAssignmentContext> assignments)
        {
            if (assignments is Dictionary<Unit_Gateway, UnitAssignmentContext> dictionary)
            {
                foreach (KeyValuePair<Unit_Gateway, UnitAssignmentContext> pair in dictionary)
                    AddReservation(requester, pair.Key, pair.Value);

                return;
            }

            foreach (KeyValuePair<Unit_Gateway, UnitAssignmentContext> pair in assignments)
                AddReservation(requester, pair.Key, pair.Value);
        }


        private void AddReservation(
            Unit_Gateway requester,
            Unit_Gateway unit,
            UnitAssignmentContext context)
        {
            if (unit == null || unit == requester || !unit.IsTargetable || !context.HasPosition)
                return;

            _reservations.Add(new PositionReservation
            {
                Position = context.AssignedPosition.Value,
                Spacing = _bodyRadius + unit.BodyRadius
            });
        }


        // =========================
        // Position Validation
        // =========================

        private bool IsPositionAvailable(
            Unit_Gateway requester,
            Vector2 position)
        {
            for (int i = 0; i < _reservations.Count; i++)
            {
                PositionReservation reservation = _reservations[i];

                if ((position - reservation.Position).sqrMagnitude
                    < reservation.Spacing * reservation.Spacing)
                {
                    return false;
                }
            }

            int count = Physics2D.OverlapCircle(
                position,
                Mathf.Max(0.01f, _bodyRadius - 0.02f),
                _contactFilter,
                _occupants
            );

            // 버퍼가 가득 찼다면 누락된 몸체가 있을 수 있으므로 빈자리로 보지 않는다.
            if (count == _occupants.Length)
                return false;

            for (int i = 0; i < count; i++)
            {
                Unit_Gateway unit = _occupants[i].GetComponentInParent<Unit_Gateway>();

                if (unit == requester || (unit != null && !unit.IsTargetable))
                    continue;

                return false;
            }

            return true;
        }


        // =========================
        // Path Evaluation
        // =========================

        private float PathCongestion(
            Unit_Gateway requester,
            Vector2 destination)
        {
            Vector2 origin = requester.transform.position;

            Vector2 delta = destination - origin;

            if (delta.sqrMagnitude < 0.0001f)
                return 0f;

            int count = Physics2D.CircleCast(
                origin,
                _bodyRadius * 0.9f,
                delta.normalized,
                _contactFilter,
                _pathHits,
                delta.magnitude
            );

            float cost = count == _pathHits.Length ? 6f : 0f;

            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = _pathHits[i];

                Unit_Gateway unit = hit.collider.GetComponentInParent<Unit_Gateway>();

                if (unit == requester || (unit != null && !unit.IsTargetable))
                    continue;

                if (hit.distance <= 0.01f)
                    continue;

                cost += unit != null ? 2f : 6f;
            }

            return cost;
        }


        // =========================
        // Utility
        // =========================

        private Vector2 Rotate(
            Vector2 direction,
            float angle)
        {
            float radian = angle * Mathf.Deg2Rad;

            float cos = Mathf.Cos(radian);

            float sin = Mathf.Sin(radian);

            return new Vector2(
                direction.x * cos - direction.y * sin,
                direction.x * sin + direction.y * cos
            );
        }
    }
}
