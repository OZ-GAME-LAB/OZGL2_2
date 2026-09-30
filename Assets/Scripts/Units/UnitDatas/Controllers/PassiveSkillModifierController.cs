using System.Collections.Generic;
using Units.Skills;


namespace Units
{
    // 외부 패시브 등록을 관리하고 스폰별 독립 목록을 만든다. 기존 유닛에는 전파하지 않는다.
    public class PassiveSkillModifierController
    {
        // ============================================================
        // Data
        // ============================================================

        private readonly List<AllyPassiveSkillModifier> _allyModifiers = new();

        private readonly List<EnemyPassiveSkillModifier> _enemyModifiers = new();


        // ============================================================
        // Public Methods
        // ============================================================

        public void AddAllyPassiveSkill(AllyPassiveSkillModifier modifier)
        {
            if (modifier.PassiveSkill == null || !IsSupportedAllyType(modifier.ApplyType))
                return;

            if (_allyModifiers.Contains(modifier))
                return;

            _allyModifiers.Add(modifier);
        }


        public void AddAllyPassiveSkills(IReadOnlyList<AllyPassiveSkillModifier> modifiers)
        {
            if (modifiers == null)
                return;

            for (int i = 0; i < modifiers.Count; i++)
            {
                AddAllyPassiveSkill(modifiers[i]);
            }
        }


        public void RemoveAllyPassiveSkill(AllyPassiveSkillModifier modifier)
        {
            _allyModifiers.Remove(modifier);
        }


        public void RemoveAllyPassiveSkills(IReadOnlyList<AllyPassiveSkillModifier> modifiers)
        {
            if (modifiers == null)
                return;

            for (int i = 0; i < modifiers.Count; i++)
            {
                RemoveAllyPassiveSkill(modifiers[i]);
            }
        }


        public IReadOnlyList<PassiveSkillData> GetAllyPassiveSkills(
            AllyUnitClass unitClass,
            AllyUnitType unitType,
            AllyUnitTier unitTier)
        {
            List<PassiveSkillData> result = new();

            HashSet<PassiveSkillData> included = new();

            foreach (AllyPassiveSkillModifier modifier in _allyModifiers)
            {
                bool matches = modifier.ApplyType switch
                {
                    UnitModifierApplyType.All => true,
                    UnitModifierApplyType.Class => modifier.TargetClass == unitClass,
                    UnitModifierApplyType.Type => modifier.TargetUnitType == unitType,
                    UnitModifierApplyType.Tier => modifier.TargetTier == unitTier,
                    _ => false
                };

                if (matches && modifier.PassiveSkill != null && included.Add(modifier.PassiveSkill))
                {
                    result.Add(modifier.PassiveSkill);
                }
            }

            return result.AsReadOnly();
        }


        public void AddEnemyPassiveSkill(EnemyPassiveSkillModifier modifier)
        {
            if (modifier.PassiveSkill == null || !IsSupportedEnemyType(modifier.ApplyType))
                return;

            if (_enemyModifiers.Contains(modifier))
                return;

            _enemyModifiers.Add(modifier);
        }


        public void AddEnemyPassiveSkills(IReadOnlyList<EnemyPassiveSkillModifier> modifiers)
        {
            if (modifiers == null)
                return;

            for (int i = 0; i < modifiers.Count; i++)
            {
                AddEnemyPassiveSkill(modifiers[i]);
            }
        }


        public void RemoveEnemyPassiveSkill(EnemyPassiveSkillModifier modifier)
        {
            _enemyModifiers.Remove(modifier);
        }


        public void RemoveEnemyPassiveSkills(IReadOnlyList<EnemyPassiveSkillModifier> modifiers)
        {
            if (modifiers == null)
                return;

            for (int i = 0; i < modifiers.Count; i++)
            {
                RemoveEnemyPassiveSkill(modifiers[i]);
            }
        }


        public IReadOnlyList<PassiveSkillData> GetEnemyPassiveSkills(
            EnemyUnitClass unitClass,
            EnemyUnitType unitType,
            EnemyUnitFaction unitFaction)
        {
            List<PassiveSkillData> result = new();

            HashSet<PassiveSkillData> included = new();

            foreach (EnemyPassiveSkillModifier modifier in _enemyModifiers)
            {
                bool matches = modifier.ApplyType switch
                {
                    UnitModifierApplyType.All => true,
                    UnitModifierApplyType.Class => modifier.TargetClass == unitClass,
                    UnitModifierApplyType.Type => modifier.TargetUnitType == unitType,
                    UnitModifierApplyType.Faction => modifier.TargetFaction == unitFaction,
                    _ => false
                };

                if (matches && modifier.PassiveSkill != null && included.Add(modifier.PassiveSkill))
                {
                    result.Add(modifier.PassiveSkill);
                }
            }

            return result.AsReadOnly();
        }


        public void RemoveBySource(object source)
        {
            _allyModifiers.RemoveAll(modifier => Equals(modifier.Source, source));

            _enemyModifiers.RemoveAll(modifier => Equals(modifier.Source, source));
        }


        // ============================================================
        // Private Methods
        // ============================================================

        private static bool IsSupportedAllyType(UnitModifierApplyType applyType)
        {
            return applyType == UnitModifierApplyType.All
                || applyType == UnitModifierApplyType.Class
                || applyType == UnitModifierApplyType.Type
                || applyType == UnitModifierApplyType.Tier;
        }


        private static bool IsSupportedEnemyType(UnitModifierApplyType applyType)
        {
            return applyType == UnitModifierApplyType.All
                || applyType == UnitModifierApplyType.Class
                || applyType == UnitModifierApplyType.Type
                || applyType == UnitModifierApplyType.Faction;
        }
    }
}
