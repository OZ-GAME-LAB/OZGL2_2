using System.Collections.Generic;



namespace Units.Skills
{
    public readonly struct SkillEffectRequest
    {
        // ============================================================
        // Source
        // ============================================================

        public Unit_Core Caster { get; }


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
            Unit_Core caster,
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