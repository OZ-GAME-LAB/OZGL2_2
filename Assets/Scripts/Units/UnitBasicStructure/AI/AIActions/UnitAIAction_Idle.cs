


namespace Units
{
    public class UnitAIAction_Idle : IUnitAIAction
    {
        // ============================================================
        // Reference
        // ============================================================

        private readonly Unit_Core _core;

        private readonly UnitAIActionSelector _actionSelector;


        // ============================================================
        // Properties
        // ============================================================

        public UnitAIActionType ActionType
            => UnitAIActionType.Idle;


        // ============================================================
        // Constructor
        // ============================================================

        public UnitAIAction_Idle(
            Unit_Core core,
            UnitAIActionSelector actionSelector)
        {
            _core = core;

            _actionSelector =
                actionSelector;
        }


        // ============================================================
        // Action
        // ============================================================

        public void Enter(
            UnitAssignment? assignment)
        {
            _core.StopMovement();
        }


        public UnitAIActionType? Evaluate(
            UnitAssignment? assignment)
        {
            if (!assignment.HasValue)
                return null;


            UnitAIActionType nextAction =
                _actionSelector.SelectAction(
                    assignment.Value
                );


            if (nextAction
                == UnitAIActionType.Idle)
            {
                return null;
            }


            return nextAction;
        }


        public void Exit()
        {
        }
    }
}