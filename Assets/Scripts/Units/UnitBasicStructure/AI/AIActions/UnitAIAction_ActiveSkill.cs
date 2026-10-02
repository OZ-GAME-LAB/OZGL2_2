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
            if (_core == null || !_core.CanUseActiveSkill) return;
            _core.StopMovement();
            _started = _core.TryActiveSkill(_core.SelectedSkillDecision);
        }
        public UnitAIActionType? Evaluate(UnitAssignment? assignment)
            => !_started || !_core.IsCombatBusy ? UnitAIActionType.Idle : (UnitAIActionType?)null;
        public void Exit() { _started = false; }
    }
}
