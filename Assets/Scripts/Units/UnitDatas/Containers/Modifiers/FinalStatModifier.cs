using System.Collections.Generic;



namespace Units
{
    public sealed class FinalStatModifier
    {
        // ============================================================
        // Data
        // ============================================================

        private readonly Dictionary<UnitStatType, StatModifierValue> _stats;


        // ============================================================
        // Properties
        // ============================================================

        public IReadOnlyDictionary<UnitStatType, StatModifierValue> Stats =>
            _stats;


        // ============================================================
        // Constructor
        // ============================================================

        public FinalStatModifier(
            Dictionary<UnitStatType, StatModifierValue> stats)
        {
            _stats =
                new Dictionary<UnitStatType, StatModifierValue>(stats);
        }


        // ============================================================
        // Public Methods
        // ============================================================

        public bool TryGetStat(
            UnitStatType statType,
            out StatModifierValue value)
        {
            return _stats.TryGetValue(
                statType,
                out value
            );
        }
    }
}