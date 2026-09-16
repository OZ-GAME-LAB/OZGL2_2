using System.Collections.Generic;
using UnityEngine;

namespace Units
{
    public class ProjectileImpactResolver
    {
        // ============================================================
        // Reference
        // ============================================================

        private readonly HitTargetResolver _hitTargetResolver;


        private readonly List<ICombatTarget> _singleTarget = new List<ICombatTarget>(1);


        // ============================================================
        // Constructor
        // ============================================================

        public ProjectileImpactResolver(
            Unit_Core attacker)
        {
            _hitTargetResolver = new HitTargetResolver(attacker);
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

            IReadOnlyList<ICombatTarget> targets;

            if (request.ImpactType == ProjectileImpactType.Single)
            {
                if (!CombatTargetUtility.IsValid(collisionTarget))
                    return;

                _singleTarget.Clear();

                _singleTarget.Add(collisionTarget);

                targets = _singleTarget;
            }
            else
            {
                targets = _hitTargetResolver.Resolve(new TargetHitRequest(position, direction, request.AreaRadius, request.AreaAngle, request.MaxDamageableCount, request.ImpactType == ProjectileImpactType.Cone ? HitAreaType.Cone : HitAreaType.Circle));
            }

            if (DamageResolver.Instance == null)
            {
                Debug.LogError("[ProjectileImpactResolver] DamageResolver가 존재하지 않습니다.");

                return;
            }

            DamageResolver.Instance.Resolve(new DamageRequest(request.Attacker, targets, request.DamageSourceType, request.DamageMultiplier));
        }
    }
}
