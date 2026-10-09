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
            _core = core;

            _data = data;
        }


        // ============================================================
        // Select
        // ============================================================

        // 신규 공통 요청 경로. 기존 단일 스킬의 SelectTarget 및 동률 의미는 그대로 둔다.
        public static SkillTargetResult SelectTargets(
            SkillTargetRequest request,
            IReadOnlyList<ICombatTarget> candidates)
        {
            var settings = request.Settings;

            if (settings == null || !request.CanSelect)
                return new SkillTargetResult(
                    null,
                    request.Origin,
                    request.Direction,
                    false,
                    "Invalid owner/settings"
                );

            if (settings.Source == SkillTargetSource.None)
                return new SkillTargetResult(
                    null,
                    request.Origin,
                    request.Direction,
                    true
                );

            var accepted = new List<ICombatTarget>();

            var lifetimes = new Dictionary<ICombatTarget, CombatTargetSnapshot>();

            // 외부 필터가 중첩 조회해도 입력 목록이 바뀌지 않는다.
            var input = candidates == null ? new List<ICombatTarget>() : new List<ICombatTarget>(candidates);

            foreach (var target in input)
            {
                if (!CombatTargetUtility.IsValid(target) || accepted.Contains(target))
                    continue;

                var lifetime = new CombatTargetSnapshot(target);

                bool self = ReferenceEquals(target, request.Owner.Target);

                bool relation = settings.Relation == SkillTargetRelation.Self ? self : settings.Relation == SkillTargetRelation.Friendly ? target.Team == request.Owner.Team && (settings.IncludeSelf || !self) : target.Team != request.Owner.Team;

                if (!relation)
                    continue;

                if (settings.Relation == SkillTargetRelation.Hostile && request.HostileFilter != null && !request.HostileFilter(target))
                    continue;

                if (!lifetime.IsTargetable)
                    continue;

                bool valid = true;

                foreach (var filter in settings.Filters)
                {
                    if (filter == null)
                        continue;

                    float value = GetMetric(
                        target,
                        request.Origin,
                        filter.Metric
                    );

                    float expected = filter.Value;

                    valid &= filter.Comparison switch
                    {
                        SkillTargetComparison.Less => value < expected,
                        SkillTargetComparison.LessOrEqual => value <= expected,
                        SkillTargetComparison.Greater => value > expected,
                        SkillTargetComparison.GreaterOrEqual => value >= expected,
                        SkillTargetComparison.Equal => Mathf.Abs(value - expected) <= 0.0001f,
                        _ => false
                    };
                }

                if (!valid)
                    continue;

                if (request.Area != null && request.Area.Shape != SkillAreaShape.Single)
                {
                    Vector2 offset = (Vector2)target.Transform.position - request.Origin;

                    if (offset.sqrMagnitude > request.Area.Radius * request.Area.Radius)
                        continue;

                    if (request.Area.Shape == SkillAreaShape.Cone && (request.Direction.sqrMagnitude <= 0f || (offset.sqrMagnitude > 0f && Vector2.Angle(request.Direction, offset) > request.Area.Angle * 0.5f)))
                        continue;
                }

                accepted.Add(target);

                lifetimes[target] = lifetime;
            }

            if (!request.CanSelect)
                return new SkillTargetResult(
                    null,
                    request.Origin,
                    request.Direction,
                    false,
                    "Owner lifetime changed"
                );

            accepted.RemoveAll(target => !lifetimes[target].IsTargetable);

            // Cluster 점수는 정렬 도중 바뀌지 않는 필터 통과 후보 집합으로 계산한다.
            var scores = new Dictionary<ICombatTarget, double[]>();

            foreach (var target in accepted)
            {
                var values = new double[settings.Priorities.Count];

                for (int i = 0; i < values.Length; i++)
                {
                    var priority = settings.Priorities[i];

                    if (priority == null)
                        continue;

                    switch (priority.Policy)
                    {
                        case SkillTargetPolicy.Current:
                            values[i] = request.CurrentTarget.IsTargetable && ReferenceEquals(target, request.CurrentTarget.Target) ? 0 : 1;

                            break;

                        case SkillTargetPolicy.Nearest:
                            values[i] = GetMetric(
                                target,
                                request.Origin,
                                SkillTargetMetric.Distance
                            );

                            break;

                        case SkillTargetPolicy.Farthest:
                            values[i] = -GetMetric(
                                target,
                                request.Origin,
                                SkillTargetMetric.Distance
                            );

                            break;

                        case SkillTargetPolicy.LowestHP:
                            values[i] = GetMetric(
                                target,
                                request.Origin,
                                SkillTargetMetric.HpRatio
                            );

                            break;

                        case SkillTargetPolicy.HighestHP:
                            values[i] = -GetMetric(
                                target,
                                request.Origin,
                                SkillTargetMetric.HpRatio
                            );

                            break;

                        case SkillTargetPolicy.Cluster:
                            foreach (var candidate in accepted)
                                if (((Vector2)candidate.Transform.position - (Vector2)target.Transform.position).sqrMagnitude <= settings.ClusterRadius * settings.ClusterRadius)
                                    values[i]--;

                            break;
                    }

                    // 일정 폭 양자화로 동률을 정의하여 근사 비교의 비추이성을 피한다.
                    values[i] = System.Math.Round(values[i] / 0.0001d);
                }

                scores[target] = values;
            }

            accepted.Sort((
                a,
                b) =>
            {
                for (int i = 0; i < settings.Priorities.Count; i++)
                {
                    int comparison = scores[a][i].CompareTo(scores[b][i]);

                    if (comparison != 0)
                        return comparison;
                }

                return a.Transform.GetInstanceID().CompareTo(b.Transform.GetInstanceID());
            });

            if (accepted.Count > settings.MaxTargetCount)
                accepted.RemoveRange(settings.MaxTargetCount, accepted.Count - settings.MaxTargetCount);

            return new SkillTargetResult(
                accepted,
                request.Origin,
                request.Direction,
                settings.Source == SkillTargetSource.None
            );
        }

        private static float GetMetric(
            ICombatTarget target,
            Vector2 origin,
            SkillTargetMetric metric)
        {
            float maxHp = target.RuntimeStatus != null ? target.RuntimeStatus.MaxHp : 0f;

            float ratio = maxHp > 0f ? Mathf.Clamp01(target.CurrentHp / maxHp) : 0f;

            return metric switch
            {
                SkillTargetMetric.Distance => Vector2.Distance(origin, target.Transform.position),
                SkillTargetMetric.CurrentHp => target.CurrentHp,
                SkillTargetMetric.HpRatio => ratio,
                SkillTargetMetric.MissingHpRatio => 1f - ratio,
                SkillTargetMetric.Shield => target.CurrentShield,
                SkillTargetMetric.Defense => target.RuntimeStatus != null ? target.RuntimeStatus.Defense : 0f,
                _ => 0f
            };
        }

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
                return Contains(candidates, currentTarget) && IsValidCandidate(currentTarget) ? currentTarget : null;
            }

            if (candidates == null || candidates.Count <= 0)
                return null;

            switch (_data.TargetPolicy)
            {
                case SkillTargetPolicy.Nearest:
                    return SelectNearest(candidates);

                case SkillTargetPolicy.Farthest:
                    return SelectFarthest(candidates);

                case SkillTargetPolicy.LowestHP:
                    return SelectLowestHP(candidates);

                case SkillTargetPolicy.HighestHP:
                    return SelectHighestHP(candidates);

                case SkillTargetPolicy.Cluster:
                    return SelectCluster(candidates);
            }

            return null;
        }


        // ============================================================
        // Candidate
        // ============================================================

        private static bool Contains(
            IReadOnlyList<ICombatTarget> candidates,
            ICombatTarget target)
        {
            if (candidates == null)
                return false;

            for (int i = 0; i < candidates.Count; i++)
                if (ReferenceEquals(candidates[i], target))
                    return true;

            return false;
        }

        internal bool IsValidCandidate(ICombatTarget target)
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
                    return target.Team != _core.Team;

                case SkillTargetRelation.Friendly:
                    if (target.Team != _core.Team) return false;
                    // 초기 선택도 첫 Action의 자신 제외 규칙을 따라야 한다.
                    // 선택 이후 제외하면 Nearest/LowestHP가 자신에게 고정될 수 있다.
                    if (!_data.UsesLegacyTargetSelection && _data.Actions.Count > 0)
                    {
                        var settings = _data.Actions[0]?.Target;
                        if (settings != null && settings.Relation == SkillTargetRelation.Friendly
                            && !settings.IncludeSelf && ReferenceEquals(target, _core.CombatTarget))
                            return false;
                    }
                    return true;

                case SkillTargetRelation.Self:
                    return target.Transform == _core.transform;
            }

            return false;
        }


        // ============================================================
        // Self
        // ============================================================

        private ICombatTarget GetSelfTarget()
        {
            ICombatTarget selfTarget = _core.GetComponent<Unit_Gateway>();

            if (!IsValidCandidate(selfTarget))
            {
                return null;
            }

            return selfTarget;
        }


        // ============================================================
        // Distance
        // ============================================================

        private ICombatTarget SelectNearest(IReadOnlyList<ICombatTarget> candidates)
        {
            ICombatTarget selectedTarget = null;

            float selectedDistanceSqr = float.MaxValue;

            Vector2 origin = _core.transform.position;

            for (int i = 0; i < candidates.Count; i++)
            {
                ICombatTarget candidate = candidates[i];

                if (!IsValidCandidate(candidate))
                {
                    continue;
                }

                float distanceSqr = ((Vector2)candidate.Transform.position - origin).sqrMagnitude;

                if (distanceSqr >= selectedDistanceSqr)
                    continue;

                selectedDistanceSqr = distanceSqr;

                selectedTarget = candidate;
            }

            return selectedTarget;
        }

        private ICombatTarget SelectFarthest(IReadOnlyList<ICombatTarget> candidates)
        {
            ICombatTarget selectedTarget = null;

            float selectedDistanceSqr = -1f;

            Vector2 origin = _core.transform.position;

            for (int i = 0; i < candidates.Count; i++)
            {
                ICombatTarget candidate = candidates[i];

                if (!IsValidCandidate(candidate))
                {
                    continue;
                }

                float distanceSqr = ((Vector2)candidate.Transform.position - origin).sqrMagnitude;

                if (distanceSqr <= selectedDistanceSqr)
                    continue;

                selectedDistanceSqr = distanceSqr;

                selectedTarget = candidate;
            }

            return selectedTarget;
        }


        // ============================================================
        // HP
        // ============================================================

        private ICombatTarget SelectLowestHP(IReadOnlyList<ICombatTarget> candidates)
        {
            ICombatTarget selectedTarget = null;

            float selectedHpRatio = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                ICombatTarget candidate = candidates[i];

                if (!IsValidCandidate(candidate))
                {
                    continue;
                }

                float hpRatio = GetHpRatio(candidate);

                if (hpRatio >= selectedHpRatio)
                    continue;

                selectedHpRatio = hpRatio;

                selectedTarget = candidate;
            }

            return selectedTarget;
        }

        private ICombatTarget SelectHighestHP(IReadOnlyList<ICombatTarget> candidates)
        {
            ICombatTarget selectedTarget = null;

            float selectedHpRatio = -1f;

            for (int i = 0; i < candidates.Count; i++)
            {
                ICombatTarget candidate = candidates[i];

                if (!IsValidCandidate(candidate))
                {
                    continue;
                }

                float hpRatio = GetHpRatio(candidate);

                if (hpRatio <= selectedHpRatio)
                    continue;

                selectedHpRatio = hpRatio;

                selectedTarget = candidate;
            }

            return selectedTarget;
        }

        private float GetHpRatio(ICombatTarget target)
        {
            Unit_Gateway gateway = target as Unit_Gateway;

            if (gateway == null)
                return 1f;

            Unit_RuntimeStatus runtimeStatus = gateway.RuntimeStatus;

            if (runtimeStatus == null)
                return 1f;

            float maxHp = runtimeStatus.MaxHp;

            if (maxHp <= 0f)
                return 0f;

            return Mathf.Clamp01(gateway.CurrentHp / maxHp);
        }


        // ============================================================
        // Cluster
        // ============================================================

        private ICombatTarget SelectCluster(IReadOnlyList<ICombatTarget> candidates)
        {
            ICombatTarget selectedTarget = null;

            int selectedCount = -1;

            float radiusSqr = _data.AreaRadius * _data.AreaRadius;

            for (int i = 0; i < candidates.Count; i++)
            {
                ICombatTarget centerTarget = candidates[i];

                if (!IsValidCandidate(centerTarget))
                {
                    continue;
                }

                Vector2 center = centerTarget.Transform.position;

                int count = 0;

                for (int j = 0; j < candidates.Count; j++)
                {
                    ICombatTarget candidate = candidates[j];

                    if (!IsValidCandidate(candidate))
                    {
                        continue;
                    }

                    float distanceSqr = ((Vector2)candidate.Transform.position - center).sqrMagnitude;

                    if (distanceSqr <= radiusSqr)
                        count++;
                }

                if (count <= selectedCount)
                    continue;

                selectedCount = count;

                selectedTarget = centerTarget;
            }

            return selectedTarget;
        }
    }
}