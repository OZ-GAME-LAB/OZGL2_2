


namespace Units
{
    public class StatModifierOrganizer
    {
        // ============================================================
        // Data
        // ============================================================

        private readonly AllyStatModifierContainer _allyContainer;
        private readonly EnemyStatModifierContainer _enemyContainer;


        // ============================================================
        // Constructor
        // ============================================================

        public StatModifierOrganizer(
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

        public void AddAllyModifier(
            AllyStatModifier modifier)
        {
            switch (modifier.ApplyType)
            {
                case UnitModifierApplyType.All:
                    _allyContainer.AddToAll(
                        modifier
                    );
                    break;

                case UnitModifierApplyType.Class:
                    _allyContainer.AddToClass(
                        modifier.TargetClass,
                        modifier
                    );
                    break;

                case UnitModifierApplyType.Type:
                    _allyContainer.AddToType(
                        modifier.TargetUnitType,
                        modifier
                    );
                    break;
            }
        }


        public void AddEnemyModifier(
            EnemyStatModifier modifier)
        {
            switch (modifier.ApplyType)
            {
                case UnitModifierApplyType.All:
                    _enemyContainer.AddToAll(
                        modifier
                    );
                    break;

                case UnitModifierApplyType.Class:
                    _enemyContainer.AddToClass(
                        modifier.TargetClass,
                        modifier
                    );
                    break;

                case UnitModifierApplyType.Type:
                    _enemyContainer.AddToType(
                        modifier.TargetUnitType,
                        modifier
                    );
                    break;
            }
        }


        public void RemoveBySource(
            object source)
        {
            _allyContainer.RemoveBySource(
                source
            );

            _enemyContainer.RemoveBySource(
                source
            );
        }
    }
}