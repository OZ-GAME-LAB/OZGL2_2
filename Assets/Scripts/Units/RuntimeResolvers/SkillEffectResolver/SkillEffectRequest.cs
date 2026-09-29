using System.Collections.Generic;


namespace Units.Skills
{
    public readonly struct SkillEffectRequest
    {

        public CombatEventMetadata Metadata { get; }

        public SkillEffectBatch Batch { get; }

        public CombatSourceSnapshot SourceSnapshot { get; }

        public System.Func<bool> CanContinue { get; }

        // ============================================================
        // Source
        // ============================================================

        public ICombatTarget Caster { get; }


        // ============================================================
        // Target
        // ============================================================

        public ICombatTarget Target { get; }

        public CombatTargetSnapshot TargetSnapshot { get; }


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
            IReadOnlyList<SkillEffectData> effects,
            CombatEventMetadata metadata = default,
            SkillEffectBatch batch = null,
            System.Func<bool> canContinue = null,
            CombatSourceSnapshot sourceSnapshot = null)
        {
            SourceSnapshot = sourceSnapshot;

            Batch = batch;

            CanContinue = canContinue;

            TargetSnapshot = new CombatTargetSnapshot(target);

            Metadata = metadata.EventId != 0 ? metadata : CombatEventMetadata.Create(caster);

            Caster = caster;

            Target = target;

            Effects = effects;
        }
    }
}
