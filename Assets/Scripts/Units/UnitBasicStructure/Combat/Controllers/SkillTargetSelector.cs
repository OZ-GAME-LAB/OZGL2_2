using System.Collections.Generic;
using Units.Skills;
using UnityEngine;


namespace Units
{
    public class SkillTargetSelector
    {
        // ============================================================
        // Reference
        // ============================================================

        private readonly Unit_Core _core;



        // ============================================================
        // Data
        // ============================================================

        private readonly ActiveSkillData _data;


        // ============================================================
        // Constructor
        // ============================================================

        public SkillTargetSelector(
            Unit_Core core,
            ActiveSkillData data)
        {
            _core =
                core;

            _data =
                data;

        }


        // ============================================================
        // Select
        // ============================================================

        public ICombatTarget SelectTarget(
            ICombatTarget currentTarget,
            IReadOnlyList<ICombatTarget> candidates)
        {
            if (_core == null || _data == null)
                return null;


            if (_data.TargetSide == SkillTargetRelation.Self)
                return GetSelfTarget();


            if (_data.TargetPolicy == SkillTargetPolicy.Current)
            {
                return Contains(candidates, currentTarget) && IsValidCandidate(
                    currentTarget
                )
                    ? currentTarget
                    : null;
            }


            if (candidates == null || candidates.Count <= 0)
                return null;


            switch (_data.TargetPolicy)
            {
                case SkillTargetPolicy.Nearest:

                    return SelectNearest(
                        candidates
                    );


                case SkillTargetPolicy.Farthest:

                    return SelectFarthest(
                        candidates
                    );


                case SkillTargetPolicy.LowestHP:

                    return SelectLowestHP(
                        candidates
                    );


                case SkillTargetPolicy.HighestHP:

                    return SelectHighestHP(
                        candidates
                    );


                case SkillTargetPolicy.Cluster:

                    return SelectCluster(
                        candidates
                    );
            }


            return null;
        }


        // ============================================================
        // Candidate
        // ============================================================

        private static bool Contains(IReadOnlyList<ICombatTarget> candidates, ICombatTarget target)
        {
            if (candidates == null)
                return false;
            for (int i = 0; i < candidates.Count; i++)
                if (ReferenceEquals(candidates[i], target))
                    return true;
            return false;
        }


        private bool IsValidCandidate(
            ICombatTarget target)
        {
            if (!CombatTargetUtility.IsValid(target))
                return false;

            if (!target.IsTargetable)
                return false;

            if (target.Transform == null)
                return false;


            switch (_data.TargetSide)
            {
                case SkillTargetRelation.Hostile:

                    return target.Team
                        != _core.Team;


                case SkillTargetRelation.Friendly:

                    return target.Team
                        == _core.Team;


                case SkillTargetRelation.Self:

                    return target.Transform
                        == _core.transform;
            }


            return false;
        }


        // ============================================================
        // Self
        // ============================================================

        private ICombatTarget GetSelfTarget()
        {
            ICombatTarget selfTarget =
                _core.GetComponent<Unit_Gateway>();


            if (!IsValidCandidate(
                selfTarget
            ))
            {
                return null;
            }


            return selfTarget;
        }


        // ============================================================
        // Distance
        // ============================================================

        private ICombatTarget SelectNearest(
            IReadOnlyList<ICombatTarget> candidates)
        {
            ICombatTarget selectedTarget =
                null;

            float selectedDistanceSqr =
                float.MaxValue;


            Vector2 origin =
                _core.transform.position;


            for (int i = 0; i < candidates.Count; i++)
            {
                ICombatTarget candidate =
                    candidates[i];


                if (!IsValidCandidate(
                    candidate
                ))
                {
                    continue;
                }


                float distanceSqr =
                    (
                        (Vector2)candidate.Transform.position
                        - origin
                    ).sqrMagnitude;


                if (distanceSqr >= selectedDistanceSqr)
                    continue;


                selectedDistanceSqr =
                    distanceSqr;

                selectedTarget =
                    candidate;
            }


            return selectedTarget;
        }


