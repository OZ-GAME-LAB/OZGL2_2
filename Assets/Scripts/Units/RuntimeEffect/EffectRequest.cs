


namespace Units.Effects
{
    public readonly struct EffectRequest
    {

        public CombatEventMetadata Metadata { get; }

        // ============================================================
        // Properties
        // ============================================================

        public EffectData EffectData { get; }

        public EffectDefinitionSnapshot Definition { get; }

        public ICombatTarget Source { get; }

        public ICombatTarget Target { get; }

        public CombatTargetSnapshot TargetSnapshot { get; }


        // ============================================================
        // Constructor
        // ============================================================

        public EffectRequest(
            EffectData effectData,
            ICombatTarget source,
            ICombatTarget target,
            CombatEventMetadata metadata = default,
            EffectDefinitionSnapshot definition = null)
        {
            Definition = definition ?? (effectData != null ? new EffectDefinitionSnapshot(effectData) : null);

            TargetSnapshot = new CombatTargetSnapshot(target);

            Metadata = metadata.EventId != 0 ? metadata : CombatEventMetadata.Create(source);

            EffectData = effectData;

            Source = source;

            Target = target;
        }
    }
}