using System;
using UnityEngine;

namespace Units
{
    public enum HitAreaType
    {
        Circle,
        Cone
    }

    public readonly struct TargetHitRequest
    {
        public Vector2 Origin { get; }

        public Vector2 Direction { get; }

        public float Radius { get; }

        public float Angle { get; }

        public int MaxTargetCount { get; }

        public HitAreaType AreaType { get; }

        public UnitTeam TargetTeam { get; }

        public Predicate<ICombatTarget> TargetFilter { get; }


        public TargetHitRequest(
            Vector2 origin,
            Vector2 direction,
            float radius,
            float angle,
            int maxTargetCount,
            HitAreaType areaType,
            UnitTeam targetTeam,
            Predicate<ICombatTarget> targetFilter = null)
        {
            Origin = origin;

            Direction = direction;

            Radius = Mathf.Max(
                0f,
                radius
            );

            Angle = Mathf.Clamp(
                angle,
                0f,
                360f
            );

            MaxTargetCount = Mathf.Max(
                0,
                maxTargetCount
            );

            AreaType = areaType;

            TargetTeam = targetTeam;
            TargetFilter = targetFilter;
        }
    }
}