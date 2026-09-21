using System.Collections.Generic;


namespace Units.Skills
{
    public readonly struct SkillEffectRequest
    {
        // ============================================================
        // Source
        // ============================================================

        public ICombatTarget Caster { get; }


        // ============================================================
        // Target
        // ============================================================

        public ICombatTarget Target { get; }


        // ============================================================
        // Effects
        // ============================================================

        public IReadOnlyList<SkillEffectData> Effects { get; }


        // ============================================================
        // Constructor
        // ============================================================

        public SkillEffectRequest(
            ICombatTarget caster,
            ICombatTarget target,
            IReadOnlyList<SkillEffectData> effects)
        {
            Caster =
                caster;

            Target =
                target;

            Effects =
                effects;
        }
    }
}