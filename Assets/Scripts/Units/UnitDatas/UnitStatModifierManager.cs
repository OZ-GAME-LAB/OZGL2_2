using System.Collections.Generic;
using Units.Skills;
using UnityEngine;



namespace Units
{
    public class UnitStatModifierManager : MonoBehaviour, IUnitStatModifierRegister
    {
        // ============================================================
        // References
        // ============================================================

        [SerializeField]
        private EffectManager _effectManager;


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
        // Spawn Artifact Methods
        // ============================================================

        // 진영별 스폰 시작 시 한 번 호출하여 이전 아티펙트분만 최신화한다.
        public bool RefreshAllyArtifactEffects()
        {
            Initialize();

            if (!TryResolveEffectManager())
                return false;

            // 제거 전에 현재 목록을 확보한다. 현재 보유하지 않은 출처도 아래에서 정리한다.
            var statModifiers = new List<AllyStatModifier>();
            var passiveModifiers = new List<AllyPassiveSkillModifier>();

            foreach (AllyStatModifier modifier in _effectManager.AllyModifiers)
            {
                if (modifier.Source is ArtifactInstance)
                    statModifiers.Add(modifier);
            }

            foreach (AllyPassiveSkillModifier modifier in _effectManager.AllyPassiveSkillModifiers)
            {
                if (modifier.Source is ArtifactInstance)
                    passiveModifiers.Add(modifier);
            }

            _organizer.RemoveAllyArtifactModifiers();
            _passiveController.RemoveAllyArtifactPassiveSkills();
            _organizer.AddAllyModifiers(statModifiers);
            _passiveController.AddAllyPassiveSkills(passiveModifiers);
            return true;
        }


        // 진영별 스폰 시작 시 한 번 호출하여 이전 아티펙트분만 최신화한다.
        public bool RefreshEnemyArtifactEffects()
        {
            Initialize();

            if (!TryResolveEffectManager())
                return false;

            // 제거 전에 현재 목록을 확보한다. 현재 보유하지 않은 출처도 아래에서 정리한다.
            var statModifiers = new List<EnemyStatModifier>();
            var passiveModifiers = new List<EnemyPassiveSkillModifier>();

            foreach (EnemyStatModifier modifier in _effectManager.EnemyModifiers)
            {
                if (modifier.Source is ArtifactInstance)
                    statModifiers.Add(modifier);
            }

            foreach (EnemyPassiveSkillModifier modifier in _effectManager.EnemyPassiveSkillModifiers)
            {
                if (modifier.Source is ArtifactInstance)
                    passiveModifiers.Add(modifier);
            }

            _organizer.RemoveEnemyArtifactModifiers();
            _passiveController.RemoveEnemyArtifactPassiveSkills();
            _organizer.AddEnemyModifiers(statModifiers);
            _passiveController.AddEnemyPassiveSkills(passiveModifiers);
            return true;
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

        private bool TryResolveEffectManager()
        {
            // 기존 씬도 동작하도록 같은 씬의 참조를 최초 사용 시 찾고 재사용한다.
            if (_effectManager == null)
            {
                foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                {
                    _effectManager = root.GetComponentInChildren<EffectManager>(true);
                    if (_effectManager != null)
                        break;
                }
            }

            if (_effectManager != null)
                return true;

            Debug.LogError(
                "[UnitStatModifierManager] EffectManager가 없어 아티펙트 효과를 최신화하지 못했습니다.",
                this
            );
            return false;
        }


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
