using System.Collections.Generic;



namespace Units
{
    public class AdvanceReferenceController
    {
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
                        sourceGroup,
                        candidate
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


        private float CalculateScore(
            Unit_GroupAI sourceGroup,
            Unit_GroupAI candidate)
        {
            // TODO:
            // 후열 우선
            // 엘리트 우선
            // Y축 거리
            // 기타 선호도 계산

            return 0f;
        }
    }
}