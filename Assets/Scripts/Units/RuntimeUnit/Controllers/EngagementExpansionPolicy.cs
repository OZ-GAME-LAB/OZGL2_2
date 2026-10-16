using System;
using System.Collections.Generic;
using UnityEngine;

namespace Units
{
    // 교전 상태를 변경하지 않는 확대 제한. 수치는 RuntimeUnitManager에서 조정한다.
    [Serializable]
    public sealed class EngagementExpansionPolicy
    {
        [SerializeField, Min(0f)] private float _maxJoinDistance = 6f;
        [SerializeField, Min(0f)] private float _maxCombinedExtent = 15f;
        [SerializeField, Min(2)] private int _maxCombinedGroups = 10;
        [SerializeField, Min(0f)] private float _closeContactGap = 1f;

        [SerializeField, Min(0f)]
        private float _combatContactMargin = 1f;


        // =========================
        // New Group Admission
        // =========================

        public bool HasCombatContact(
            Unit_GroupAI first,
            Unit_GroupAI second)
        {
            if (first == null || second == null || first.Team == second.Team)
                return false;

            // 신규 그룹은 기본 공격과 활성화된 적대 스킬의 개시 거리로 합류한다.
            // 돌진은 이동 거리가 아닌 스킬 사용 사거리부터 전투 AI에 맡긴다.
            for (int i = 0; i < first.Members.Count; i++)
            {
                Unit_Gateway source = first.Members[i];

                if (source == null || !source.IsTargetable)
                    continue;

                Vector2 origin = source.Transform.position;

                float sourceRadius = source.BodyRadius;

                float sourceRange =
                    source.EngagementEntryRange;

                for (int j = 0; j < second.Members.Count; j++)
                {
                    Unit_Gateway target = second.Members[j];

                    if (target == null || !target.IsTargetable)
                        continue;

                    float targetRadius = target.BodyRadius;

                    float targetRange =
                        target.EngagementEntryRange;

                    float contactDistance = Mathf.Max(
                        sourceRadius + targetRadius,
                        Mathf.Max(sourceRange, targetRange)
                            + CombatRangeUtility.Correction(sourceRadius, targetRadius)
                    ) + Mathf.Max(0f, _combatContactMargin);

                    if (((Vector2)target.Transform.position - origin).sqrMagnitude
                        <= contactDistance * contactDistance)
                    {
                        return true;
                    }
                }
            }

            return false;
        }


        // =========================
        // Engagement Expansion
        // =========================

        public bool Allows(
            IReadOnlyList<Unit_GroupAI> first,
            IReadOnlyList<Unit_GroupAI> second, bool closeContact = false)
        {
            if (first == null || second == null || first.Count == 0 || second.Count == 0)
                return false;

            var groups = new HashSet<Unit_GroupAI>();
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            if (!Include(first, groups, ref min, ref max)
                || !Include(second, groups, ref min, ref max))
                return false;

            if (groups.Count > Mathf.Max(2, _maxCombinedGroups)
                || (max - min).sqrMagnitude > Mathf.Max(0f, _maxCombinedExtent) * Mathf.Max(0f, _maxCombinedExtent))
                return false;

            if (closeContact) return true;

            float maxDistance = Mathf.Max(0f, _maxJoinDistance);
            for (int i = 0; i < first.Count; i++)
            {
                for (int j = 0; j < second.Count; j++)
                {
                    if ((first[i].CenterPosition - second[j].CenterPosition).sqrMagnitude
                        <= maxDistance * maxDistance)
                        return true;
                }
            }

            return false;
        }

        // Body-to-body distance, not the potentially distant group centers.
        public bool HasCloseContact(Unit_GroupAI first, Unit_GroupAI second)
        {
            if (first == null || second == null || first.Team == second.Team) return false;
            foreach (var a in first.Members)
                foreach (var b in second.Members)
                    if (a != null && b != null && a.IsTargetable && b.IsTargetable
                        && Vector2.Distance(a.Transform.position, b.Transform.position)
                            <= a.BodyRadius + b.BodyRadius + Mathf.Max(0f, _closeContactGap))
                        return true;
            return false;
        }

        private static bool Include(
            IReadOnlyList<Unit_GroupAI> source,
            HashSet<Unit_GroupAI> groups,
            ref Vector2 min,
            ref Vector2 max)
        {
            for (int i = 0; i < source.Count; i++)
            {
                Unit_GroupAI group = source[i];
                if (group == null || group.Members.Count == 0)
                    return false;

                Vector2 center = group.CenterPosition;
                if (float.IsNaN(center.x) || float.IsNaN(center.y)
                    || float.IsInfinity(center.x) || float.IsInfinity(center.y))
                    return false;

                if (!groups.Add(group))
                    continue;

                min = Vector2.Min(min, center);
                max = Vector2.Max(max, center);
            }

            return true;
        }
    }
}
