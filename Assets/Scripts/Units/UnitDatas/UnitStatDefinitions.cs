using System;


namespace Units.UnitDatas
{
    public static class UnitStatDefinitions
    {
        // ============================================================
        // Public Methods
        // ============================================================

        public static UnitStatDefinition Get(
            UnitStatType statType)
        {
            switch (statType)
            {
                // ====================================================
                // Life
                // ====================================================

                case UnitStatType.MaxHp:

                    return new UnitStatDefinition(
                        0f,
                        1f,
                        100000000f,
                        UnitStatDisplayType.Value
                    );


                case UnitStatType.Defense:

                    return new UnitStatDefinition(
                        0f,
                        0f,
                        10000f,
                        UnitStatDisplayType.Value
                    );


                case UnitStatType.DamageTakenMultiplier:

                    return new UnitStatDefinition(
                        1f,
                        0.1f,
                        10f,
                        UnitStatDisplayType.Multiplier
                    );


                case UnitStatType.HealingTakenMultiplier:

                    return new UnitStatDefinition(
                        1f,
                        0f,
                        10f,
                        UnitStatDisplayType.Multiplier
                    );


                // ====================================================
                // Combat
                // ====================================================

                case UnitStatType.AttackPower:

                    return new UnitStatDefinition(
                        0f,
                        0f,
                        10000f,
                        UnitStatDisplayType.Value
                    );


                case UnitStatType.DamageMultiplier:

                    return new UnitStatDefinition(
                        1f,
                        0.1f,
                        10f,
                        UnitStatDisplayType.Multiplier
                    );


                case UnitStatType.BasicAttackMultiplier:

                    return new UnitStatDefinition(
                        1f,
                        0.1f,
                        10f,
                        UnitStatDisplayType.Multiplier
                    );


                case UnitStatType.SkillDamageMultiplier:

                    return new UnitStatDefinition(
                        1f,
                        0.1f,
                        10f,
                        UnitStatDisplayType.Multiplier
                    );


                case UnitStatType.DefenseIgnore:

                    return new UnitStatDefinition(
                        0f,
                        0f,
                        1f,
                        UnitStatDisplayType.Percentage
                    );


                case UnitStatType.LifeSteal:

                    return new UnitStatDefinition(
                        0f,
                        0f,
                        1f,
                        UnitStatDisplayType.Percentage
                    );


                case UnitStatType.HealingMultiplier:

                    return new UnitStatDefinition(
                        1f,
                        0f,
                        10f,
                        UnitStatDisplayType.Multiplier
                    );


                case UnitStatType.AttackSpeed:

                    return new UnitStatDefinition(
                        1f,
                        0.1f,
                        10f,
                        UnitStatDisplayType.Multiplier
                    );


                case UnitStatType.CooldownReduction:

                    return new UnitStatDefinition(
                        0f,
                        0f,
                        0.9f,
                        UnitStatDisplayType.Percentage
                    );


                // ====================================================
                // Critical
                // ====================================================

                case UnitStatType.CriticalChance:

                    return new UnitStatDefinition(
                        0f,
                        0f,
                        1f,
                        UnitStatDisplayType.Percentage
                    );


                case UnitStatType.CriticalDamage:

                    return new UnitStatDefinition(
                        1.5f,
                        1f,
                        10f,
                        UnitStatDisplayType.Percentage
                    );


                // ====================================================
                // Movement
                // ====================================================

                case UnitStatType.MoveSpeed:

                    return new UnitStatDefinition(
                        5f,
                        0f,
                        100f,
                        UnitStatDisplayType.Value
                    );


                // ====================================================
                // Detection
                // ====================================================

                case UnitStatType.DetectionRange:

                    return new UnitStatDefinition(
                        10f,
                        0f,
                        100f,
                        UnitStatDisplayType.Value
                    );


                default:

                    throw new ArgumentOutOfRangeException(
                        nameof(statType),
                        statType,
                        null
                    );
            }
        }
    }
}