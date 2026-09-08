using System.Collections.Generic;
using UnityEngine;
using Units.UnitDatas;


namespace Units
{
    public partial class Unit_RuntimeStatus
    {
        private class AdjustedStatus
        {
            // ============================================================
            // Data
            // ============================================================

            private readonly Dictionary<UnitStatType, float> _baseValues;
            private readonly Dictionary<UnitStatType, float> _adjustedValues;


            // ============================================================
            // Constructor
            // ============================================================

            public AdjustedStatus(
                UnitData unitData,
                IEnumerable<UnitStatModifier> modifiers)
            {
                _baseValues =
                    CreateBaseValues(unitData);

                _adjustedValues =
                    CalculateAdjustedValues(
                        _baseValues,
                        modifiers
                    );
            }


            // ============================================================
            // Public Methods
            // ============================================================

            public float Get(UnitStatType statType)
            {
                return GetValue(
                    _adjustedValues,
                    statType
                );
            }


            // ============================================================
            // Calculation
            // ============================================================

            private Dictionary<UnitStatType, float> CalculateAdjustedValues(
                Dictionary<UnitStatType, float> baseValues,
                IEnumerable<UnitStatModifier> modifiers)
            {
                Dictionary<UnitStatType, float> result =
                    new(baseValues);

                if (modifiers == null)
                    return result;

                Dictionary<UnitStatType, float> flatValues =
                    new();

                Dictionary<UnitStatType, float> percentValues =
                    new();

                foreach (UnitStatModifier modifier in modifiers)
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
                    float baseValue =
                        GetValue(
                            baseValues,
                            statType
                        );

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

                    result[statType] =
                        CalculateValue(
                            baseValue,
                            flat,
                            percent
                        );
                }

                return result;
            }


            // ============================================================
            // Data Creation
            // ============================================================

            private Dictionary<UnitStatType, float> CreateBaseValues(
                UnitData unitData)
            {
                Dictionary<UnitStatType, float> result =
                    new();

                foreach (UnitStatEntry stat in unitData.Stats)
                {
                    result[stat.StatType] =
                        stat.Value;
                }

                return result;
            }


            // ============================================================
            // Utility
            // ============================================================

            private static float CalculateValue(
                float baseValue,
                float flat,
                float percent)
            {
                return Mathf.Max(
                    0f,
                    (baseValue + flat)
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