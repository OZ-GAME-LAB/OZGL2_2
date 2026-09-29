using System;
using Units.Skills;
using UnityEngine;

namespace Units
{
    public enum ProjectileImpactType
    {
        Single,
        Circle,
        Cone
    }


    public readonly struct ProjectileRequest
    {
        // ============================================================
        // Properties
        // ============================================================

        public ICombatTarget Attacker { get; }

        public CombatSourceSnapshot SourceSnapshot { get; }

        public ProjectileFlightState Flight { get; }

        public ICombatTarget Target { get; }

        public UnitTeam TargetTeam { get; }

        public Vector2 Origin { get; }

        public float ProjectileSpeed { get; }

        public ProjectileImpactType ImpactType { get; }

        public float AreaRadius { get; }

        public float AreaAngle { get; }

        public int MaxImpactTargetCount { get; }

        public Predicate<ICombatTarget> TargetFilter { get; }

        public int AttackerLifetimeVersion { get; }


        // 실제 명중 대상은 Projectile 충돌 시점에 확정한다.
        // 발사 시점에는 Target이 비어 있는 Request를 저장한다.
        public DamageRequest? DamageRequest { get; }

        public SkillEffectRequest? SkillEffectRequest { get; }


        // ============================================================
        // Constructor
        // ============================================================

        public ProjectileRequest(
            ICombatTarget attacker,
            ICombatTarget target,
            Vector2 origin,
            float projectileSpeed,
            ProjectileImpactType impactType,
            float areaRadius,
            float areaAngle,
            int maxImpactTargetCount,
            DamageRequest damageRequest,
            Predicate<ICombatTarget> targetFilter = null,
            ProjectileFlightState flight = null)
        {
            SourceSnapshot = new CombatSourceSnapshot(attacker);

            Flight = flight ?? new ProjectileFlightState();

            Attacker = attacker;

            Target = target;

            TargetTeam = target != null ? target.Team : default;

            Origin = origin;

            ProjectileSpeed = projectileSpeed;

            ImpactType = impactType;

            AreaRadius = Mathf.Max(0f, areaRadius);

            AreaAngle = Mathf.Clamp(
                areaAngle,
                0f,
                360f
            );

            MaxImpactTargetCount = Mathf.Max(1, maxImpactTargetCount);

            TargetFilter = targetFilter;

            DamageRequest = new DamageRequest(
                attacker,
                damageRequest.Targets,
                damageRequest.SourceType,
                damageRequest.DamageType,
                damageRequest.DamageMultiplier,
                damageRequest.Metadata,
                SourceSnapshot
            );

            SkillEffectRequest = null;

            AttackerLifetimeVersion = attacker != null ? attacker.LifetimeVersion : 0;
        }

        public ProjectileRequest(
            ICombatTarget attacker,
            ICombatTarget target,
            Vector2 origin,
            float projectileSpeed,
            ProjectileImpactType impactType,
            float areaRadius,
            float areaAngle,
            int maxImpactTargetCount,
            SkillEffectRequest skillEffectRequest,
            Predicate<ICombatTarget> targetFilter = null,
            ProjectileFlightState flight = null)
        {
            SourceSnapshot = new CombatSourceSnapshot(attacker);

            Flight = flight ?? new ProjectileFlightState();

            Attacker = attacker;

            Target = target;

            TargetTeam = target != null ? target.Team : default;

            Origin = origin;

            ProjectileSpeed = projectileSpeed;

            ImpactType = impactType;

            AreaRadius = Mathf.Max(0f, areaRadius);

            AreaAngle = Mathf.Clamp(
                areaAngle,
                0f,
                360f
            );

            MaxImpactTargetCount = Mathf.Max(1, maxImpactTargetCount);

            TargetFilter = targetFilter;

            DamageRequest = null;

            var context = new SkillConditionContext(
                attacker,
                new CombatTargetSnapshot(target),
                new TargetResolver(),
                skillEffectRequest.Metadata,
                skillEffectRequest.Batch?.IsActiveSkill ?? false,
                skillEffectRequest.Metadata.ActionIndex,
                skillEffectRequest.Batch?.PreviousResult,
                origin
            );

            SkillEffectRequest = new SkillEffectRequest(
                attacker,
                null,
                SkillDefinitionCopy.Copy(new System.Collections.Generic.List<SkillEffectData>(skillEffectRequest.Effects)),
                skillEffectRequest.Metadata,
                skillEffectRequest.Batch?.Freeze(context, SourceSnapshot),
                sourceSnapshot: SourceSnapshot
            );

            Flight.Configure(SkillEffectRequest.Value.Batch, skillEffectRequest.Metadata);

            AttackerLifetimeVersion = attacker != null ? attacker.LifetimeVersion : 0;
        }
    }
}