using System.Collections.Generic;
using UnityEngine;



namespace Units
{
    public class AdvanceReferenceController
    {
        // =========================
        // Settings & Assignment State
        // =========================

        private const float VerticalWeight = 1.5f;

        private const float AssignmentWeight = 3f;

        private const float MaximumAssignmentPenalty = 6f;

        private const float SwitchImprovement = 0.75f;

        private const float MinimumOpeningFrontSeparation = 6f;

        private readonly Dictionary<Unit_GroupAI, int> _assignedMembers = new();

        // 첫 약진에서만 사용하는 상단/하단 집단. 교전 시작 후에는 기존 선택 규칙을 쓴다.
        private readonly Dictionary<Unit_GroupAI, int> _openingFronts = new();

        public void ClearOpeningAdvance()
        {
            foreach (var pair in _openingFronts)
                if (pair.Key != null) pair.Key.ClearOpeningAdvance();
            _openingFronts.Clear();
        }

        public void PrepareOpeningAdvance()
        {
            ClearOpeningAdvance();
            AssignOpeningFronts(_allyGroups);
            AssignOpeningFronts(_enemyGroups);

            var points = new Vector2[2];
            var available = new bool[2];
            for (int front = 0; front < 2; front++)
            {
                bool hasAllies = TryGetFrontCenter(_allyGroups, front, out Vector2 allies);
                bool hasEnemies = TryGetFrontCenter(_enemyGroups, front, out Vector2 enemies);
                available[front] = hasAllies && hasEnemies;
                points[front] = (allies + enemies) * 0.5f;
            }

            // 같은 높이로 배치된 편성도 두 약진 지점이 겹치지 않게 한다.
            if (available[0] && available[1]
                && points[0].y - points[1].y < MinimumOpeningFrontSeparation)
            {
                float middle = (points[0].y + points[1].y) * 0.5f;
                points[0].y = middle + MinimumOpeningFrontSeparation * 0.5f;
                points[1].y = middle - MinimumOpeningFrontSeparation * 0.5f;
            }

            foreach (var pair in _openingFronts)
                if (available[pair.Value]) pair.Key.SetOpeningAdvance(points[pair.Value]);
        }

        private void AssignOpeningFronts(IReadOnlyList<Unit_GroupAI> groups)
        {
            var ordered = new List<Unit_GroupAI>();
            foreach (var group in groups)
                if (IsValidCandidate(group)) ordered.Add(group);

            // 기존 소규모 편성은 유지한다. 높이 동률은 등록 순서를 보존한다.
            for (int i = 1; i < ordered.Count; i++)
            {
                var group = ordered[i];
                int j = i - 1;
                while (j >= 0 && ordered[j].CenterPosition.y < group.CenterPosition.y)
                {
                    ordered[j + 1] = ordered[j];
                    j--;
                }
                ordered[j + 1] = group;
            }
            int upperCount = (ordered.Count + 1) / 2;
            for (int i = 0; i < ordered.Count; i++)
                _openingFronts[ordered[i]] = i < upperCount ? 0 : 1;
        }

        private bool TryGetFrontCenter(IReadOnlyList<Unit_GroupAI> groups, int front, out Vector2 center)
        {
            center = Vector2.zero;
            int count = 0;
            foreach (var group in groups)
            {
                if (group == null || !_openingFronts.TryGetValue(group, out int assigned) || assigned != front)
                    continue;
                foreach (var member in group.Members)
                {
                    if (member == null || !member.IsTargetable) continue;
                    center += (Vector2)member.Transform.position;
                    count++;
                }
            }
            if (count > 0) center /= count;
            return count > 0;
        }


        // =========================
        // References
        // =========================

        private readonly IReadOnlyList<Unit_GroupAI>
            _allyGroups;

        private readonly IReadOnlyList<Unit_GroupAI>
            _enemyGroups;


        public AdvanceReferenceController(
            IReadOnlyList<Unit_GroupAI> allyGroups,
            IReadOnlyList<Unit_GroupAI> enemyGroups)
        {
            _allyGroups =
                allyGroups;

            _enemyGroups =
                enemyGroups;
        }


        public Unit_GroupAI SelectReference(
            Unit_GroupAI sourceGroup)
        {
            if (sourceGroup == null)
                return null;

            IReadOnlyList<Unit_GroupAI> candidates =
                GetCandidates(
                    sourceGroup.Team
                );

            if (candidates.Count == 0)
                return null;

            GatherAssignments(sourceGroup);

            return SelectBestReference(
                sourceGroup,
                candidates
            );
        }


