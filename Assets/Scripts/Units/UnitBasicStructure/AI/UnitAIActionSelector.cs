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
            UnitAssignment assignment)
        {
            if (_core == null)
                return UnitAIActionType.Idle;

            if (!IsTargetValid(
                assignment.Target))
            {
                return UnitAIActionType.Idle;
            }


            float distance =
                GetDistanceToTarget(
                    assignment.Target
                );


            if (CanUseActiveSkill(
                distance))
            {
                return UnitAIActionType.ActiveSkill;
            }


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

        private bool CanUseActiveSkill(
            float distance)
        {
            if (!_core.CanUseActiveSkill)
                return false;

            if (_core.RuntimeStatus == null)
                return false;

            if (_core.RuntimeStatus.ActiveSkillData == null)
                return false;

            return distance <=
                _core.RuntimeStatus.ActiveSkillData.SkillRange;
        }


        // ============================================================
        // Basic Attack
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
                targetPosition);
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