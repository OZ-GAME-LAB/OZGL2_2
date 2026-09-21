using System.Collections.Generic;


namespace Units
{
    public readonly struct AttackerContext
    {
        // ============================================================
        // Source
        // ============================================================

        public ICombatTarget Attacker { get; }

        public DamageSourceType SourceType { get; }

        public DamageType DamageType { get; }


        // ============================================================
        // Damage
        // ============================================================

        public float AttackPower { get; }

        // 공격 자체가 가진 고유 배율
        public float DamageMultiplier { get; }

        // 기본 공격 / 스킬에 따른 배율
        public float SourceDamageMultiplier { get; }

        // 모든 피해에 적용되는 공통 배율
        public float GeneralDamageMultiplier { get; }


        // ============================================================
        // Defense
        // ============================================================

        public float DefenseIgnore { get; }


        // ============================================================
        // Critical
        // ============================================================

        public float CriticalChance { get; }

        public float CriticalDamage { get; }


        // ============================================================
        // Passive
        // ============================================================

        public IReadOnlyList<PassiveDamageModifier> DamageModifiers
        {
            get;
        }


        // ============================================================
        // Constructor
        // ============================================================

        public AttackerContext(
            ICombatTarget attacker,
            DamageSourceType sourceType,
            DamageType damageType,
            float attackPower,
            float damageMultiplier,
            float sourceDamageMultiplier,
            float generalDamageMultiplier,
            float defenseIgnore,
            float criticalChance,
            float criticalDamage,
            IReadOnlyList<PassiveDamageModifier> damageModifiers)
        {
            Attacker =
                attacker;

            SourceType =
                sourceType;

            DamageType =
                damageType;

            AttackPower =
                attackPower;

            DamageMultiplier =
                damageMultiplier;

            SourceDamageMultiplier =
                sourceDamageMultiplier;

            GeneralDamageMultiplier =
                generalDamageMultiplier;

            DefenseIgnore =
                defenseIgnore;

            CriticalChance =
                criticalChance;

            CriticalDamage =
                criticalDamage;

            DamageModifiers =
                damageModifiers;
        }
    }
}