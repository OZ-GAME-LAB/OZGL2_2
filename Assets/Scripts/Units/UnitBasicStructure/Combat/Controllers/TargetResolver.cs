using System.Collections.Generic;
using UnityEngine;



namespace Units
{
    public class TargetResolver
    {

        private const int DefaultBufferSize = 32;


        // ============================================================
        // Physics
        // ============================================================

        private readonly ContactFilter2D _contactFilter;


        // ============================================================
        // Runtime Buffer
        // ============================================================

        private Collider2D[] _colliderBuffer;

        private readonly List<ICombatTarget> _targets;


        // ============================================================
        // Constructor
        // ============================================================

        public TargetResolver(int bufferSize = DefaultBufferSize)
        {
            int finalBufferSize = Mathf.Max(1, bufferSize);

            _colliderBuffer = new Collider2D[finalBufferSize];

            _targets = new List<ICombatTarget>(finalBufferSize);

            _contactFilter = ContactFilter2D.noFilter;
        }


        // ============================================================
        // Resolve
        // ============================================================

        public SkillTargetResult ResolveSkillTargets(SkillTargetRequest request)
        {
            if (request.Settings == null || !request.CanSelect)
                return new SkillTargetResult(
                    null,
                    request.Origin,
                    request.Direction,
                    false,
                    "Invalid owner/settings"
                );

            var settings = request.Settings;

            var candidates = new List<ICombatTarget>();

            if (settings.Source == Units.Skills.SkillTargetSource.None)
                return new SkillTargetResult(
                    candidates,
                    request.Origin,
                    request.Direction,
                    true
                );

            if (settings.Source == Units.Skills.SkillTargetSource.Search)
            {
                UnitTeam team = settings.Relation == SkillTargetRelation.Hostile ? (request.Owner.Team == UnitTeam.Ally ? UnitTeam.Enemy : UnitTeam.Ally) : request.Owner.Team;

                candidates.AddRange(ResolveCandidates(new TargetCandidateRequest(request.Origin, settings.Range, team)));
            }
            else
            {
                CombatTargetSnapshot target = settings.Source switch
                {
                    Units.Skills.SkillTargetSource.Self => request.Owner,
                    Units.Skills.SkillTargetSource.InitialTarget => request.InitialTarget,
                    Units.Skills.SkillTargetSource.CurrentTarget => request.CurrentTarget,
                    Units.Skills.SkillTargetSource.PreviousTarget => request.PreviousTarget,
                    Units.Skills.SkillTargetSource.TriggerTarget => request.TriggerTarget,
                    _ => default
                };

                if (target.IsTargetable)
                    candidates.Add(target.Target);
            }

            return SkillTargetSelector.SelectTargets(request, candidates);
        }

        public IReadOnlyList<ICombatTarget> ResolveHitTargets(TargetHitRequest request)
        {
            _targets.Clear();

            if (request.MaxTargetCount <= 0)
                return _targets.ToArray();

            if (request.Radius <= 0f)
                return _targets.ToArray();

            switch (request.AreaType)
            {
                case HitAreaType.Circle:
                    ResolveCircle(request);

                    break;

                case HitAreaType.Cone:
                    ResolveCone(request);

                    break;
            }

            // 외부 필터가 같은 Resolver를 재진입해도 이번 조회의 목록은 독립적이다.
            var resolved = new List<ICombatTarget>(_targets);

            var lifetimes = new Dictionary<ICombatTarget, CombatTargetSnapshot>();

            foreach (var target in resolved)
                lifetimes[target] = new CombatTargetSnapshot(target);

            if (request.TargetFilter != null)
                resolved.RemoveAll(target => !request.TargetFilter(target));

            resolved.RemoveAll(target => !lifetimes[target].IsTargetable || !IsValidTarget(target, request.TargetTeam));

            SortByDistance(resolved, request.Origin);

            TrimTargetCount(resolved, request.MaxTargetCount);

            return resolved.ToArray();
        }


        // ============================================================
        // Candidate Resolve
        // ============================================================

        public IReadOnlyList<ICombatTarget> ResolveCandidates(TargetCandidateRequest request)
        {
            _targets.Clear();

            if (request.Range <= 0f)
                return _targets.ToArray();

            int hitCount = CollectColliders(request.Origin, request.Range);

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = _colliderBuffer[i];

                TryAddTarget(hit, request.TargetTeam);
            }

            // Collider 일부만 사거리에 걸친 대상은 위치 기준으로 다시 검사한다.
            float rangeSquared = request.Range * request.Range;

