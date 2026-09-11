


namespace Units
{
    public class UnitAIAction_BasicAttack : IUnitAIAction
    {
        // ============================================================
        // Reference
        // ============================================================

        private readonly Unit_Core _core;


        // ============================================================
        // Properties
        // ============================================================

        public UnitAIActionType ActionType
            => UnitAIActionType.BasicAttack;


        // ============================================================
        // Constructor
        // ============================================================

        public UnitAIAction_BasicAttack(
            Unit_Core core)
        {
            _core = core;
        }


        // ============================================================
        // Action
        // ============================================================

        public void Enter(
            UnitAssignment? assignment)
        {
            if (!assignment.HasValue)
                return;


            ICombatTarget target =
                assignment.Value.Target;


            if (!IsTargetValid(
                target))
            {
                return;
            }


            _core.StopMovement();

            _core.TryBasicAttack(
                target
            );
        }


        public UnitAIActionType? Evaluate(
            UnitAssignment? assignment)
        {
            if (!assignment.HasValue)
            {
                return UnitAIActionType.Idle;
            }


            if (!IsTargetValid(
                assignment.Value.Target))
            {
                return UnitAIActionType.Idle;
            }


            return null;
        }


        public void Exit()
        {
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