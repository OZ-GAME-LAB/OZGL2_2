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

        public ProjectileImpactResolver(
            Unit_Core attacker)
        {
            _targetResolver =
                new TargetResolver(
                    attacker
                );
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


            // 사망한 시전자의 투사체는 유지하되, 풀에서 재사용된 다른 생애의 공격으로 처리하지 않는다.
            Unit_Gateway attackerGateway = request.Attacker.GetComponent<Unit_Gateway>();
            if (attackerGateway != null && attackerGateway.LifetimeVersion != request.AttackerLifetimeVersion)
                return;


            IReadOnlyList<ICombatTarget> targets;


            if (request.ImpactType == ProjectileImpactType.Single)
            {
                if (!CombatTargetUtility.IsValid(collisionTarget)
                    || (request.TargetFilter != null && !request.TargetFilter(collisionTarget)))
                    return;


                _singleTarget.Clear();

                _singleTarget.Add(
                    collisionTarget
                );


                targets =
                    _singleTarget;
            }
            else
            {
                targets =
                    _targetResolver.ResolveHitTargets(
                        new TargetHitRequest(
                            position,
                            direction,
                            request.AreaRadius,
                            request.AreaAngle,
                            request.MaxImpactTargetCount,
                            request.ImpactType == ProjectileImpactType.Cone
                                ? HitAreaType.Cone
                                : HitAreaType.Circle,
                            request.TargetTeam,
                            request.TargetFilter
                        )
                    );
            }


            if (request.DamageRequest.HasValue)
            {
                ResolveDamage(
                    request.DamageRequest.Value,
                    targets
                );

                return;
            }


            if (request.SkillEffectRequest.HasValue)
            {
                ResolveSkillEffects(
                    request.SkillEffectRequest.Value,
                    targets
                );
            }
        }


        // ============================================================
        // Damage
        // ============================================================

        private void ResolveDamage(
            DamageRequest pendingRequest,
            IReadOnlyList<ICombatTarget> targets)
        {
            if (DamageResolver.Instance == null)
            {
                Debug.LogError(
                    "[ProjectileImpactResolver] DamageResolver가 존재하지 않습니다."
                );

                return;
            }


            DamageResolver.Instance.Resolve(
                new DamageRequest(
                    pendingRequest.Attacker,
                    targets,
                    pendingRequest.SourceType,
                    pendingRequest.DamageType,
                    pendingRequest.DamageMultiplier
                )
            );
        }


        // ============================================================
        // Skill Effect
        // ============================================================

        private void ResolveSkillEffects(
            SkillEffectRequest pendingRequest,
            IReadOnlyList<ICombatTarget> targets)
        {
            if (SkillEffectResolver.Instance == null)
            {
                Debug.LogError(
                    "[ProjectileImpactResolver] SkillEffectResolver가 존재하지 않습니다."
                );

                return;
            }


            if (targets == null)
                return;


            for (int i = 0; i < targets.Count; i++)
            {
                ICombatTarget target =
                    targets[i];

                if (!CombatTargetUtility.IsValid(target))
                    continue;


                SkillEffectResolver.Instance.Resolve(
                    new SkillEffectRequest(
                        pendingRequest.Caster,
                        target,
                        pendingRequest.Effects
                    )
                );
            }
        }
    }
}