        private IReadOnlyList<Unit_GroupAI> GetCandidates(
            UnitTeam sourceTeam)
        {
            return sourceTeam == UnitTeam.Ally
                ? _enemyGroups
                : _allyGroups;
        }


        private Unit_GroupAI SelectBestReference(
            Unit_GroupAI sourceGroup,
            IReadOnlyList<Unit_GroupAI> candidates)
        {
            Unit_GroupAI bestReference =
                null;

            float bestScore =
                float.MinValue;


            Vector2 sourceCenter = sourceGroup.CenterPosition;

            int openingFront = -1;
            if (sourceGroup.HasOpeningAdvance && _openingFronts.TryGetValue(sourceGroup, out int assignedFront))
            {
                foreach (var candidate in candidates)
                    if (IsValidCandidate(candidate) && _openingFronts.TryGetValue(candidate, out int front)
                        && front == assignedFront)
                    {
                        openingFront = assignedFront;
                        break;
                    }
                // 담당 집단이 전멸하면 오래된 집결점 대신 기존 합류 규칙을 사용한다.
                if (openingFront < 0) sourceGroup.ClearOpeningAdvance();
            }

            for (int i = 0;
                 i < candidates.Count;
                 i++)
            {
                Unit_GroupAI candidate =
                    candidates[i];

                if (!IsValidCandidate(
                    candidate))
                {
                    continue;
                }

                if (openingFront >= 0 && (!_openingFronts.TryGetValue(candidate, out int front)
                    || front != openingFront)) continue;

                float score =
                    CalculateScore(
                        sourceCenter,
                        candidate,
                        sourceGroup.AdvanceReference
                    );

                if (score <= bestScore)
                    continue;

                bestScore =
                    score;

                bestReference =
                    candidate;
            }

            return bestReference;
        }


        private bool IsValidCandidate(
            Unit_GroupAI candidate)
        {
            if (candidate == null)
                return false;

            foreach (var member in candidate.Members)
                if (member != null && member.IsTargetable) return true;
            return false;
        }


        // =========================
        // Front Selection
        // =========================

        private float CalculateScore(
            Vector2 sourceCenter,
            Unit_GroupAI candidate,
            Unit_GroupAI currentReference)
        {
            float approachCost = float.PositiveInfinity;

            int livingMembers = 0;

            for (int i = 0; i < candidate.Members.Count; i++)
            {
                Unit_Gateway member = candidate.Members[i];

                if (member == null || !member.IsTargetable)
                    continue;

                livingMembers++;

                Vector2 offset = (Vector2)member.Transform.position - sourceCenter;

                // 좌우로 싸우는 전장에서는 위아래 이동에 추가 비용을 준다.
                // 같은 높이의 전선을 우선하되 그곳이 비면 인접 전선도 지원한다.
                float cost = offset.magnitude + Mathf.Abs(offset.y) * VerticalWeight;

                approachCost = Mathf.Min(approachCost, cost);
            }

            if (livingMembers == 0)
                return float.NegativeInfinity;

            _assignedMembers.TryGetValue(candidate, out int assignedMembers);

            float assignmentCost = Mathf.Min(
                MaximumAssignmentPenalty,
                AssignmentWeight * assignedMembers / livingMembers
            );

            // 작은 배정 변화만으로 위아래 전진 목표를 계속 바꾸지 않는다.
            float retention = candidate == currentReference ? SwitchImprovement : 0f;

            return -(approachCost + assignmentCost) + retention;
        }


        // =========================
        // Existing Assignments
        // =========================

        private void GatherAssignments(Unit_GroupAI requester)
        {
            _assignedMembers.Clear();

            IReadOnlyList<Unit_GroupAI> groups = requester.Team == UnitTeam.Ally
                ? _allyGroups
                : _enemyGroups;

            for (int i = 0; i < groups.Count; i++)
            {
                Unit_GroupAI group = groups[i];

                if (group == null || group == requester)
                    continue;

                Unit_GroupAI reference = group.AdvanceReference;

                for (int j = 0; j < group.Members.Count; j++)
                {
                    Unit_Gateway member = group.Members[j];

                    if (member == null || !member.IsTargetable)
                        continue;

                    Unit_GroupAI destination = null;

                    if (group.State == GroupAIState.Advancing)
                    {
                        destination = reference;
                    }
                    else if (group.State == GroupAIState.Engaged
                        && group.AssignmentContexts.TryGetValue(member, out UnitAssignmentContext context)
                        && context.HasTarget && context.AssignedTarget is Unit_Gateway target)
                    {
                        destination = target.GroupAI;
                    }

                    if (destination == null)
                        continue;

                    _assignedMembers.TryGetValue(destination, out int count);
                    _assignedMembers[destination] = count + 1;
                }
            }
        }
    }
}
