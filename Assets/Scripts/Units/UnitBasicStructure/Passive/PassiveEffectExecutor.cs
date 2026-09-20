using System.Collections.Generic;



namespace Units.Skills
{
    public class PassiveEffectExecutor
    {
        // ============================================================
        // References
        // ============================================================

        private readonly Unit_Core _core;

        private readonly TargetResolver _targetResolver;


        // ============================================================
        // Constructor
        // ============================================================

        public PassiveEffectExecutor(
            Unit_Core core,
            TargetResolver targetResolver)
        {
            _core =
                core;

            _targetResolver =
                targetResolver;
        }


        // ============================================================
        // Activate
        // ============================================================

        // 조건이 처음 만족되었을 때 지속형 Action을 적용한다.
        public void Activate(
            RuntimePassiveSkill runtimePassive,
            PassiveContext context)
        {
            if (!CanExecute(
                    runtimePassive))
            {
                return;
            }

            IReadOnlyList<PassiveSkillActionData> actions =
                runtimePassive.Data.Actions;

            for (int i = 0;
                 i < actions.Count;
                 i++)
            {
                PassiveSkillActionData action =
                    actions[i];

                if (action == null)
                    continue;

                if (action
                    is PassiveStatModifierActionData statModifierAction)
                {
                    ApplyStatModifier(
                        runtimePassive,
                        statModifierAction
                    );
                }
            }
        }


        // ============================================================
        // Execute
        // ============================================================

        // Trigger가 발생하고 조건을 만족할 때 실행형 Action을 처리한다.
        public void Execute(
            RuntimePassiveSkill runtimePassive,
            PassiveContext context)
        {
            if (!CanExecute(
                    runtimePassive))
            {
                return;
            }

            IReadOnlyList<PassiveSkillActionData> actions =
                runtimePassive.Data.Actions;

            for (int i = 0;
                 i < actions.Count;
                 i++)
            {
                PassiveSkillActionData action =
                    actions[i];

                if (action == null)
                    continue;

                if (action
                    is PassiveEffectActionData effectAction)
                {
                    ExecuteEffectAction(
                        effectAction,
                        context
                    );
                }

                // PassiveDamageModifierActionData는
                // DamageResolver의 계산 단계에서 처리한다.
            }
        }


        // ============================================================
        // Deactivate
        // ============================================================

        // 조건이 더 이상 만족되지 않을 때 지속형 Action을 해제한다.
        public void Deactivate(
            RuntimePassiveSkill runtimePassive,
            PassiveContext context)
        {
            if (runtimePassive == null)
                return;

            RemoveStatModifiers(
                runtimePassive
            );
        }


        // ============================================================
        // Stat Modifier
        // ============================================================

        private void ApplyStatModifier(
            RuntimePassiveSkill runtimePassive,
            PassiveStatModifierActionData action)
        {
            if (_core.RuntimeStatus == null)
                return;

            CombatStatModifier modifier =
                new CombatStatModifier(
                    runtimePassive,
                    action.StatType,
                    action.ModifierType,
                    action.Value
                );

            _core.RuntimeStatus.AddCombatModifier(
                modifier
            );
        }


        private void RemoveStatModifiers(
            RuntimePassiveSkill runtimePassive)
        {
            if (_core == null ||
                _core.RuntimeStatus == null)
            {
                return;
            }

            _core.RuntimeStatus.RemoveCombatModifiers(
                runtimePassive
            );
        }


        // ============================================================
        // Effect Action
        // ============================================================

        private void ExecuteEffectAction(
            PassiveEffectActionData action,
            PassiveContext context)
        {
            if (action.Effects == null ||
                action.Effects.Count == 0)
            {
                return;
            }

            switch (action.TargetType)
            {
                case PassiveSkillTargetType.Self:

                    ExecuteSelfEffect(
                        action,
                        context
                    );

                    break;


                case PassiveSkillTargetType.TriggerTarget:

                    ExecuteTriggerTargetEffect(
                        action,
                        context
                    );

                    break;


                case PassiveSkillTargetType.Search:

                    ExecuteTargetEffects(
                        action,
                        context
                    );

                    break;
            }
        }


        // ============================================================
        // Self Effect
        // ============================================================

        private void ExecuteSelfEffect(
            PassiveEffectActionData action,
            PassiveContext context)
        {
            ICombatTarget owner =
                context.Owner;

            if (owner == null ||
                !owner.IsTargetable)
            {
                return;
            }

            ApplyEffects(
                owner,
                action.Effects
            );
        }


        // ============================================================
        // Target Effect
        // ============================================================

