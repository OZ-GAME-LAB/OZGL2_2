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

        private Vector2 _lastTargetPosition;
        private float _lastCombatRange;
        private float _nextPositionRefreshTime;
        private const float PositionRefreshInterval = 0.1f;
        private const float TargetMovementThreshold = 0.05f;

        private void RefreshPosition(ICombatTarget target)
        {
            _lastTargetPosition = target.Transform.position;
            _lastCombatRange = _core.PreferredCombatRange;
            _nextPositionRefreshTime = Time.time + PositionRefreshInterval;
            // GroupAI가 현재 대상과 장애물 배치를 기준으로 목적지를 다시 선택한다.
            // 이동 중 갱신은 Unit_AI에서 Stop/Enter 없이 반영한다.
            _core.RequestPositionAssignment();
        }


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


            if (_core == null
                || !_core.CanMove)
            {
                return;
            }


            _core.MoveTo(
                assignment.Value.PreferredPosition
            );

            // 공격/대기 중 대상이 이동했을 수 있으므로 이전 목적지를 재사용하지 않는다.
            if (IsTargetValid(assignment.Value.Target))
                RefreshPosition(assignment.Value.Target);
        }


        public UnitAIActionType? Evaluate(
            UnitAssignment? assignment)
        {
            if (!assignment.HasValue)
            {
                return UnitAIActionType.Idle;
            }


            if (_core == null
                || !_core.CanMove)
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
                    currentAssignment, approaching: true
                );


            if (nextAction
                != UnitAIActionType.Move)
            {
                return nextAction;
            }


            if (Time.time >= _nextPositionRefreshTime
                && (Vector2.Distance(_lastTargetPosition, currentAssignment.Target.Transform.position)
                        >= TargetMovementThreshold
                    || Mathf.Abs(_lastCombatRange - _core.PreferredCombatRange) > 0.01f))
                RefreshPosition(currentAssignment.Target);

            return null;
        }


        public void Exit()
        {
            _core?.StopMovement();
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