


namespace Units.Effects
{
    public readonly struct EffectRequest
    {
        // ============================================================
        // Properties
        // ============================================================

        public EffectData EffectData { get; }

        public ICombatTarget Source { get; }

        public ICombatTarget Target { get; }


        // ============================================================
        // Constructor
        // ============================================================

        public EffectRequest(
            EffectData effectData,
            ICombatTarget source,
            ICombatTarget target)
        {
            EffectData = effectData;
            Source = source;
            Target = target;
        }
    }
}