        private void ExecuteTargetEffects(
            PassiveEffectActionData action,
            PassiveContext context)
        {
            ICombatTarget owner =
                context.Owner;

            if (owner == null ||
                owner.Transform == null ||
                _targetResolver == null)
            {
                return;
            }

            UnitTeam targetTeam =
                GetTargetTeam(
                    owner.Team,
                    action.TargetRelation
                );

            IReadOnlyList<ICombatTarget> targets =
                _targetResolver.ResolveCandidates(
                    new TargetCandidateRequest(
                        owner.Transform.position,
                        action.AreaRadius,
                        targetTeam
                    )
                );

            int maxTargetCount =
                GetMaxTargetCount(
                    action
                );

            int appliedCount =
                0;

            for (int i = 0;
                 i < targets.Count;
                 i++)
            {
                ICombatTarget target =
                    targets[i];

                if (!CanApplyTarget(
                        action,
                        context,
                        target))
                {
                    continue;
                }

                ApplyEffects(
                    target,
                    action.Effects
                );

                appliedCount++;

                if (appliedCount >=
                    maxTargetCount)
                {
                    break;
                }
            }
        }


        // ============================================================
        // Trigger Target Effect
        // ============================================================

        private void ExecuteTriggerTargetEffect(
            PassiveEffectActionData action,
            PassiveContext context)
        {
            ICombatTarget target =
                context.Target;

            if (target == null ||
                !target.IsTargetable)
            {
                return;
            }

            ApplyEffects(
                target,
                action.Effects
            );
        }


        // ============================================================
        // Target Validation
        // ============================================================

        private bool CanApplyTarget(
            PassiveEffectActionData action,
            PassiveContext context,
            ICombatTarget target)
        {
            if (target == null ||
                target.Transform == null ||
                !target.IsTargetable)
            {
                return false;
            }

            // Friendly 범위 효과에서는 자신을 제외한다.
            if (action.TargetRelation ==
                SkillTargetRelation.Friendly &&
                ReferenceEquals(
                    target,
                    context.Owner))
            {
                return false;
            }

            switch (action.AreaType)
            {
                case PassiveSkillAreaType.Single:

                    return true;


                case PassiveSkillAreaType.Circle:

                    return true;


                case PassiveSkillAreaType.Cone:

                    return IsInsideCone(
                        context.Owner,
                        target,
                        action.AreaAngle
                    );


                default:

                    return false;
            }
        }


        private bool IsInsideCone(
            ICombatTarget owner,
            ICombatTarget target,
            float angle)
        {
            if (owner == null ||
                owner.Transform == null ||
                target == null ||
                target.Transform == null)
            {
                return false;
            }

            UnityEngine.Vector2 origin =
                owner.Transform.position;

            UnityEngine.Vector2 forward =
                owner.Transform.right;

            UnityEngine.Vector2 targetDirection =
                (UnityEngine.Vector2)
                target.Transform.position -
                origin;

            if (targetDirection.sqrMagnitude <= 0f)
                return true;

            float targetAngle =
                UnityEngine.Vector2.Angle(
                    forward,
                    targetDirection
                );

            return targetAngle <=
                   angle * 0.5f;
        }


        private int GetMaxTargetCount(
            PassiveEffectActionData action)
        {
            if (action.AreaType ==
                PassiveSkillAreaType.Single)
            {
                return 1;
            }

            return action.MaxEffectTargetCount;
        }


        // ============================================================
        // Team
        // ============================================================

        private UnitTeam GetTargetTeam(
            UnitTeam ownerTeam,
            SkillTargetRelation targetRelation)
        {
            switch (targetRelation)
            {
                case SkillTargetRelation.Friendly:

                    return ownerTeam;


                case SkillTargetRelation.Hostile:

                    return ownerTeam ==
                           UnitTeam.Ally
                        ? UnitTeam.Enemy
                        : UnitTeam.Ally;


                default:

                    return ownerTeam;
            }
        }


        // ============================================================
        // Skill Effect
        // ============================================================

        private void ApplyEffects(
            ICombatTarget target,
            IReadOnlyList<SkillEffectData> effects)
        {
            SkillEffectRequest request =
                new SkillEffectRequest(
                    _core,
                    target,
                    effects
                );

            SkillEffectResolver.Instance.Resolve(
                request
            );
        }


        // ============================================================
        // Validation
        // ============================================================

        private bool CanExecute(
            RuntimePassiveSkill runtimePassive)
        {
            return _core != null
                && runtimePassive != null
                && runtimePassive.Data != null;
        }
    }
}