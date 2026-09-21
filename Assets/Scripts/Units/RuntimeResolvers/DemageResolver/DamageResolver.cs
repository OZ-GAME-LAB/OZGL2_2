using System.Collections.Generic;
using Units.Skills;
using UnityEngine;



namespace Units
{
    public class DamageResolver : MonoBehaviour
    {
        // ============================================================
        // Singleton
        // ============================================================

        public static DamageResolver Instance { get; private set; }


        // ============================================================
        // References
        // ============================================================

        private readonly DamageContextBuilder _contextBuilder =
            new();


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Awake()
        {
            if (Instance != null
                && Instance != this)
            {
                Debug.LogError(
                    "[DamageResolver] DamageResolver가 중복 생성되어 자동 삭제됩니다."
                );

                Destroy(
                    gameObject
                );

                return;
            }

            Instance =
                this;
        }


        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance =
                    null;
            }
        }


        // ============================================================
        // Resolve
        // ============================================================

        public void Resolve(
            DamageRequest request)
        {
            if (!IsValidRequest(
                    request))
            {
                return;
            }

            for (int i = 0;
                 i < request.Targets.Count;
                 i++)
            {
                ICombatTarget target =
                    request.Targets[i];

                if (!IsValidTarget(
                        request,
                        target))
                {
                    continue;
                }

                DamageContext damageContext =
                    _contextBuilder.Build(
                        request,
                        target
                    );

                float damage =
                    CalculateDamage(
                        damageContext,
                        out bool isCritical
                    );

                if (damage <= 0f)
                    continue;

                ApplyDamage(
                    damageContext,
                    damage,
                    isCritical
                );
            }
        }


        public void ResolveDotDamage(
            DotDamageRequest request)
        {
            if (!IsValidDotDamageRequest(
                    request))
            {
                return;
            }

            DamageResult result =
                new DamageResult(
                    request.Attacker,
                    request.Target,
                    request.Damage,
                    request.SourceType,
                    false
                );

            request.Target.TakeDamage(
                result
            );
        }


        // ============================================================
        // Validation
        // ============================================================

        private bool IsValidRequest(
            DamageRequest request)
        {
            if (request.Attacker == null)
                return false;

            if (request.Attacker.RuntimeStatus == null)
                return false;

            if (request.Targets == null)
                return false;

            if (request.Targets.Count <= 0)
                return false;

            if (request.DamageMultiplier <= 0f)
                return false;

            return true;
        }


        private bool IsValidTarget(
            DamageRequest request,
            ICombatTarget target)
        {
            if (target == null)
                return false;

            if (!target.IsTargetable)
                return false;

            if (target.Team ==
                request.Attacker.Team)
            {
                return false;
            }

            if (target.RuntimeStatus == null)
                return false;

            return true;
        }


        private bool IsValidDotDamageRequest(
            DotDamageRequest request)
        {
            if (request.Attacker == null)
                return false;

            if (request.Target == null)
                return false;

            if (!request.Target.IsTargetable)
                return false;

            if (request.Target.Team ==
                request.Attacker.Team)
            {
                return false;
            }

            if (request.Damage <= 0f)
                return false;

            return true;
        }


        // ============================================================
        // Damage Calculation
        // ============================================================

        private float CalculateDamage(
            DamageContext context,
            out bool isCritical)
        {
            float damage =
                CalculateBaseDamage(
                    context
                );

            damage =
                CalculateCriticalDamage(
                    context,
                    damage,
                    out isCritical
                );

            damage =
                CalculateDefenseDamage(
                    context,
                    damage
                );


            return Mathf.Max(
                0f,
                damage
            );
        }


        // ============================================================
        // Base Damage
        // ============================================================

        private float CalculateBaseDamage(
            DamageContext context)
        {
            AttackerContext attacker =
                context.Attacker;

            float damage =
                attacker.AttackPower
                * attacker.DamageMultiplier
                * attacker.SourceDamageMultiplier
                * attacker.GeneralDamageMultiplier;

            damage =
                ApplyDamageValueModifiers(
                    context,
                    damage
                );

            return Mathf.Max(
                0f,
                damage
            );
        }


        // ============================================================
        // Critical
        // ============================================================

        private float CalculateCriticalDamage(
            DamageContext context,
            float damage,
            out bool isCritical)
        {
            float criticalChance =
                context.Attacker.CriticalChance;

            float criticalDamage =
                context.Attacker.CriticalDamage;

            bool forceCritical =
                false;

            bool preventCritical =
                false;

            ApplyCriticalModifiers(
                context,
                ref criticalChance,
                ref criticalDamage,
                ref forceCritical,
                ref preventCritical
            );

            criticalChance =
                Mathf.Clamp01(
                    criticalChance
                );

            if (preventCritical)
            {
                isCritical =
                    false;

                return damage;
            }

            if (forceCritical)
            {
                isCritical =
                    true;
            }
            else
            {
                isCritical =
                    Random.value <=
                    criticalChance;
            }

            if (!isCritical)
                return damage;

            return damage
                * criticalDamage;
        }


        // ============================================================
        // Defense
        // ============================================================

        private float CalculateDefenseDamage(
            DamageContext context,
            float damage)
        {
            float defense =
                Mathf.Max(
                    0f,
                    context.Target.Defense
                );

            float defenseIgnore =
                context.Attacker.DefenseIgnore;

            float damageTakenMultiplier =
                context.Target
                    .DamageTakenMultiplier;

            ApplyDefenseModifiers(
                context,
                ref defenseIgnore,
                ref damageTakenMultiplier
            );

            defenseIgnore =
                Mathf.Clamp01(
                    defenseIgnore
                );

            switch (context.Attacker.DamageType)
            {
                case DamageType.Physical:
                    break;

                case DamageType.Magic:

                    defense *=
                        0.5f;

                    break;
            }

            defense *=
                1f - defenseIgnore;

            damage =
                Mathf.Max(
                    0f,
                    damage - defense
                );

            return damage
                * damageTakenMultiplier;
        }


        // ============================================================
        // Damage Modifier
        // ============================================================

        private float ApplyDamageValueModifiers(
            DamageContext context,
            float damage)
        {
            damage =
                ApplyAttackerDamageValueModifiers(
                    context,
                    damage
                );

            damage =
                ApplyTargetDamageValueModifiers(
                    context,
                    damage
                );

            return damage;
        }


        private float ApplyAttackerDamageValueModifiers(
            DamageContext context,
            float damage)
        {
            IReadOnlyList<PassiveDamageModifier> modifiers =
                context.Attacker.DamageModifiers;

            if (modifiers == null)
                return damage;

            for (int i = 0;
                 i < modifiers.Count;
                 i++)
            {
                PassiveDamageModifier modifier =
                    modifiers[i];

                if (modifier.Action.CalculationType !=
                    PassiveDamageCalculationType.Damage)
                {
                    continue;
                }

                if (modifier.Action
                    is not PassiveDamageValueModifierActionData action)
                {
                    continue;
                }

                if (!EvaluateAttackerModifier(
                        context,
                        modifier))
                {
                    continue;
                }

                damage =
                    ApplyValueModifier(
                        damage,
                        action.ModifierType,
                        action.Value
                    );
            }

            return damage;
        }


        private float ApplyTargetDamageValueModifiers(
            DamageContext context,
            float damage)
        {
            IReadOnlyList<PassiveDamageModifier> modifiers =
                context.Target.DamageModifiers;

            if (modifiers == null)
                return damage;

            for (int i = 0;
                 i < modifiers.Count;
                 i++)
            {
                PassiveDamageModifier modifier =
                    modifiers[i];

                if (modifier.Action.CalculationType !=
                    PassiveDamageCalculationType.Damage)
                {
                    continue;
                }

                if (modifier.Action
                    is not PassiveDamageValueModifierActionData action)
                {
                    continue;
                }

                if (!EvaluateTargetModifier(
                        context,
                        modifier))
                {
                    continue;
                }

                damage =
                    ApplyValueModifier(
                        damage,
                        action.ModifierType,
                        action.Value
                    );
            }

            return damage;
        }


        // ============================================================
        // Critical Modifier
        // ============================================================

        private void ApplyCriticalModifiers(
            DamageContext context,
            ref float criticalChance,
            ref float criticalDamage,
            ref bool forceCritical,
            ref bool preventCritical)
        {
            ApplyCriticalModifiers(
                context,
                context.Attacker.DamageModifiers,
                true,
                ref criticalChance,
                ref criticalDamage,
                ref forceCritical,
                ref preventCritical
            );

            ApplyCriticalModifiers(
                context,
                context.Target.DamageModifiers,
                false,
                ref criticalChance,
                ref criticalDamage,
                ref forceCritical,
                ref preventCritical
            );
        }


        private void ApplyCriticalModifiers(
            DamageContext context,
            IReadOnlyList<PassiveDamageModifier> modifiers,
            bool isAttacker,
            ref float criticalChance,
            ref float criticalDamage,
            ref bool forceCritical,
            ref bool preventCritical)
        {
            if (modifiers == null)
                return;

            for (int i = 0;
                 i < modifiers.Count;
                 i++)
            {
                PassiveDamageModifier modifier =
                    modifiers[i];

                if (modifier.Action.CalculationType !=
                    PassiveDamageCalculationType.Critical)
                {
                    continue;
                }

                if (modifier.Action
                    is not PassiveCriticalModifierActionData action)
                {
                    continue;
                }

                bool conditionPassed =
                    isAttacker
                        ? EvaluateAttackerModifier(
                            context,
                            modifier
                        )
                        : EvaluateTargetModifier(
                            context,
                            modifier
                        );

                if (!conditionPassed)
                    continue;

                switch (action.ModifierType)
                {
                    case PassiveCriticalModifierType.Chance:

                        criticalChance +=
                            action.Value;

                        break;


                    case PassiveCriticalModifierType.Damage:

                        criticalDamage +=
                            action.Value;

                        break;


                    case PassiveCriticalModifierType.ForceCritical:

                        forceCritical =
                            true;

                        break;


                    case PassiveCriticalModifierType.PreventCritical:

                        preventCritical =
                            true;

                        break;
                }
            }
        }


        // ============================================================
        // Defense Modifier
        // ============================================================

        private void ApplyDefenseModifiers(
            DamageContext context,
            ref float defenseIgnore,
            ref float damageTakenMultiplier)
        {
            IReadOnlyList<PassiveDamageModifier> attackerModifiers =
                context.Attacker.DamageModifiers;

            if (attackerModifiers != null)
            {
                for (int i = 0;
                     i < attackerModifiers.Count;
                     i++)
                {
                    PassiveDamageModifier modifier =
                        attackerModifiers[i];

                    if (modifier.Action.CalculationType !=
                        PassiveDamageCalculationType.Defense)
                    {
                        continue;
                    }

                    if (modifier.Action
                        is not PassiveDefenseModifierActionData action)
                    {
                        continue;
                    }

                    if (!EvaluateAttackerModifier(
                            context,
                            modifier))
                    {
                        continue;
                    }

                    // 공격자 측 Defense Modifier는
                    // 방어력 관통에 적용한다.
                    defenseIgnore +=
                        action.Value;
                }
            }

            IReadOnlyList<PassiveDamageModifier> targetModifiers =
                context.Target.DamageModifiers;

            if (targetModifiers == null)
                return;

            for (int i = 0;
                 i < targetModifiers.Count;
                 i++)
            {
                PassiveDamageModifier modifier =
                    targetModifiers[i];

                if (modifier.Action.CalculationType !=
                    PassiveDamageCalculationType.Defense)
                {
                    continue;
                }

                if (modifier.Action
                    is not PassiveDefenseModifierActionData action)
                {
                    continue;
                }

                if (!EvaluateTargetModifier(
                        context,
                        modifier))
                {
                    continue;
                }

                // 피격자 측 Defense Modifier는
                // 받는 피해 배율에 적용한다.
                damageTakenMultiplier +=
                    action.Value;
            }
        }


        // ============================================================
        // Modifier Condition
        // ============================================================

        private bool EvaluateAttackerModifier(
            DamageContext context,
            PassiveDamageModifier modifier)
        {
            return context.Attacker.Attacker
                .EvaluateDamageModifierConditions(
                    modifier.RuntimePassive,
                    context.Target.Target
                );
        }


        private bool EvaluateTargetModifier(
            DamageContext context,
            PassiveDamageModifier modifier)
        {
            return context.Target.Target
                .EvaluateDamageModifierConditions(
                    modifier.RuntimePassive,
                    context.Attacker.Attacker
                );
        }


        // ============================================================
        // Value Modifier
        // ============================================================

        private float ApplyValueModifier(
            float value,
            UnitStatModifierType modifierType,
            float modifierValue)
        {
            switch (modifierType)
            {
                case UnitStatModifierType.Flat:

                    return value
                        + modifierValue;


                case UnitStatModifierType.Percent:

                    return value
                        * (1f + modifierValue);


                default:

                    return value;
            }
        }


        // ============================================================
        // Damage Apply
        // ============================================================

        private void ApplyDamage(
            DamageContext context,
            float damage,
            bool isCritical)
        {
            DamageResult result =
                new DamageResult(
                    context.Attacker.Attacker,
                    context.Target.Target,
                    damage,
                    context.Attacker.SourceType,
                    isCritical
                );

            context.Target.Target.TakeDamage(
                result
            );
        }
    }
}