using UnityEngine;



namespace Units
{
    public class UnitStatModifierManager : MonoBehaviour
    {
        // ============================================================
        // Data
        // ============================================================

        private AllyStatModifierContainer _allyContainer;

        private EnemyStatModifierContainer _enemyContainer;

        private StatModifierOrganizer _organizer;

        private FinalStatModifierBuilder _builder;


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Awake()
        {
            Initialize();
        }


        // ============================================================
        // Public Methods
        // ============================================================

        public void AddAllyModifier(
            AllyStatModifier modifier)
        {
            _organizer.AddAllyModifier(
                modifier
            );
        }


        public void AddEnemyModifier(
            EnemyStatModifier modifier)
        {
            _organizer.AddEnemyModifier(
                modifier
            );
        }


        public void RemoveModifiersBySource(
            object source)
        {
            _organizer.RemoveBySource(
                source
            );
        }


        public FinalStatModifier GetAllyFinalModifier(
            AllyUnitClass unitClass,
            AllyUnitType unitType,
            AllyUnitTier unitTier)
        {
            return _builder.BuildAlly(
                unitClass,
                unitType,
                unitTier
            );
        }


        public FinalStatModifier GetEnemyFinalModifier(
            EnemyUnitClass unitClass,
            EnemyUnitType unitType,
            EnemyUnitFaction unitFaction)
        {
            return _builder.BuildEnemy(
                unitClass,
                unitType,
                unitFaction
            );
        }


        // ============================================================
        // Private Methods
        // ============================================================

        private void Initialize()
        {
            _allyContainer =
                new AllyStatModifierContainer();

            _enemyContainer =
                new EnemyStatModifierContainer();

            _organizer =
                new StatModifierOrganizer(
                    _allyContainer,
                    _enemyContainer
                );

            _builder =
                new FinalStatModifierBuilder(
                    _allyContainer,
                    _enemyContainer
                );
        }
    }
}