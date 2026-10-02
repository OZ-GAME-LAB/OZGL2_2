using System.Collections.Generic;
using Units.Effects;
namespace Units
{
    internal sealed class Hero_PhaseEffect
    {
        private readonly Unit_Core _core;
        private readonly object _statOwner = new();
        private object _effectOwner;
        internal Hero_PhaseEffect(Unit_Core core) { _core = core; }
        internal void Replace(Hero_PhaseData phase)
        {
            var status = _core.RuntimeStatus;
            status.BeginEffectTransaction();
            try
            {
                ClearEffects();
                var modifiers = new List<CombatStatModifier>();
                if (phase != null)
                    foreach (var value in phase.StatModifiers) if (value != null) modifiers.Add(value.Create(_statOwner));
                status.ReplaceCombatModifiers(_statOwner, modifiers);
                if (phase == null || !_core.IsAlive || _core.CurrentHp <= 0) return;
                _effectOwner = new object();
                foreach (var effect in phase.MaintainedEffects)
                    if (effect != null) RuntimeEffectManager.Instance?.ApplyEffect(new EffectRequest(effect,
                        _core.CombatTarget, _core.CombatTarget, ownershipKey: _effectOwner));
            }
            finally { status.EndEffectTransaction(true); }
        }
        private void ClearEffects()
        {
            if (_effectOwner == null) return;
            RuntimeEffectManager.Instance?.RemoveOwnedEffects(_core.CombatTarget, _effectOwner);
            _effectOwner = null;
        }
        internal void Clear() => Replace(null);
    }
}
