using System.Collections.Generic;
using UnityEngine;



namespace Units
{
    public class TargetSelector
    {
        // =========================
        // Constants
        // =========================

        private const float CandidateDistanceRatio = 1.25f;

        private const float LocalDistanceAllowance = 0.75f;

        private const float LocalCombatDepth = 4f;

        private const int MaxDetailedCandidates = 4;

        private const int MaxApproachNeighbours = 16;

        private const float PressureWeight = 1.5f;

        private const float MinimumSwitchImprovement = 0.2f;


        // =========================
        // Candidate State
        // =========================

        private struct TargetCandidate
        {
            public ICombatTarget Target;

            public Vector2 Position;

            public float Distance;

            public float ApproachDistance;

            public float Cost;

            public float CachedApproachCost;
        }


        private struct ApproachNeighbour
        {
            public Unit_Gateway Unit;

            public Vector2 Position;

            public float SqrDistance;

            public float Radius;

            public bool SameTeam;
        }


        // =========================
        // References
        // =========================

        private CombatQueryContext _combatQueries;

        private readonly HashSet<Unit_GroupAI> _allowedGroups = new();

        private readonly List<Unit_Gateway> _nearby = new();



        private readonly List<Unit_GroupAI> _enemyGroups;

        private readonly Dictionary<Unit_Gateway, UnitAssignmentContext>
            _assignmentContexts;


        // =========================
        // Runtime Buffer
        // =========================

        private readonly Dictionary<ICombatTarget, int> _pressure = new();

        private readonly List<TargetCandidate> _candidates = new();

        private readonly HashSet<ICombatTarget> _localTargets = new();

        private readonly TargetCandidate[] _shortlist =
            new TargetCandidate[MaxDetailedCandidates];

        private readonly ApproachNeighbour[] _neighbours =
            new ApproachNeighbour[MaxApproachNeighbours];

        private int _shortlistCount;

        private int _enemyNeighbourCount;

        private int _allyNeighbourCount;

        private int _attackerCount;

        private Vector2 _origin;

        private float _bodyRadius;

        private float _localAreaRadiusSqr;

        private bool _recovering;


        // =========================
        // Constructor
        // =========================

        public TargetSelector(
            List<Unit_GroupAI> enemyGroups,
            Dictionary<Unit_Gateway, UnitAssignmentContext> assignmentContexts)
        {
            _enemyGroups = enemyGroups;

            _assignmentContexts = assignmentContexts;
        }


        // =========================
        // Shared Combat Queries
        // =========================

        public void SetCombatQueries(CombatQueryContext queries)
        {
            _combatQueries = queries;
            RefreshEnemyGroups();
        }

        public void RefreshEnemyGroups()
        {
            _allowedGroups.Clear();

            for (int i = 0; i < _enemyGroups.Count; i++)
                _allowedGroups.Add(_enemyGroups[i]);
        }



        // =========================
        // Target Assignment
        // =========================

        public bool TryAssignTarget(Unit_Gateway unit)
        {
            if (unit == null)
                return false;

            if (!_assignmentContexts.TryGetValue(
                unit,
                out UnitAssignmentContext context))
            {
                return false;
            }

            ICombatTarget target = SelectTarget(unit, context.AssignedTarget);

            if (target == null)
            {
                context.ClearTarget();

                return false;
            }

            context.SetTarget(target);

            return true;
        }


        public bool TryReevaluateTarget(Unit_Gateway unit)
        {
            if (unit == null)
                return false;

            if (!_assignmentContexts.TryGetValue(
                unit,
                out UnitAssignmentContext context))
            {
                return false;
            }

            if (!context.HasTarget)
                return false;

            ICombatTarget target =
                SelectTarget(unit, context.AssignedTarget);

            if (target == null
                || ReferenceEquals(target, context.AssignedTarget))
            {
                return false;
            }

            context.SetTarget(target);

            return true;
        }


        // =========================
        // Target Selection
        // =========================

