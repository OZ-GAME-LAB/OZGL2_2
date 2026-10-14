using System.Collections.Generic;



namespace Units
{
    public interface IUnitStatModifierRegister
    {
        // ============================================================
        // Stat Modifier
        // ============================================================
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


        // ============================================================
        // Passive Skill Modifier
        // ============================================================

        // 등록 변경은 이후 스폰부터 적용한다. 이미 스폰된 유닛의 패시브는 유지한다.
        void AddBothPassiveSkills(
            IReadOnlyList<AllyPassiveSkillModifier> allyModifiers,
            IReadOnlyList<EnemyPassiveSkillModifier> enemyModifiers);


        void AddAllyPassiveSkill(AllyPassiveSkillModifier modifier);

        void AddAllyPassiveSkills(IReadOnlyList<AllyPassiveSkillModifier> modifiers);

        void RemoveAllyPassiveSkill(AllyPassiveSkillModifier modifier);

        void RemoveAllyPassiveSkills(IReadOnlyList<AllyPassiveSkillModifier> modifiers);

        void AddEnemyPassiveSkill(EnemyPassiveSkillModifier modifier);

        void AddEnemyPassiveSkills(IReadOnlyList<EnemyPassiveSkillModifier> modifiers);

        void RemoveEnemyPassiveSkill(EnemyPassiveSkillModifier modifier);

        void RemoveEnemyPassiveSkills(IReadOnlyList<EnemyPassiveSkillModifier> modifiers);

        void RemovePassiveSkillsBySource(object source);



    }
}