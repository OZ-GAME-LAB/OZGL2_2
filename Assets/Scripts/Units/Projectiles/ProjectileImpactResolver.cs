using System.Collections.Generic;
using Units.Skills;
using UnityEngine;

namespace Units
{
    public class ProjectileImpactResolver
    {
        // ============================================================
        // Reference
        // ============================================================

        private readonly TargetResolver _targetResolver;

        private readonly List<ICombatTarget> _singleTarget = new List<ICombatTarget>(1);


        // ============================================================
        // Constructor
        // ============================================================

        public ProjectileImpactResolver()
        {
            _targetResolver = new TargetResolver();
        }


        // ============================================================
        // Impact
        // ============================================================

        public void Resolve(
            ProjectileRequest request,
            ICombatTarget collisionTarget,
            Vector2 position,
            Vector2 direction)
        {
            if (request.Attacker == null)
                return;

            // 공격자 수명이 끝나도 발사 시 스냅샷으로 적용한다. 이벤트 전달은 원래 수명에서만 허용한다.
            IReadOnlyList<ICombatTarget> targets;

            if (request.ImpactType == ProjectileImpactType.Single)
            {
                if (!CombatTargetUtility.IsValid(collisionTarget) || (request.TargetFilter != null && !request.TargetFilter(collisionTarget)))
                    return;

                _singleTarget.Clear();

                _singleTarget.Add(collisionTarget);

                targets = _singleTarget.ToArray();
            }
            else
            {
                targets = _targetResolver.ResolveHitTargets(new TargetHitRequest(position, direction, request.AreaRadius, request.AreaAngle, request.MaxImpactTargetCount, request.ImpactType == ProjectileImpactType.Cone ? HitAreaType.Cone : HitAreaType.Circle, request.TargetTeam, request.TargetFilter));
            }

            if (request.DamageRequest.HasValue)
            {
                ResolveDamage(
                    request.DamageRequest.Value,
                    targets,
                    request.Flight
                );

                return;
            }

            if (request.SkillEffectRequest.HasValue)
            {
                ResolveSkillEffects(
                    request.SkillEffectRequest.Value,
                    targets,
                    request.Flight
                );
            }
        }


        // ============================================================
        // Damage
        // ============================================================

        private void ResolveDamage(
            DamageRequest pendingRequest,
            IReadOnlyList<ICombatTarget> targets,
            ProjectileFlightState flight)
        {
            if (DamageResolver.Instance == null)
            {
                Debug.LogError("[ProjectileImpactResolver] DamageResolver가 존재하지 않습니다.");

                return;
            }

            var unique = new List<ICombatTarget>();

            foreach (var target in targets)
            {
                var snapshot = new CombatTargetSnapshot(target);

                if (snapshot.IsTargetable && flight.Enter(snapshot))
                    unique.Add(target);
            }

            if (unique.Count == 0)
                return;

            foreach (var result in DamageResolver.Instance.ResolveWithResults(new DamageRequest(pendingRequest.Attacker, unique, pendingRequest.SourceType, pendingRequest.DamageType, pendingRequest.DamageMultiplier, pendingRequest.Metadata, pendingRequest.SourceSnapshot)))
            {
                flight.Record(result);
                if (result.Status != CombatApplicationStatus.Invalid)
                    flight.NotifyFX(SkillFXHook.OnHit, result.Target.Position, result.Target);
            }
        }


        // ============================================================
        // Skill Effect
        // ============================================================

        private void ResolveSkillEffects(
            SkillEffectRequest pendingRequest,
            IReadOnlyList<ICombatTarget> targets,
            ProjectileFlightState flight)
        {
            if (SkillEffectResolver.Instance == null)
            {
                Debug.LogError("[ProjectileImpactResolver] SkillEffectResolver가 존재하지 않습니다.");

                return;
            }

            if (targets == null)
                return;

            var snapshots = new List<CombatTargetSnapshot>();

            foreach (var candidate in targets)
                snapshots.Add(new CombatTargetSnapshot(candidate));

            for (int i = 0; i < snapshots.Count; i++)
            {
                ICombatTarget target = snapshots[i].Target;

                if (!snapshots[i].IsTargetable || !flight.Enter(snapshots[i]))
                    continue;

                if (pendingRequest.Batch != null)
                {
                    var context = new SkillConditionContext(
                        pendingRequest.Caster,
                        snapshots[i],
                        _targetResolver,
                        pendingRequest.Metadata,
                        pendingRequest.Batch.IsActiveSkill,
                        pendingRequest.Metadata.ActionIndex,
                        pendingRequest.Batch.PreviousResult,
                        target.Transform.position,
                        pendingRequest.SourceSnapshot
                    );

                    foreach (var result in SkillAttackDelivery.Hit(pendingRequest.Batch, context))
                        flight.Record(result);

                    pendingRequest.Batch.EventTemplate?.NotifyHit(
                        snapshots[i],
                        pendingRequest.Metadata,
                        context.Position
                    );

                    flight.NotifyFX(SkillFXHook.OnHit, context.Position, snapshots[i]);

                    continue;
                }

                foreach (var result in SkillEffectResolver.Instance.ResolveWithResults(new SkillEffectRequest(pendingRequest.Caster, target, pendingRequest.Effects, pendingRequest.Metadata, sourceSnapshot: pendingRequest.SourceSnapshot)))
                    flight.Record(result);
            }
        }
    }
}