        private ICombatTarget SelectTarget(
            Unit_Gateway unit,
            ICombatTarget currentTarget)
        {
            _candidates.Clear();
            _shortlistCount = 0;
            _enemyNeighbourCount = 0;
            _allyNeighbourCount = 0;

            // 한 번의 평가 안에서는 위치와 크기 스냅샷을 재사용한다.
            _origin = unit.Transform.position;
            _bodyRadius = unit.BodyRadius;

            float basicRange =
                unit.RuntimeStatus?.BasicAttackData?.BasicAttackRange ?? 0f;

            float range = basicRange > 0f
                ? basicRange
                : unit.PreferredCombatRange;

            _assignmentContexts.TryGetValue(unit, out UnitAssignmentContext recovery);
            recovery?.ObserveApproach(unit, range);
            _recovering = recovery != null && recovery.IsRecovering;
            // Commit to a reachable replacement while it makes progress.
            if (_recovering && IsValidTarget(currentTarget)
                && !recovery.IsTemporarilyBlocked(currentTarget)) return currentTarget;

            float nearestDistance = GatherCandidates(unit, range);

            RestrictLocalCandidates(range, nearestDistance);

            GatherPressure(unit);

            if (_candidates.Count == 0)
                return null;

            // 주변 전선의 집중도는 접근 비용을 보완하며 균등 배정을 강제하지 않는다.
            // 실제 접근 비용이 높으면 더 가까운 대상에게 집중할 수 있다.
            float expectedPressure = Mathf.Max(
                1f,
                (float)_attackerCount / _candidates.Count
            );

            float distanceLimit = Mathf.Max(
                nearestDistance * CandidateDistanceRatio,
                nearestDistance + LocalDistanceAllowance
            );

            if (_recovering) distanceLimit = range + 12f;

            bool waiting =
                _assignmentContexts.TryGetValue(unit, out UnitAssignmentContext context)
                && context.WaitingForPosition;

            bool hasCurrent = false;
            TargetCandidate current = default;

            for (int i = 0; i < _candidates.Count; i++)
            {
                TargetCandidate candidate = _candidates[i];

                candidate.Cost = CalculateBaseCost(candidate, expectedPressure, unit);
                // Include obstruction before shortlisting so clear flank targets survive.
                if (_recovering)
                {
                    candidate.CachedApproachCost = EstimateApproachCost(unit, candidate);
                    candidate.Cost += candidate.CachedApproachCost
                        + (recovery.IsTemporarilyBlocked(candidate.Target) ? 100f : 0f);
                }

                if (ReferenceEquals(candidate.Target, currentTarget))
                {
                    current = candidate;
                    hasCurrent = true;
                }

                _candidates[i] = candidate;
            }

            // 정상적으로 공격 중인 위치를 단순 분산 목적으로 포기하지 않는다.
            if (hasCurrent && !waiting && current.Distance <= range)
                distanceLimit = Mathf.Min(distanceLimit, range);

            for (int i = 0; i < _candidates.Count; i++)
            {
                TargetCandidate candidate = _candidates[i];

                if (candidate.Distance > distanceLimit
                    || ReferenceEquals(candidate.Target, currentTarget))
                {
                    continue;
                }

                AddShortlist(candidate);
            }

            ICombatTarget bestTarget = hasCurrent ? currentTarget : null;

            float currentCost = hasCurrent
                ? current.Cost + (_recovering ? current.CachedApproachCost : EstimateApproachCost(unit, current))
                : float.PositiveInfinity;

            if (hasCurrent && waiting)
                currentCost += 1f;

            float bestCost = currentCost;

            for (int i = 0; i < _shortlistCount; i++)
            {
                TargetCandidate candidate = _shortlist[i];

                float cost = candidate.Cost + (_recovering ? candidate.CachedApproachCost : EstimateApproachCost(unit, candidate));

                if (cost >= bestCost)
                    continue;

                bestCost = cost;
                bestTarget = candidate.Target;
            }

            // 작은 점수 차이로 매번 타겟을 바꾸지 않는다.
            // 현재 대상도 같은 비용식으로 비교하여 분산과 접근성을 함께 판단한다.
            if (hasCurrent && currentCost - bestCost < MinimumSwitchImprovement)
                return currentTarget;

            return bestTarget;
        }


