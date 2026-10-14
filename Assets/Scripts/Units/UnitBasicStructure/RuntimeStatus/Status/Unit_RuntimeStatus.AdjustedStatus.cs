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
                FinalStatModifier modifier)
            {
                _baseValues =
                    CreateBaseValues(unitData);

                _adjustedValues =
                    CalculateAdjustedValues(
                        _baseValues,
                        modifier
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
                FinalStatModifier modifier)
            {
                Dictionary<UnitStatType, float> result =
                    new(baseValues);


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
                        0f;

                    float percent =
                        0f;


                    if (modifier != null &&
                        modifier.TryGetStat(
                            statType,
                            out StatModifierValue modifierValue))
                    {
                        flat =
                            modifierValue.Flat;

                        percent =
                            modifierValue.Percent;
                    }


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