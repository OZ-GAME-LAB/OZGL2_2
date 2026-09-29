using System.Collections.Generic;



namespace Units
{
    public interface IUnitStatModifierRegister
    {
        void AddBothModifiers(
            IReadOnlyList<AllyStatModifier> allyModifiers,
            IReadOnlyList<EnemyStatModifier> enemyModifiers);

        void AddAllyModifiers(
            IReadOnlyList<AllyStatModifier> modifiers);

        void AddEnemyModifiers(
            IReadOnlyList<EnemyStatModifier> modifiers);

        void AddAllyModifier(
            AllyStatModifier modifier);

        void AddEnemyModifier(
            EnemyStatModifier modifier);

        void RemoveModifiersBySource(
            object source);
    }
}