        // =========================
        // Candidate Collection
        // =========================

        private float GatherCandidates(
            Unit_Gateway unit,
            float range)
        {
            if (_combatQueries != null)
                return GatherNearbyCandidates(unit, range);

            float nearestDistance = float.PositiveInfinity;

            for (int i = 0; i < _enemyGroups.Count; i++)
            {
                Unit_GroupAI group = _enemyGroups[i];

                if (group == null)
                    continue;

                for (int j = 0; j < group.Members.Count; j++)
                {
                    Unit_Gateway target = group.Members[j];

                    if (!IsValidTarget(target) || target.Team == unit.Team)
                        continue;

                    Vector2 position = target.Transform.position;

                    float radius = target.BodyRadius;

                    float distance = Mathf.Max(
                        0f,
                        Vector2.Distance(_origin, position)
                            - CombatRangeUtility.Correction(_bodyRadius, radius)
                    );

                    _candidates.Add(new TargetCandidate
                    {
                        Target = target,
                        Position = position,
                        Distance = distance,
                        ApproachDistance = Mathf.Max(0f, distance - range)
                    });

                    nearestDistance = Mathf.Min(nearestDistance, distance);

                    AddNeighbour(unit, target, position, radius);
                }
            }

            return nearestDistance;
        }


        private float GatherNearbyCandidates(Unit_Gateway unit, float range)
        {
            if (_allowedGroups.Count == 0)
                return float.PositiveInfinity;

            _combatQueries.CollectNearby(_origin, range + (_recovering ? 12f : LocalCombatDepth), _bodyRadius, _nearby);

            float nearestDistance = AddNearbyCandidates(unit, range);

            if (_candidates.Count > 0)
                return nearestDistance;

            Unit_Gateway nearest = _combatQueries.FindNearestEnemy(unit, _allowedGroups);

            if (nearest == null)
                return float.PositiveInfinity;

            float distance = CombatRangeUtility.Distance(unit, nearest);

            _combatQueries.CollectNearby(
                _origin, distance + LocalDistanceAllowance, _bodyRadius, _nearby
            );

            return AddNearbyCandidates(unit, range);
        }


        private float AddNearbyCandidates(Unit_Gateway unit, float range)
        {
            float nearestDistance = float.PositiveInfinity;

            for (int i = 0; i < _nearby.Count; i++)
            {
                Unit_Gateway target = _nearby[i];

                if (!IsValidTarget(target) || target.Team == unit.Team)
                    continue;

                Vector2 position = target.Transform.position;
                float radius = target.BodyRadius;

                // 다른 교전의 몸체도 접근 경로를 막을 수 있다.
                AddNeighbour(unit, target, position, radius);

                if (target.GroupAI == null || !_allowedGroups.Contains(target.GroupAI))
                    continue;

                float distance = Mathf.Max(0f, Vector2.Distance(_origin, position)
                    - CombatRangeUtility.Correction(_bodyRadius, radius));

                _candidates.Add(new TargetCandidate
                {
                    Target = target,
                    Position = position,
                    Distance = distance,
                    ApproachDistance = Mathf.Max(0f, distance - range)
                });

                nearestDistance = Mathf.Min(nearestDistance, distance);
            }

            return nearestDistance;
        }


        // =========================
        // Candidate Evaluation
        // =========================

