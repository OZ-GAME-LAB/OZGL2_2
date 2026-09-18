


namespace Units.Effects
{
    public readonly struct EffectRequest
    {
        // ============================================================
        // Properties
        // ============================================================

        public EffectData EffectData { get; }

        public Unit_Gateway Source { get; }

        public Unit_Gateway Target { get; }


        // ============================================================
        // Constructor
        // ============================================================

        public EffectRequest(
            EffectData effectData,
            Unit_Gateway source,
            Unit_Gateway target)
        {
            EffectData = effectData;
            Source = source;
            Target = target;
        }
    }
}