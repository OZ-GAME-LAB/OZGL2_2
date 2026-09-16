using UnityEngine;



namespace Units
{
    public class UnitAIAction_Move : IUnitAIAction
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
            => UnitAIActionType.Move;


        // ============================================================
        // Constructor
        // ============================================================

        public UnitAIAction_Move(
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
            if (!assignment.HasValue)
                return;


            _core.MoveTo(
                assignment.Value.PreferredPosition
            );
        }


        public UnitAIActionType? Evaluate(
            UnitAssignment? assignment)
        {
            if (!assignment.HasValue)
            {
                return UnitAIActionType.Idle;
            }


            UnitAssignment currentAssignment =
                assignment.Value;


            if (!IsTargetValid(
                currentAssignment.Target))
            {
                return UnitAIActionType.Idle;
            }


            UnitAIActionType nextAction =
                _actionSelector.SelectAction(
                    currentAssignment
                );


            if (nextAction
                != UnitAIActionType.Move)
            {
                return nextAction;
            }


            return null;
        }


        public void Exit()
        {
            _core.StopMovement();
        }


        // ============================================================
        // Position
        // ============================================================

        private bool IsPreferredPositionReached(
            UnitAssignment assignment)
        {
            float distance =
                Vector2.Distance(
                    _core.transform.position,
                    assignment.PreferredPosition
                );


            return distance
                <= assignment.PositionTolerance;
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