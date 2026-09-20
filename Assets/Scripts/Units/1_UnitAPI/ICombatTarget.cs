using System.Collections.Generic;
using Units.Skills;
using UnityEngine;



namespace Units
{
    public interface ICombatTarget
    {
        // ============================================================
        // Transform
        // ============================================================

        Transform Transform { get; }


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

        bool IsTargetable { get; }


        // ============================================================
        // Life
        // ============================================================

        float CurrentHp { get; }

        void TakeDamage(
            DamageResult result
        );

        void Heal(
            float amount
        );


        // ============================================================
        // Passive
        // ============================================================

        void CollectDamageModifiers(
            PassiveDamageOwnerType ownerType,
            List<PassiveDamageModifier> results
        );

        bool EvaluateDamageModifierConditions(
            RuntimePassiveSkill runtimePassive,
            ICombatTarget target
        );
    }
}