using System.Collections.Generic;
using Units.Skills;



namespace Units
{
    public class DamageContextBuilder
    {
        // ============================================================
        // Build
        // ============================================================

        public DamageContext Build(
            DamageRequest request,
            ICombatTarget target)
        {
            if (request.Attacker == null ||
                target == null)
            {
                return default;
            }

            AttackerContext attackerContext =
                BuildAttackerContext(
                    request
                );

            TargetContext targetContext =
                BuildTargetContext(
                    target
                );

            return new DamageContext(
                attackerContext,
                targetContext
            );
        }


        // ============================================================
        // Attacker Context
        // ============================================================

        private AttackerContext BuildAttackerContext(
            DamageRequest request)
        {
            Unit_Core attacker =
                request.Attacker;

            Unit_RuntimeStatus runtimeStatus =
                attacker.RuntimeStatus;

            List<PassiveDamageModifier> damageModifiers =
                new();

            attacker.CollectDamageModifiers(
                PassiveDamageOwnerType.Attacker,
                damageModifiers
            );

            float sourceDamageMultiplier =
                GetSourceDamageMultiplier(
                    runtimeStatus,
                    request.SourceType
                );

            return new AttackerContext(
                attacker,
                request.SourceType,
                request.DamageType,
                runtimeStatus.AttackPower,
                request.DamageMultiplier,
                sourceDamageMultiplier,
                runtimeStatus.DamageMultiplier,
                runtimeStatus.DefenseIgnore,
                runtimeStatus.CriticalChance,
                runtimeStatus.CriticalDamage,
                damageModifiers
            );
        }


        // ============================================================
        // Target Context
        // ============================================================

        private TargetContext BuildTargetContext(
            ICombatTarget target)
        {
            Unit_RuntimeStatus runtimeStatus =
                target.RuntimeStatus;

            List<PassiveDamageModifier> damageModifiers =
                new();

            target.CollectDamageModifiers(
                PassiveDamageOwnerType.Target,
                damageModifiers
            );

            return new TargetContext(
                target,
                runtimeStatus.Defense,
                runtimeStatus.DamageTakenMultiplier,
                damageModifiers
            );
        }


        // ============================================================
        // Source Damage Multiplier
        // ============================================================

        private float GetSourceDamageMultiplier(
            Unit_RuntimeStatus runtimeStatus,
            DamageSourceType sourceType)
        {
            if (runtimeStatus == null)
                return 1f;

            switch (sourceType)
            {
                case DamageSourceType.BasicAttack:

                    return runtimeStatus
                        .BasicAttackMultiplier;


                case DamageSourceType.Skill:

                    return runtimeStatus
                        .SkillDamageMultiplier;


                default:

                    return 1f;
            }
        }
    }
}