        private void RestrictLocalCandidates(
            float range,
            float nearestDistance)
        {
            _localTargets.Clear();

            // Engagement는 참여 관계를 나타낸다. 합류로 멀리 있는 그룹이 늘어도
            // 개별 유닛의 후보와 분산 비율은 현재 전선 주변에서 계산한다.
            float radius = Mathf.Max(
                range + (_recovering ? 12f : LocalCombatDepth),
                nearestDistance + LocalDistanceAllowance
            );

            _localAreaRadiusSqr = radius * radius;

            int writeIndex = 0;

            for (int i = 0; i < _candidates.Count; i++)
            {
                TargetCandidate candidate = _candidates[i];

                if (candidate.Distance > radius)
                    continue;

                _candidates[writeIndex++] = candidate;
                if (_combatQueries == null)
                    _localTargets.Add(candidate.Target);
            }

            _candidates.RemoveRange(writeIndex, _candidates.Count - writeIndex);
        }


        private float CalculateBaseCost(
            TargetCandidate candidate,
            float expectedPressure,
            Unit_Gateway requester)
        {
            int pressure;

            if (_combatQueries != null)
                pressure = _combatQueries.GetPressure(candidate.Target, requester);
            else
                _pressure.TryGetValue(candidate.Target, out pressure);

            // 인원 비용은 이동 거리와 같은 점수 단위로 합산한다.
            // 비용에 상한을 두어 빈 타겟을 찾느라 적진 깊숙이 이동하지 않게 한다.
            float pressureCost = PressureWeight * Mathf.Min(
                3f,
                (pressure + 1f) / expectedPressure
            );

            return candidate.ApproachDistance
                + pressureCost
                + candidate.Distance * 0.02f;
        }


        private void AddShortlist(TargetCandidate candidate)
        {
            int index = _shortlistCount;

            if (index == MaxDetailedCandidates)
            {
                if (candidate.Cost >= _shortlist[index - 1].Cost)
                    return;

                index--;
            }
            else
            {
                _shortlistCount++;
            }

            while (index > 0 && candidate.Cost < _shortlist[index - 1].Cost)
            {
                _shortlist[index] = _shortlist[index - 1];
                index--;
            }

            _shortlist[index] = candidate;
        }


        // =========================
        // Approach Estimation
        // =========================

        private float EstimateApproachCost(
            Unit_Gateway unit,
            TargetCandidate candidate)
        {
            if (candidate.ApproachDistance <= 0.01f)
                return 0f;

            Vector2 origin = _origin;

            Vector2 direction = (candidate.Position - origin).normalized;

            float cost = 0f;

            // 실제 PositionAssigner나 Physics 쿼리를 호출하지 않는다.
            // 상위 4개 후보와 기존 대상만, 가까운 최대 16개 몸체와 비교한다.
            for (int i = 0; i < MaxApproachNeighbours; i++)
            {
                int half = MaxApproachNeighbours / 2;

                if ((i < half && i >= _enemyNeighbourCount)
                    || (i >= half && i - half >= _allyNeighbourCount))
                {
                    continue;
                }

                ApproachNeighbour neighbour = _neighbours[i];

                if (ReferenceEquals(neighbour.Unit, candidate.Target))
                    continue;

                Vector2 offset = neighbour.Position - origin;

                float projection = Vector2.Dot(offset, direction);

                if (projection <= 0f)
                    continue;

                float clearance = _bodyRadius + neighbour.Radius + 0.15f;

                float closest = Mathf.Min(projection, candidate.ApproachDistance);

                float lateralSqr = (offset - direction * closest).sqrMagnitude;

                if (lateralSqr >= clearance * clearance)
                    continue;

                cost += neighbour.SameTeam ? 0.5f : 3f;

                if (cost >= 6f)
                    return 6f;
            }

            return cost;
        }