            _targets.RemoveAll(target => !IsValidTarget(target, request.TargetTeam) || ((Vector2)target.Transform.position - request.Origin).sqrMagnitude > rangeSquared);

            // 외부 필터가 같은 Resolver를 재진입해도 이번 조회의 목록은 독립적이다.
            var resolved = new List<ICombatTarget>(_targets);

            var lifetimes = new Dictionary<ICombatTarget, CombatTargetSnapshot>();

            foreach (var target in resolved)
                lifetimes[target] = new CombatTargetSnapshot(target);

            if (request.TargetFilter != null)
                resolved.RemoveAll(target => !request.TargetFilter(target));

            resolved.RemoveAll(target => !lifetimes[target].IsTargetable || !IsValidTarget(target, request.TargetTeam));

            SortByDistance(resolved, request.Origin);

            return resolved.ToArray();
        }


        // ============================================================
        // Physics Buffer
        // ============================================================

        private int CollectColliders(
            Vector2 origin,
            float radius)
        {
            while (true)
            {
                int count = Physics2D.OverlapCircle(
                    origin,
                    radius,
                    _contactFilter,
                    _colliderBuffer
                );

                if (count < _colliderBuffer.Length)
                    return count;

                // 밀집된 전장에서도 고정 버퍼 크기로 후보가 누락되지 않도록 확장한다.
                System.Array.Resize(ref _colliderBuffer, _colliderBuffer.Length * 2);
            }
        }


        // ============================================================
        // Circle
        // ============================================================

        private void ResolveCircle(TargetHitRequest request)
        {
            int hitCount = CollectColliders(request.Origin, request.Radius);

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = _colliderBuffer[i];

                TryAddTarget(hit, request.TargetTeam);
            }
        }


        // ============================================================
        // Cone
        // ============================================================

        private void ResolveCone(TargetHitRequest request)
        {
            if (request.Direction.sqrMagnitude <= 0f)
                return;

            int hitCount = CollectColliders(request.Origin, request.Radius);

            Vector2 direction = request.Direction.normalized;

            float halfAngle = request.Angle * 0.5f;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = _colliderBuffer[i];

                if (hit == null)
                    continue;

                ICombatTarget target = GetCombatTarget(hit);

                if (!IsValidTarget(target, request.TargetTeam))
                {
                    continue;
                }

                Vector2 targetDirection = (Vector2)target.Transform.position - request.Origin;

                if (targetDirection.sqrMagnitude <= 0f)
                    continue;

                float angle = Vector2.Angle(direction, targetDirection.normalized);

                if (angle > halfAngle)
                    continue;

                AddTarget(target);
            }
        }


        // ============================================================
        // Target Resolve
        // ============================================================

        private void TryAddTarget(
            Collider2D hit,
            UnitTeam targetTeam)
        {
            if (hit == null)
                return;

            ICombatTarget target = GetCombatTarget(hit);

            if (!IsValidTarget(target, targetTeam))
            {
                return;
            }

            AddTarget(target);
        }

        private ICombatTarget GetCombatTarget(Collider2D hit)
        {
            if (hit == null)
                return null;

            return hit.GetComponentInParent<ICombatTarget>();
        }


        // ============================================================
        // Validation
        // ============================================================

        private bool IsValidTarget(
            ICombatTarget target,
            UnitTeam targetTeam)
        {
            if (!CombatTargetUtility.IsValid(target))
                return false;

            if (!target.IsTargetable)
                return false;

            if (target.Transform == null)
                return false;

            if (target.Team != targetTeam)
                return false;

            return true;
        }


        // ============================================================
        // Target List
        // ============================================================

        private void AddTarget(ICombatTarget target)
        {
            if (_targets.Contains(target))
            {
                return;
            }

            _targets.Add(target);
        }

        private void SortByDistance(
            List<ICombatTarget> targets,
            Vector2 origin)
        {
            targets.Sort((
                a,
                b) =>
            {
                float distanceA = ((Vector2)a.Transform.position - origin).sqrMagnitude;

                float distanceB = ((Vector2)b.Transform.position - origin).sqrMagnitude;

                return distanceA.CompareTo(distanceB);
            });
        }

        private void TrimTargetCount(
            List<ICombatTarget> targets,
            int maxTargetCount)
        {
            if (targets.Count <= maxTargetCount)
                return;

            targets.RemoveRange(maxTargetCount, targets.Count - maxTargetCount);
        }
    }
}