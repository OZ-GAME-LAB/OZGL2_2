using System.Collections.Generic;

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

        public void AddAllyModifiers(
            IReadOnlyList<AllyStatModifier> modifiers)
        {
            if (modifiers == null)
                return;

            foreach (AllyStatModifier modifier in modifiers)
            {
                AddAllyModifier(
                    modifier
                );
            }
        }


        public void AddEnemyModifiers(
            IReadOnlyList<EnemyStatModifier> modifiers)
        {
            if (modifiers == null)
                return;

            foreach (EnemyStatModifier modifier in modifiers)
            {
                AddEnemyModifier(
                    modifier
                );
            }
        }


        public void AddAllyModifier(
            AllyStatModifier modifier)
        {
            switch (modifier.ApplyType)
            {
                case UnitModifierApplyType.Tier:
                    _allyContainer.AddToTier(
                        modifier.TargetTier,
                        modifier
                    );
                    break;

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
                case UnitModifierApplyType.Faction:
                    _enemyContainer.AddToFaction(
                        modifier.TargetFaction,
                        modifier
                    );
                    break;

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