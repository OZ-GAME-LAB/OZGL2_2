using System.Collections.Generic;
using Units.Skills;
using UnityEngine;



namespace Units
{
    public interface ICombatTarget
    {

        event System.Action<CombatStateChange> CombatStateChanged;

        void NotifySkillEvent(CombatSkillEvent notification);

        // ============================================================
        // Transform
        // ============================================================

        Transform Transform { get; }

        // 외형 반전과 독립적으로 유지하는 월드 좌우 방향이다.
        Vector2 FacingDirection { get; }


        // ============================================================
        // Component
        // ============================================================

        Unit_RuntimeStatus RuntimeStatus { get; }


        // ============================================================
        // Team
        // ============================================================

        UnitTeam Team { get; }


        // ============================================================
        // State
        // ============================================================

        bool IsAlive { get; }

        bool IsTargetable { get; }

        int LifetimeVersion { get; }


        // ============================================================
        // Life
        // ============================================================

        float CurrentHp { get; }

        float CurrentShield { get; }

        CombatApplicationResult TakeDamageWithResult(DamageResult result);

        CombatApplicationResult HealWithResult(
            float amount,
            CombatEventMetadata metadata);

        CombatApplicationResult AddShieldWithResult(
            float amount,
            CombatEventMetadata metadata);

        bool TryConsumeEffectStacks(Units.Effects.EffectStackConsumeRequest request);

        void TakeDamage(DamageResult result);

        void Heal(float amount);

        void AddShield(float amount);


        // ============================================================
        // Passive
        // ============================================================

        void CollectDamageModifiers(
            PassiveDamageOwnerType ownerType,
            List<PassiveDamageModifier> results);

        bool EvaluateDamageModifierConditions(
            RuntimePassiveSkill runtimePassive,
            ICombatTarget target,
            CombatSourceSnapshot frozenTarget = null);

        void NotifyDamageDealt(ICombatTarget target);
    }
}