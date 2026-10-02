using Units.Skills;

namespace Units
{
    internal sealed class RuntimeActiveSkill
    {
        internal readonly UnitActiveSkillEntry Entry;
        internal readonly ActiveSkillExecutor Executor;
        internal readonly SkillTargetSelector Selector;
        internal float Cooldown, Retry;
        internal RuntimeActiveSkill(Unit_Core core, UnitActiveSkillEntry entry, TargetResolver resolver)
        {
            Entry = entry;
            Executor = new ActiveSkillExecutor(core, entry.Skill, resolver);
            Selector = new SkillTargetSelector(core, entry.Skill);
        }
    }
}
