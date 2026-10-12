using UnityEngine;

namespace Units
{
    // 기존 중심 사거리에서 일반 유닛보다 커진 몸의 증가분만 보정한다.
    public static class CombatRangeUtility
    {
        public const float StandardBodyRadius = 0.5f;
        public static float Expansion(float radius) => Mathf.Max(0f, radius - StandardBodyRadius);
        public static float Correction(float sourceRadius, float targetRadius)
            => Expansion(sourceRadius) + Expansion(targetRadius);
        public static float Correction(ICombatTarget source, ICombatTarget target)
            => Correction(source?.BodyRadius ?? 0f, target?.BodyRadius ?? 0f);
        public static float Distance(Vector2 origin, float sourceRadius, ICombatTarget target)
            => Mathf.Max(0f, Vector2.Distance(origin, target.Transform.position)
                - Correction(sourceRadius, target.BodyRadius));
        public static float Distance(ICombatTarget source, ICombatTarget target)
            => Distance(source.Transform.position, source.BodyRadius, target);
        public static float PreferredCenterDistance(ICombatTarget source, ICombatTarget target, float range)
            => Mathf.Max(source.BodyRadius + target.BodyRadius,
                Mathf.Max(0f, range) * 0.9f + Correction(source, target));
    }
}
