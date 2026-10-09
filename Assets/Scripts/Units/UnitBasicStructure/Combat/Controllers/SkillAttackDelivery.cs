using System;
using System.Collections.Generic;
using Units.Skills;
using UnityEngine;

// 액티브와 패시브가 같은 명중 효과 및 투사체 발사 경로를 사용하게 한다.
namespace Units
{
    // 액티브/패시브가 공유하는 명중 효과 경계. 피해 공식 및 대상 적용은 기존 Resolver에 맡긴다.
    public static class SkillAttackDelivery
    {

        // ============================================================
        // Execution
        // ============================================================

        public static IReadOnlyList<CombatApplicationResult> Hit(
            SkillEffectBatch batch,
            SkillConditionContext context) => SkillEffectPipeline.Resolve(
            batch,
            context,
            SkillEffectTiming.OnHit
        );

        public static bool Fire(
            ICombatTarget owner,
            ICombatTarget target,
            Vector2 origin,
            SkillAttackActionData action,
            SkillEffectBatch batch,
            CombatEventMetadata metadata,
            Predicate<ICombatTarget> filter = null,
            ProjectileFlightState flight = null)
        {
            var type = action.Area == ActiveSkillAreaType.Single ? ProjectileImpactType.Single : action.Area == ActiveSkillAreaType.SelfCone ? ProjectileImpactType.Cone : ProjectileImpactType.Circle;

            var pending = new SkillEffectRequest(
                owner,
                null,
                Array.Empty<SkillEffectData>(),
                metadata,
                batch
            );

            var request = new ProjectileRequest(
                owner,
                target,
                origin,
                action.ProjectileSpeed,
                type,
                action.Radius,
                action.Angle,
                type == ProjectileImpactType.Single ? 1 : action.MaxEffectTargets,
                pending,
                filter,
                flight,
                action.ProjectilePrefab
            );

            bool fired = ProjectileManager.GetOrCreate().Fire(request);

            if (!fired)
                request.Flight.Complete();

            return fired;
        }
    }
}
