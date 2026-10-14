using System.Collections.Generic;
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
                            request.MaxDamageableCount,
                            request.ImpactType == ProjectileImpactType.Cone
                                ? HitAreaType.Cone
                                : HitAreaType.Circle,
                            request.TargetTeam,
                            request.TargetFilter
                        )
                    );
            }


            if (DamageResolver.Instance == null)
            {
                Debug.LogError(
                    "[ProjectileImpactResolver] DamageResolver가 존재하지 않습니다."
                );

                return;
            }


            DamageResolver.Instance.Resolve(
                new DamageRequest(
                    request.Attacker,
                    targets,
                    request.DamageSourceType,
                    request.DamageMultiplier
                )
            );
        }
    }
}