        private void AddNeighbour(
            Unit_Gateway requester,
            Unit_Gateway neighbour,
            Vector2 position,
            float radius = -1f)
        {
            if (neighbour == requester)
                return;

            float sqrDistance =
                (position - _origin).sqrMagnitude;

            // 뒤에 밀집한 아군만 버퍼를 차지하여 전방 적을 놓치지 않도록
            // 적과 아군에 각각 8칸을 배정한다.
            bool sameTeam = neighbour.Team == requester.Team;

            int capacity = MaxApproachNeighbours / 2;

            int start = sameTeam ? capacity : 0;

            int count = sameTeam ? _allyNeighbourCount : _enemyNeighbourCount;

            int index = start + count;

            if (count == capacity)
            {
                if (sqrDistance >= _neighbours[index - 1].SqrDistance)
                    return;

                index--;
            }
            else
            {
                if (sameTeam)
                    _allyNeighbourCount++;
                else
                    _enemyNeighbourCount++;
            }

            while (index > start && sqrDistance < _neighbours[index - 1].SqrDistance)
            {
                _neighbours[index] = _neighbours[index - 1];
                index--;
            }

            _neighbours[index] = new ApproachNeighbour
            {
                Unit = neighbour,
                Position = position,
                SqrDistance = sqrDistance,
                Radius = radius >= 0f ? radius : neighbour.BodyRadius,
                SameTeam = sameTeam
            };
        }


        // =========================
        // Assignment Check
        // =========================

        private void GatherPressure(Unit_Gateway requester)
        {
            _pressure.Clear();
            _attackerCount = 0;

            if (_combatQueries != null)
            {
                GatherNearbyPressure(requester);

                return;
            }

            IReadOnlyList<Unit_GroupAI> groups = requester.GroupAI?.EngagementAllies;

            if (groups == null)
            {
                CountAssignments(requester, _assignmentContexts);

                return;
            }

            for (int i = 0; i < groups.Count; i++)
            {
                Unit_GroupAI group = groups[i];

                if (group == null || group.Team != requester.Team)
                    continue;

                CountAssignments(requester, group.AssignmentContexts);
            }
        }


        private void GatherNearbyPressure(Unit_Gateway requester)
        {
            // 후보 조회에서 얻은 주변 목록을 재사용한다. 먼 공격자의 배정은 대상별
            // 집중도에 반영하되, 현재 전선의 아군 수까지 부풀리지 않는다.
            for (int i = 0; i < _nearby.Count; i++)
            {
                Unit_Gateway member = _nearby[i];

                if (member.Team != requester.Team)
                    continue;

                Vector2 position = member.Transform.position;

                if ((position - _origin).sqrMagnitude <= _localAreaRadiusSqr)
                    _attackerCount++;

                AddNeighbour(requester, member, position);
            }
        }


        private void CountAssignments(
            Unit_Gateway requester,
            IReadOnlyDictionary<Unit_Gateway, UnitAssignmentContext> assignments)
        {
            // 실제 저장소의 구조체 열거자를 사용하여 반복 평가의 박싱 할당을 피한다.
            if (assignments is Dictionary<Unit_Gateway, UnitAssignmentContext> dictionary)
            {
                foreach (KeyValuePair<Unit_Gateway, UnitAssignmentContext> pair in dictionary)
                    CountAssignment(requester, pair.Key, pair.Value);

                return;
            }

            foreach (KeyValuePair<Unit_Gateway, UnitAssignmentContext> pair in assignments)
                CountAssignment(requester, pair.Key, pair.Value);
        }


        private void CountAssignment(
            Unit_Gateway requester,
            Unit_Gateway unit,
            UnitAssignmentContext context)
        {
            if (unit == null || !unit.IsTargetable)
                return;

            Vector2 position = unit.Transform.position;

            bool nearby = (position - _origin).sqrMagnitude <= _localAreaRadiusSqr;

            bool assignedLocally = context.HasTarget
                && _localTargets.Contains(context.AssignedTarget);

            if (!nearby && !assignedLocally)
                return;

            _attackerCount++;

            if (unit == requester)
                return;

            AddNeighbour(requester, unit, position);

            if (!assignedLocally)
                return;

            ICombatTarget target = context.AssignedTarget;

            _pressure.TryGetValue(target, out int count);

            _pressure[target] = count + 1;
        }


        // =========================
        // Target Validation
        // =========================

        private bool IsValidTarget(ICombatTarget target)
        {
            return target != null
                && target.IsTargetable
                && target.Transform != null;
        }
    }
}
