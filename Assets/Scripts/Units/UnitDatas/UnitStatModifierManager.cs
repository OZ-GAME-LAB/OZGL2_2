using System.Collections.Generic;
using Units.Skills;
using UnityEngine;



namespace Units
{
    public class UnitStatModifierManager : MonoBehaviour, IUnitStatModifierRegister
    {
        // ============================================================
        // Data
        // ============================================================

        private AllyStatModifierContainer _allyContainer;

        private EnemyStatModifierContainer _enemyContainer;

        private StatModifierOrganizer _organizer;

        private FinalStatModifierBuilder _builder;

        private PassiveSkillModifierController _passiveController;


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

        public void AddBothModifiers(
            IReadOnlyList<AllyStatModifier> allyModifiers,
            IReadOnlyList<EnemyStatModifier> enemyModifiers)
        {
            _organizer.AddAllyModifiers(
                allyModifiers
            );

            _organizer.AddEnemyModifiers(
                enemyModifiers
            );
        }


        public void AddAllyModifiers(
            IReadOnlyList<AllyStatModifier> modifiers)
        {
            _organizer.AddAllyModifiers(
                modifiers
            );
        }


        public void AddEnemyModifiers(
            IReadOnlyList<EnemyStatModifier> modifiers)
        {
            _organizer.AddEnemyModifiers(
                modifiers
            );
        }


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
            Initialize();

            _organizer.RemoveBySource(
                source
            );

            _passiveController.RemoveBySource(source);
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
        // Spawn Passive Methods
        // ============================================================

        public void AddBothPassiveSkills(
            IReadOnlyList<AllyPassiveSkillModifier> allyModifiers,
            IReadOnlyList<EnemyPassiveSkillModifier> enemyModifiers)
        {
            AddAllyPassiveSkills(allyModifiers);

            AddEnemyPassiveSkills(enemyModifiers);
        }


        public void AddAllyPassiveSkill(AllyPassiveSkillModifier modifier)
        {
            Initialize();

            _passiveController.AddAllyPassiveSkill(modifier);
        }


        public void AddAllyPassiveSkills(IReadOnlyList<AllyPassiveSkillModifier> modifiers)
        {
            Initialize();

            _passiveController.AddAllyPassiveSkills(modifiers);
        }


        public void RemoveAllyPassiveSkill(AllyPassiveSkillModifier modifier)
        {
            Initialize();

            _passiveController.RemoveAllyPassiveSkill(modifier);
        }


        public void RemoveAllyPassiveSkills(IReadOnlyList<AllyPassiveSkillModifier> modifiers)
        {
            Initialize();

            _passiveController.RemoveAllyPassiveSkills(modifiers);
        }


        public IReadOnlyList<PassiveSkillData> GetAllyPassiveSkills(
            AllyUnitClass unitClass,
            AllyUnitType unitType,
            AllyUnitTier unitTier)
        {
            Initialize();

            return _passiveController.GetAllyPassiveSkills(unitClass, unitType, unitTier);
        }


        public void AddEnemyPassiveSkill(EnemyPassiveSkillModifier modifier)
        {
            Initialize();

            _passiveController.AddEnemyPassiveSkill(modifier);
        }


        public void AddEnemyPassiveSkills(IReadOnlyList<EnemyPassiveSkillModifier> modifiers)
        {
            Initialize();

            _passiveController.AddEnemyPassiveSkills(modifiers);
        }


        public void RemoveEnemyPassiveSkill(EnemyPassiveSkillModifier modifier)
        {
            Initialize();

            _passiveController.RemoveEnemyPassiveSkill(modifier);
        }


        public void RemoveEnemyPassiveSkills(IReadOnlyList<EnemyPassiveSkillModifier> modifiers)
        {
            Initialize();

            _passiveController.RemoveEnemyPassiveSkills(modifiers);
        }


        public IReadOnlyList<PassiveSkillData> GetEnemyPassiveSkills(
            EnemyUnitClass unitClass,
            EnemyUnitType unitType,
            EnemyUnitFaction unitFaction)
        {
            Initialize();

            return _passiveController.GetEnemyPassiveSkills(unitClass, unitType, unitFaction);
        }


        public void RemovePassiveSkillsBySource(object source)
        {
            Initialize();

            _passiveController.RemoveBySource(source);
        }


        // ============================================================
        // Private Methods
        // ============================================================

        private void Initialize()
        {
            if (_organizer != null)
                return;

            _passiveController = new PassiveSkillModifierController();

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
