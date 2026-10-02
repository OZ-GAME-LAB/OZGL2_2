using System;
using UnityEngine;

namespace Units.Skills
{
    [Serializable]
    public sealed class PassiveDistanceConditionData : PassiveSkillConditionData
    {
        // ============================================================
        // Condition
        // ============================================================

        [SerializeField, Min(0f)]
        [Tooltip("유닛 중심 사이의 2D 거리. 이 값 이상일 때 조건을 충족합니다.")]
        private float _minimumDistance = 5f;

        public float MinimumDistance => Mathf.Max(0f, _minimumDistance);

        public override CombatStateChange OwnerDependencies => CombatStateChange.Lifetime;

        public override CombatStateChange TargetDependencies => CombatStateChange.Lifetime;

        public override bool UsesSpatialQuery => true;

        public override bool SupportsSourceSnapshot => true;

        // ============================================================
        // Evaluate
        // ============================================================

        public override bool Evaluate(PassiveContext context) => Evaluate(new SkillConditionContext(
            context.Owner, context.TargetSnapshot, context.TargetResolver,
            context.Metadata, frozenTarget: context.FrozenTarget));

        public override bool Evaluate(SkillConditionContext context)
        {
            // 즉시 피해와 투사체 모두 피해 계산 시점의 현재 위치를 사용한다.
            // 스냅샷은 원래 유닛의 수명 확인에만 사용하고 저장 위치는 읽지 않는다.
            if (!TryPosition(context.Owner, out var owner) ||
                !TryPosition(context.Target, out var target) ||
                float.IsNaN(_minimumDistance) || float.IsInfinity(_minimumDistance))
                return false;

            return Vector2.Distance(owner, target) >= MinimumDistance;
        }

        private static bool TryPosition(CombatTargetSnapshot snapshot, out Vector2 position)
        {
            position = default;

            if (!snapshot.IsTargetable || snapshot.Target.Transform == null)
                return false;

            position = snapshot.Target.Transform.position;
            return true;
        }
    }
}
