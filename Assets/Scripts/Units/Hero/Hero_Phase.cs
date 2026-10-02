using System.Collections.Generic;
using Units.Skills;
using UnityEngine;
namespace Units
{
    [DisallowMultipleComponent]
    public sealed class Hero_Phase : MonoBehaviour
    {
        [SerializeField] private Hero_PhaseSetData _data;
        private Unit_Core _core;
        private Hero_PhaseData _current;
        private Hero_PhaseEffect _effects;
        private bool _running, _paused, _transitioning, _pending;
        private float _elapsed;
        public int Version { get; private set; }
        public bool IsInitialized => _core != null && _current != null;
        public bool BlocksNewExecution => _transitioning || _pending;

        internal void Initialize(Unit_Core core)
        {
            Stop();
            _core = core;
            if (_data == null) { Debug.LogWarning("[Hero_Phase] Phase Set이 없습니다.", this); return; }
            if (!_data.Validate(out var error)) { Debug.LogError("[Hero_Phase] " + error, this); return; }
            var ids = new HashSet<string>();
            foreach (var entry in core.RuntimeStatus.ActiveSkills) if (entry != null) ids.Add(entry.Id);
            foreach (var phase in _data.Phases)
                foreach (var policy in phase.SkillPolicies)
                    if (!ids.Contains(policy.SkillId)) { Debug.LogError("[Hero_Phase] 없는 스킬 ID: " + policy.SkillId, this); return; }
            _effects = new Hero_PhaseEffect(core);
            Enter(_data.Find(_data.InitialPhaseId));
        }
        internal SkillUsePolicyData GetPolicy(UnitActiveSkillEntry entry) => _current != null ? _current.GetPolicy(entry) : entry.Policy;
        internal bool TryGetInfo(out Hero_PhaseInfo info)
        {
            info = IsInitialized ? new Hero_PhaseInfo(_current.Id, _current.Name, Version, _elapsed, _pending) : default;
            return IsInitialized;
        }
        internal void StartPhase() { if (IsInitialized) _running = true; }
        internal void PausePhase() => _paused = true;
        internal void ResumePhase() => _paused = false;
        internal void Stop()
        {
            _running = false; _paused = false; _pending = false;
            _current = null;
            Version++;
            var effects = _effects; _effects = null;
            _core = null;
            effects?.Clear();
        }
        private void OnDisable() => Stop();
        private void LateUpdate() => Tick(Time.deltaTime);
        internal void Tick(float delta)
        {
            if (!_running || _paused || _transitioning || !IsInitialized || !_core.IsAlive || _core.CurrentHp <= 0) return;
            _elapsed += Mathf.Max(0, delta);
            Hero_PhaseTransitionData selected = null;
            foreach (var transition in _current.Transitions)
                if (transition.Matches(_core, _elapsed) && (selected == null || transition.Priority > selected.Priority)) selected = transition;
            _pending = selected != null;
            if (selected == null) return;
            if (_core.IsCombatBusy && selected.Mode == Hero_PhaseTransitionMode.AfterExecution) return;
            _transitioning = true;
            try
            {
                var core = _core;
                var lifetime = new CombatTargetSnapshot(core.CombatTarget);
                int version = Version;
                if (selected.Mode == Hero_PhaseTransitionMode.InterruptExecution) core.InterruptCombatForPhase();
                if (version != Version || _core != core || !lifetime.IsTargetable || core.CurrentHp <= 0) return;
                Enter(_data.Find(selected.Destination));
            }
            finally { _transitioning = false; }
        }
        private void Enter(Hero_PhaseData phase)
        {
            if (phase == null) return;
            _transitioning = true;
            try
            {
                _current = phase; _elapsed = 0; _pending = false; Version++;
                var core = _core;
                var lifetime = new CombatTargetSnapshot(core.CombatTarget);
                int version = Version;
                _effects.Replace(phase);
                foreach (var action in phase.EntryActions)
                {
                    if (version != Version || !lifetime.IsTargetable || core.CurrentHp <= 0) return;
                    action?.Execute(core);
                }
                if (version == Version && lifetime.IsTargetable && core.CurrentHp > 0) core.NotifyHeroPhaseChanged();
            }
            finally { _transitioning = false; }
        }
    }
}
