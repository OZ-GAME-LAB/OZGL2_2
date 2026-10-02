using System;
using UnityEngine;
using Units.Skills;

namespace Units
{
    [Serializable]
    public sealed class UnitActiveSkillEntry
    {
        [SerializeField] private string _id = Guid.NewGuid().ToString("N");
        [SerializeField] private ActiveSkillData _skill;
        [SerializeField] private SkillUsePolicyData _policy = new();
        public string Id => _id;
        public ActiveSkillData Skill => _skill;
        public SkillUsePolicyData Policy => _policy;
        public UnitActiveSkillEntry() { }
        public UnitActiveSkillEntry(string id, ActiveSkillData skill, SkillUsePolicyData policy = null)
        { _id = id; _skill = skill; _policy = policy ?? new SkillUsePolicyData(); }
    }
}