        private ICombatTarget SelectFarthest(
            IReadOnlyList<ICombatTarget> candidates)
        {
            ICombatTarget selectedTarget =
                null;

            float selectedDistanceSqr =
                -1f;


            Vector2 origin =
                _core.transform.position;


            for (int i = 0; i < candidates.Count; i++)
            {
                ICombatTarget candidate =
                    candidates[i];


                if (!IsValidCandidate(
                    candidate
                ))
                {
                    continue;
                }


                float distanceSqr =
                    (
                        (Vector2)candidate.Transform.position
                        - origin
                    ).sqrMagnitude;


                if (distanceSqr <= selectedDistanceSqr)
                    continue;


                selectedDistanceSqr =
                    distanceSqr;

                selectedTarget =
                    candidate;
            }


            return selectedTarget;
        }


        // ============================================================
        // HP
        // ============================================================

        private ICombatTarget SelectLowestHP(
            IReadOnlyList<ICombatTarget> candidates)
        {
            ICombatTarget selectedTarget =
                null;

            float selectedHpRatio =
                float.MaxValue;


            for (int i = 0; i < candidates.Count; i++)
            {
                ICombatTarget candidate =
                    candidates[i];


                if (!IsValidCandidate(
                    candidate
                ))
                {
                    continue;
                }


                float hpRatio =
                    GetHpRatio(
                        candidate
                    );


                if (hpRatio >= selectedHpRatio)
                    continue;


                selectedHpRatio =
                    hpRatio;

                selectedTarget =
                    candidate;
            }


            return selectedTarget;
        }


        private ICombatTarget SelectHighestHP(
            IReadOnlyList<ICombatTarget> candidates)
        {
            ICombatTarget selectedTarget =
                null;

            float selectedHpRatio =
                -1f;


            for (int i = 0; i < candidates.Count; i++)
            {
                ICombatTarget candidate =
                    candidates[i];


                if (!IsValidCandidate(
                    candidate
                ))
                {
                    continue;
                }


                float hpRatio =
                    GetHpRatio(
                        candidate
                    );


                if (hpRatio <= selectedHpRatio)
                    continue;


                selectedHpRatio =
                    hpRatio;

                selectedTarget =
                    candidate;
            }


            return selectedTarget;
        }


        private float GetHpRatio(
            ICombatTarget target)
        {
            Unit_Gateway gateway =
                target as Unit_Gateway;


            if (gateway == null)
                return 1f;


            Unit_RuntimeStatus runtimeStatus =
                gateway.RuntimeStatus;


            if (runtimeStatus == null)
                return 1f;


            float maxHp =
                runtimeStatus.MaxHp;


            if (maxHp <= 0f)
                return 0f;


            return Mathf.Clamp01(
                gateway.CurrentHp
                / maxHp
            );
        }


        // ============================================================
        // Cluster
        // ============================================================

        private ICombatTarget SelectCluster(
            IReadOnlyList<ICombatTarget> candidates)
        {
            ICombatTarget selectedTarget =
                null;

            int selectedCount =
                -1;

            float radiusSqr =
                _data.AreaRadius
                * _data.AreaRadius;


            for (int i = 0; i < candidates.Count; i++)
            {
                ICombatTarget centerTarget =
                    candidates[i];


                if (!IsValidCandidate(
                    centerTarget
                ))
                {
                    continue;
                }


                Vector2 center =
                    centerTarget.Transform.position;

                int count =
                    0;


                for (int j = 0; j < candidates.Count; j++)
                {
                    ICombatTarget candidate =
                        candidates[j];


                    if (!IsValidCandidate(
                        candidate
                    ))
                    {
                        continue;
                    }


                    float distanceSqr =
                        (
                            (Vector2)candidate.Transform.position
                            - center
                        ).sqrMagnitude;


                    if (distanceSqr <= radiusSqr)
                        count++;
                }


                if (count <= selectedCount)
                    continue;


                selectedCount =
                    count;

                selectedTarget =
                    centerTarget;
            }


            return selectedTarget;
        }
    }
}