namespace Units
{
    public class UnitAIAction_ActiveSkill : IUnitAIAction
    {
        private readonly Unit_Core _core;
        private bool _started;
        public UnitAIActionType ActionType => UnitAIActionType.ActiveSkill;
        public UnitAIAction_ActiveSkill(Unit_Core core) { _core = core; }
        public void Enter(UnitAssignment? assignment)
        {
            _started = false;
            if (_core == null || !_core.IsCombatBusy) return;
            _core.StopMovement();
            // Unit_AI가 전투 준비를 승인받은 뒤 이 행동에 진입한다.
            _started = true;
        }
        public UnitAIActionType? Evaluate(UnitAssignment? assignment)
            => !_started || !_core.IsCombatBusy ? UnitAIActionType.Idle : (UnitAIActionType?)null;
        public void Exit() { _started = false; }
    }
}
