using System;
using UnityEngine;
using Units.Effects;
namespace Units
{
    [Serializable]
    public abstract class Hero_PhaseActionData
    { public abstract void Execute(Unit_Core core); }
    [Serializable]
    public sealed class Hero_HealAction : Hero_PhaseActionData
    {
        [SerializeField, Range(0, 1)] private float _maxHpRatio = .2f;
        public override void Execute(Unit_Core core) => core.HealWithResult(core.RuntimeStatus.MaxHp * _maxHpRatio,
            CombatEventMetadata.Create(core.CombatTarget));
    }
    [Serializable]
    public sealed class Hero_ShieldAction : Hero_PhaseActionData
    {
        [SerializeField, Min(0)] private float _maxHpRatio = .2f;
        public override void Execute(Unit_Core core) => core.AddShieldWithResult(core.RuntimeStatus.MaxHp * _maxHpRatio,
            CombatEventMetadata.Create(core.CombatTarget));
    }
    [Serializable]
    public sealed class Hero_ApplyEffectAction : Hero_PhaseActionData
    {
        [SerializeField] private EffectData _effect;
        public override void Execute(Unit_Core core)
        {
            if (_effect != null) RuntimeEffectManager.Instance?.ApplyEffect(new EffectRequest(_effect, core.CombatTarget, core.CombatTarget));
        }
    }
}
