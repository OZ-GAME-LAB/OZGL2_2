using UnityEngine;



namespace Units
{
    public class UnitAIActionSelector
    {
        // ============================================================
        // Reference
        // ============================================================

        private readonly Unit_Core _core;


        // ============================================================
        // Constructor
        // ============================================================

        public UnitAIActionSelector(
            Unit_Core core)
        {
            _core = core;
        }


        // ============================================================
        // Action Selection
        // ============================================================

        public UnitAIActionType SelectAction(
            UnitAssignment assignment, bool approaching = false)
        {
            if (_core == null)
                return UnitAIActionType.Idle;


            if (_core.TrySelectActiveSkill(assignment.Target, out _))
                return UnitAIActionType.ActiveSkill;
            if (!IsTargetValid(assignment.Target)) return UnitAIActionType.Idle;
            float distance = GetDistanceToTarget(assignment.Target);

            // 접근을 시작했으면 사거리 안쪽까지 이동하여 경계에서의 반복 정지를 막는다.
            // 공격 판정 자체의 사거리는 변경하지 않는다.
            if (approaching && _core.CanMove
                && distance > _core.PreferredCombatRange * 0.9f)
                return UnitAIActionType.Move;

            if (CanUseBasicAttack(
                distance))
            {
                return UnitAIActionType.BasicAttack;
            }


            if (ShouldMove(
                distance))
            {
                return UnitAIActionType.Move;
            }


            return UnitAIActionType.Idle;
        }


        // ============================================================
        // Active Skill
        // ============================================================

        private bool CanUseBasicAttack(
            float distance)
        {
            if (!_core.CanUseBasicAttack)
                return false;


            if (_core.RuntimeStatus == null)
                return false;


            if (_core.RuntimeStatus.BasicAttackData == null)
                return false;


            return distance <=
                _core.RuntimeStatus.BasicAttackData.BasicAttackRange;
        }


        // ============================================================
        // Move
        // ============================================================

        private bool ShouldMove(
            float distance)
        {
            if (!_core.CanMove)
                return false;


            return distance >
                _core.PreferredCombatRange;
        }


        // ============================================================
        // Distance
        // ============================================================

        private float GetDistanceToTarget(
            ICombatTarget target)
        {
            Vector2 unitPosition =
                _core.transform.position;


            Vector2 targetPosition =
                target.Transform.position;


            return Vector2.Distance(
                unitPosition,
                targetPosition
            );
        }


        // ============================================================
        // Validation
        // ============================================================

        private bool IsTargetValid(
            ICombatTarget target)
        {
            return target != null
                && target.IsTargetable
                && target.Transform != null;
        }
    }
}