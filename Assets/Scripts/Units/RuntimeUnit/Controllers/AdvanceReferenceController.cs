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

        private readonly Dictionary<Unit_GroupAI, int> _assignedMembers = new();


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

            if (candidate.Members.Count == 0)
                return false;

            return true;
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
