using System;
using System.Collections.Generic;
using Units.Effects;
using Units.Skills;
using UnityEngine;
namespace Units
{
    [Serializable]
    public sealed class Hero_PhaseSkillPolicy
    {
        [SerializeField] private string _skillId;
        [SerializeField] private SkillUsePolicyData _policy = new();
        public string SkillId => _skillId;
        public SkillUsePolicyData Policy => _policy;
    }
    [Serializable]
    public sealed class Hero_PhaseStatModifier
    {
        [SerializeField] private UnitStatType _stat;
        [SerializeField] private UnitStatModifierType _type;
        [SerializeField] private float _value;
        public CombatStatModifier Create(object source) => new(source, _stat, _type, _value);
    }
    [Serializable]
    public sealed class Hero_PhaseData
    {
        [SerializeField] private string _id;
        [SerializeField] private string _name;
        [SerializeField, Tooltip("활성화하면 아래에 명시된 스킬만 사용할 수 있습니다.")]
        private bool _restrictSkills;
        [SerializeField] private List<Hero_PhaseSkillPolicy> _skillPolicies = new();
        [SerializeField] private List<Hero_PhaseStatModifier> _statModifiers = new();
        [SerializeField] private List<EffectData> _maintainedEffects = new();
        [SerializeReference] private List<Hero_PhaseActionData> _entryActions = new();
        [SerializeField] private List<Hero_PhaseTransitionData> _transitions = new();
        public string Id => _id;
        public string Name => _name;
        public IReadOnlyList<Hero_PhaseSkillPolicy> SkillPolicies => _skillPolicies;
        public IReadOnlyList<Hero_PhaseStatModifier> StatModifiers => _statModifiers;
        public IReadOnlyList<EffectData> MaintainedEffects => _maintainedEffects;
        public IReadOnlyList<Hero_PhaseActionData> EntryActions => _entryActions;
        public IReadOnlyList<Hero_PhaseTransitionData> Transitions => _transitions;
        public SkillUsePolicyData GetPolicy(UnitActiveSkillEntry skill)
        {
            foreach (var entry in _skillPolicies)
                if (entry != null && entry.SkillId == skill.Id) return entry.Policy;
            return _restrictSkills ? null : skill.Policy;
        }
    }
}
