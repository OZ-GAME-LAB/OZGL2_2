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

        public bool Allows(
            IReadOnlyList<Unit_GroupAI> first,
            IReadOnlyList<Unit_GroupAI> second)
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
