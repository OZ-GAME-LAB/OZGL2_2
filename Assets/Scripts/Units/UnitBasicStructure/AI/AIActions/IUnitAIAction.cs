


namespace Units
{
    public interface IUnitAIAction
    {
        // ============================================================
        // Properties
        // ============================================================

        UnitAIActionType ActionType
        {
            get;
        }


        // ============================================================
        // Action
        // ============================================================

        void Enter(
            UnitAssignment? assignment);

        UnitAIActionType? Evaluate(
            UnitAssignment? assignment);

        void Exit();
    }
}