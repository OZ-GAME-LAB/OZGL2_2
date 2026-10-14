


namespace Units
{
    public class UnitAIAction_ActiveSkill : IUnitAIAction
    {
        // ============================================================
        // Reference
        // ============================================================

        private readonly Unit_Core _core;

        private bool _started;


        // ============================================================
        // Properties
        // ============================================================

        public UnitAIActionType ActionType
            => UnitAIActionType.ActiveSkill;


        // ============================================================
        // Constructor
        // ============================================================

        public UnitAIAction_ActiveSkill(
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
            _started = false;
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

            _started = _core.TryActiveSkill(
                target
            );
        }


        public UnitAIActionType? Evaluate(
            UnitAssignment? assignment)
        {
            if (!_started || !assignment.HasValue)
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