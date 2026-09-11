using System.Collections.Generic;



namespace Units
{
    public class FinalStatModifierBuilder
    {
        // ============================================================
        // Data
        // ============================================================

        private readonly AllyStatModifierContainer _allyContainer;
        private readonly EnemyStatModifierContainer _enemyContainer;


        // ============================================================
        // Constructor
        // ============================================================

        public FinalStatModifierBuilder(
            AllyStatModifierContainer allyContainer,
            EnemyStatModifierContainer enemyContainer)
        {
            _allyContainer =
                allyContainer;

            _enemyContainer =
                enemyContainer;
        }


        // ============================================================
        // Public Methods
        // ============================================================

        public FinalStatModifier BuildAlly(
            AllyUnitClass unitClass,
            AllyUnitType unitType)
        {
            Dictionary<UnitStatType, StatModifierValue> result =
                new();

            AddAllyModifiers(
                result,
                _allyContainer.GetFromAllModifiers()
            );

            AddAllyModifiers(
                result,
                _allyContainer.GetFromClassModifiers(unitClass)
            );

            AddAllyModifiers(
                result,
                _allyContainer.GetFromTypeModifiers(unitType)
            );

            return new FinalStatModifier(result);
        }


        public FinalStatModifier BuildEnemy(
            EnemyUnitClass unitClass,
            EnemyUnitType unitType)
        {
            Dictionary<UnitStatType, StatModifierValue> result =
                new();

            AddEnemyModifiers(
                result,
                _enemyContainer.GetFromAllModifiers()
            );

            AddEnemyModifiers(
                result,
                _enemyContainer.GetFromClassModifiers(unitClass)
            );

            AddEnemyModifiers(
                result,
                _enemyContainer.GetFromTypeModifiers(unitType)
            );

            return new FinalStatModifier(result);
        }


        // ============================================================
        // Private Methods
        // ============================================================

        private void AddAllyModifiers(
            Dictionary<UnitStatType, StatModifierValue> result,
            IReadOnlyList<AllyStatModifier> modifiers)
        {
            foreach (AllyStatModifier modifier in modifiers)
            {
                AddModifier(
                    result,
                    modifier.StatType,
                    modifier.ModifierType,
                    modifier.Value
                );
            }
        }


        private void AddEnemyModifiers(
            Dictionary<UnitStatType, StatModifierValue> result,
            IReadOnlyList<EnemyStatModifier> modifiers)
        {
            foreach (EnemyStatModifier modifier in modifiers)
            {
                AddModifier(
                    result,
                    modifier.StatType,
                    modifier.ModifierType,
                    modifier.Value
                );
            }
        }


        private void AddModifier(
            Dictionary<UnitStatType, StatModifierValue> result,
            UnitStatType statType,
            UnitStatModifierType modifierType,
            float value)
        {
            result.TryGetValue(
                statType,
                out StatModifierValue currentValue
            );

            float flat =
                currentValue.Flat;

            float percent =
                currentValue.Percent;

            switch (modifierType)
            {
                case UnitStatModifierType.Flat:
                    flat += value;
                    break;

                case UnitStatModifierType.Percent:
                    percent += value;
                    break;
            }

            result[statType] =
                new StatModifierValue(
                    flat,
                    percent
                );
        }
    }
}