using System.Collections.Generic;
using UnityEngine;



namespace Units
{
    public partial class Unit_RuntimeStatus
    {
        private class FinalStatus
        {
            // ============================================================
            // Data
            // ============================================================

            private readonly AdjustedStatus _adjustedStatus;

            private readonly List<UnitStatModifier> _modifiers =
                new();

            private readonly Dictionary<UnitStatType, float> _finalValues =
                new();


            // ============================================================
            // Constructor
            // ============================================================

            public FinalStatus(
                AdjustedStatus adjustedStatus)
            {
                _adjustedStatus =
                    adjustedStatus;

                Recalculate();
            }


            // ============================================================
            // Public Methods
            // ============================================================

            public float Get(UnitStatType statType)
            {
                return GetValue(
                    _finalValues,
                    statType
                );
            }


            public void AddModifier(
                UnitStatModifier modifier)
            {
                _modifiers.Add(modifier);

                Recalculate();
            }


            public void RemoveModifiers(object source)
            {
                _modifiers.RemoveAll(
                    modifier =>
                        ReferenceEquals(
                            modifier.Source,
                            source
                        )
                );

                Recalculate();
            }


            public void ClearModifiers()
            {
                _modifiers.Clear();

                Recalculate();
            }


            public Dictionary<UnitStatType, float> GetAffectedValues(
                object source)
            {
                Dictionary<UnitStatType, float> result =
                    new();

                foreach (UnitStatModifier modifier in _modifiers)
                {
                    if (!ReferenceEquals(
                            modifier.Source,
                            source))
                    {
                        continue;
                    }

                    if (result.ContainsKey(
                            modifier.StatType))
                    {
                        continue;
                    }

                    result.Add(
                        modifier.StatType,
                        Get(modifier.StatType)
                    );
                }

                return result;
            }


            public Dictionary<UnitStatType, float> GetCurrentValues()
            {
                return new Dictionary<UnitStatType, float>(
                    _finalValues
                );
            }


            // ============================================================
            // Calculation
            // ============================================================

            private void Recalculate()
            {
                Dictionary<UnitStatType, float> flatValues =
                    new();

                Dictionary<UnitStatType, float> percentValues =
                    new();

                foreach (UnitStatModifier modifier in _modifiers)
                {
                    switch (modifier.ModifierType)
                    {
                        case UnitStatModifierType.Flat:
                            AddValue(
                                flatValues,
                                modifier.StatType,
                                modifier.Value
                            );
                            break;

                        case UnitStatModifierType.Percent:
                            AddValue(
                                percentValues,
                                modifier.StatType,
                                modifier.Value
                            );
                            break;
                    }
                }

                foreach (UnitStatType statType
                         in System.Enum.GetValues(
                             typeof(UnitStatType)))
                {
                    float adjustedValue =
                        _adjustedStatus.Get(statType);

                    float flat =
                        GetValue(
                            flatValues,
                            statType
                        );

                    float percent =
                        GetValue(
                            percentValues,
                            statType
                        );

                    _finalValues[statType] =
                        CalculateValue(
                            adjustedValue,
                            flat,
                            percent
                        );
                }
            }


            // ============================================================
            // Utility
            // ============================================================

            private static float CalculateValue(
                float adjustedValue,
                float flat,
                float percent)
            {
                return Mathf.Max(
                    0f,
                    (adjustedValue + flat)
                    * (1f + percent)
                );
            }


            private static void AddValue(
                Dictionary<UnitStatType, float> dictionary,
                UnitStatType statType,
                float value)
            {
                if (dictionary.TryGetValue(
                        statType,
                        out float currentValue))
                {
                    dictionary[statType] =
                        currentValue + value;

                    return;
                }

                dictionary.Add(
                    statType,
                    value
                );
            }


            private static float GetValue(
                Dictionary<UnitStatType, float> dictionary,
                UnitStatType statType)
            {
                return dictionary.TryGetValue(
                    statType,
                    out float value)
                    ? value
                    : 0f;
            }
        }
    }
}