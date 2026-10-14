using UnityEngine;



namespace Units
{
    public class UnitAIActionSelector
    {
        // ============================================================
        // Reference
        // ============================================================

        private readonly Unit_Core _core;

        private ICombatTarget _rangeTarget;
        private float _rangeExitGraceUntil;


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
            var basicAttack = _core.RuntimeStatus?.BasicAttackData;
            if (!ReferenceEquals(_rangeTarget, assignment.Target))
            {
                _rangeTarget = assignment.Target;
                _rangeExitGraceUntil = 0f;
            }
            if (basicAttack != null && distance <= basicAttack.BasicAttackRange)
                _rangeExitGraceUntil = Time.time + 0.2f;

            if (CanUseBasicAttack(
                distance))
            {
                return UnitAIActionType.BasicAttack;
            }


            // Cooldown is not a reason to push farther into a formation.
            if (basicAttack != null && distance <= basicAttack.BasicAttackRange)
                return UnitAIActionType.Idle;
            // Briefly tolerate boundary motion without extending the attack hit range.
            if (!approaching && !_core.CanUseBasicAttack && basicAttack != null && Time.time < _rangeExitGraceUntil
                && distance <= basicAttack.BasicAttackRange + 0.1f)
                return UnitAIActionType.Idle;
            if (assignment.WaitingForPosition)
                return UnitAIActionType.Idle;

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


            return CombatRangeUtility.Distance(_core.CombatTarget, target);
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
