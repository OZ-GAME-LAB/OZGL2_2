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

        private readonly System.Action<SkillFXRequest> _fxRequested;


        // ============================================================
        // Constructor
        // ============================================================

        public PassiveEffectExecutor(
            Unit_Core core,
            TargetResolver targetResolver,
            System.Action<SkillFXRequest> fxRequested = null)
        {
            _core = core;

            _targetResolver = targetResolver;

            _fxRequested = fxRequested;
        }


        // ============================================================
        // Activate
        // ============================================================

        // 조건이 처음 만족되었을 때 지속형 Action을 적용한다.
        public void Activate(
            RuntimePassiveSkill runtimePassive,
            PassiveContext context)
        {
            ActivateWithResult(runtimePassive, context);
        }

        public bool ActivateWithResult(
            RuntimePassiveSkill runtimePassive,
            PassiveContext context)
        {
            if (!CanExecute(runtimePassive))
                return false;

            bool applied = false;

            var owner = new CombatTargetSnapshot(context.Owner);

            foreach (var action in runtimePassive.Data.Actions)
            {
                if (!owner.IsTargetable || runtimePassive.IsStopped)
                    break;

                if (action is PassiveStatModifierActionData modifier && modifier.Value != 0f)
                {
                    ApplyStatModifier(runtimePassive, modifier);

                    applied = true;
                }
            }

            return applied;
        }


        // ============================================================
        // Execute
        // ============================================================

        // Trigger가 발생하고 조건을 만족할 때 실행형 Action을 처리한다.
        public void Execute(
            RuntimePassiveSkill runtimePassive,
            PassiveContext context)
        {
            ExecuteWithResult(runtimePassive, context);
        }

        public bool ExecuteWithResult(
            RuntimePassiveSkill runtimePassive,
            PassiveContext context)
        {
            if (!CanExecute(runtimePassive))
                return false;

            bool applied = false;

            var owner = new CombatTargetSnapshot(context.Owner);

            foreach (var action in runtimePassive.Data.Actions)
            {
                if (!owner.IsTargetable || runtimePassive.IsStopped)
                    break;

                if (action is PassiveEffectActionData effect)
                    applied |= ExecuteEffectAction(effect, context);

                else if (action is PassiveAdditionalAttackActionData additional && !context.Metadata.IsAdditionalAttack)
                    applied |= ExecuteAdditionalAttack(
                        runtimePassive,
                        additional,
                        context
                    );

            // PassiveDamageModifierActionData는
            // DamageResolver의 계산 단계에서 처리한다.
            }

            return applied;
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

            var parent = CombatEventContext.Current.EventId != 0 ? CombatEventContext.Current : runtimePassive.ActivationMetadata;

            var metadata = CombatEventMetadata.Create(
                context.Owner,
                parent,
                additionalAttack: runtimePassive.ActivationMetadata.IsAdditionalAttack
            );

            using (CombatEventContext.Enter(metadata))
                RemoveStatModifiers(runtimePassive);
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

            CombatStatModifier modifier = new CombatStatModifier(
                runtimePassive,
                action.StatType,
                action.ModifierType,
                action.Value
            );

            _core.RuntimeStatus.AddCombatModifier(modifier);
        }

        private void RemoveStatModifiers(RuntimePassiveSkill runtimePassive)
        {
            if (_core == null || _core.RuntimeStatus == null)
            {
                return;
            }

            _core.RuntimeStatus.RemoveCombatModifiers(runtimePassive);
        }


        // ============================================================
        // Effect Action
        // ============================================================

        private bool ExecuteAdditionalAttack(
            RuntimePassiveSkill runtime,
            PassiveAdditionalAttackActionData data,
            PassiveContext context)
        {
            var owner = new CombatTargetSnapshot(context.Owner);

            if (!owner.IsTargetable || data.Attack == null || !data.Attack.IsConfigured || context.Metadata.IsAdditionalAttack)
                return false;

            var action = SkillDefinitionCopy.Copy(data.Attack);

            var entries = new List<SkillEffectEntry>(action.BaseEffects);

            entries.AddRange(action.ConditionalEffects);

            var ids = new HashSet<int>();

            int id = 1;

            foreach (var entry in entries)
                if (entry != null)
                    id = UnityEngine.Mathf.Max(id, entry.EntryId + 1);

            foreach (var entry in entries)
                if (entry != null && (entry.EntryId <= 0 || !ids.Add(entry.EntryId)))
                {
                    entry.SetEntryId(id++);

                    ids.Add(entry.EntryId);
                }

            var origin = data.UseTriggerPosition && context.TargetSnapshot.ObjectId != 0 ? context.TargetSnapshot.Position : owner.Position;

            var direction = context.TargetSnapshot.ObjectId != 0 ? (context.TargetSnapshot.Position - owner.Position).normalized : context.Owner.FacingDirection;

            var settings = action.Target;

            var selected = _targetResolver.ResolveSkillTargets(new SkillTargetRequest(context.Owner, settings, origin, direction, initialTarget: context.TargetSnapshot, currentTarget: context.TargetSnapshot, triggerTarget: context.TargetSnapshot));

            if (!selected.Success)
                return false;

            var root = CombatEventMetadata.Create(
                context.Owner,
                context.Metadata,
                additionalAttack: true
            );

            var metadata = CombatEventMetadata.Create(
                context.Owner,
                root,
                executionId: root.EventId,
                actionIndex: 0,
                impactId: root.EventId
            );

            var batch = new SkillEffectBatch(
                action,
                new SkillEffectLedger(),
                null,
                fxRequested: _fxRequested,
                canContinue: () => owner.IsTargetable && !runtime.IsStopped,
                isActiveSkill: false
            );

            bool applied = false;

            void FX(SkillFXHook hook, bool cleanup = false, CombatTargetSnapshot target = default)
            {
                foreach (var entry in action.FXEntries)
                    if (entry != null && (cleanup || entry.Hook == hook))
                        _fxRequested?.Invoke(new SkillFXRequest(entry, metadata,
                            hook == SkillFXHook.OnHit && target.ObjectId != 0 ? target.Position : origin, cleanup,
                            target.ObjectId != 0 ? target.Position - origin : direction,
                            target.ObjectId != 0 ? target : selected.PrimaryTarget));
            }

            void Timing(SkillEffectTiming timing)
            {
                if (!owner.IsTargetable || runtime.IsStopped)
                    return;

                foreach (var result in SkillEffectPipeline.Resolve(
                    batch,
                    new SkillConditionContext(context.Owner, selected.PrimaryTarget, _targetResolver, metadata, position: origin),
                    timing
                ))
                    applied |= result.WasApplied;
            }

            using (CombatEventContext.Enter(metadata))
            {
                try
                {
                    FX(SkillFXHook.OnStart);

                    Timing(SkillEffectTiming.OnStart);

                    if (!owner.IsTargetable || runtime.IsStopped)
                        return applied;

                    if (action.Delivery == ActiveSkillDeliveryType.Projectile)
                    {
                        foreach (var target in selected.Targets)
                        {
                            if (!owner.IsTargetable || runtime.IsStopped)
                                break;

                            if (!target.IsTargetable)
                                continue;

                            runtime.BeginPending();

                            var flight = new ProjectileFlightState(_ => runtime.EndPending(), () =>
                            {
                                if (!runtime.IsStopped && owner.IsTargetable && runtime.Data.EffectMode == PassiveSkillEffectMode.Once)
                                    runtime.MarkExecutedOnce();
                            });

                            try
                            {
                                bool fired = SkillAttackDelivery.Fire(
                                    context.Owner,
                                    target.Target,
                                    origin,
                                    action,
                                    batch,
                                    CombatEventMetadata.Create(context.Owner, metadata, impactId: CombatEventMetadata.Create(context.Owner).EventId),
                                    flight: flight
                                );

                                if (fired)
                                    FX(SkillFXHook.Fire, target: target);
                            }
                            catch
                            {
                                flight.Complete();

                                throw;
                            }
                        }

                        Timing(SkillEffectTiming.OnComplete);

                        if (owner.IsTargetable && !runtime.IsStopped)
                            FX(SkillFXHook.OnComplete);

                        return applied; // Fire만으로 Applied/Once를 소비하지 않는다.
                    }

                    var targets = new List<CombatTargetSnapshot>();

                    if (action.Area == ActiveSkillAreaType.Single)
                        targets.Add(selected.PrimaryTarget);

                    else
                    {
                        var center = action.Area == ActiveSkillAreaType.TargetCircle && !data.UseTriggerPosition ? selected.PrimaryTarget.Position : origin;

                        var team = settings.Relation == SkillTargetRelation.Hostile ? (owner.Team == UnitTeam.Ally ? UnitTeam.Enemy : UnitTeam.Ally) : owner.Team;

                        foreach (var target in _targetResolver.ResolveHitTargets(new TargetHitRequest(center, direction, action.Radius, action.Angle, action.MaxEffectTargets, action.Area == ActiveSkillAreaType.SelfCone ? HitAreaType.Cone : HitAreaType.Circle, team)))
                        {
                            if (settings.Relation == SkillTargetRelation.Self && !ReferenceEquals(target, context.Owner))
                                continue;

                            if (settings.Relation == SkillTargetRelation.Friendly && !settings.IncludeSelf && ReferenceEquals(target, context.Owner))
                                continue;

                            targets.Add(new CombatTargetSnapshot(target));
                        }
                    }

                    foreach (var target in targets)
                    {
                        if (!owner.IsTargetable || runtime.IsStopped)
                            break;

                        if (!target.IsTargetable)
                            continue;

                        foreach (var result in SkillAttackDelivery.Hit(batch, new SkillConditionContext(context.Owner, target, _targetResolver, metadata, position: origin)))
                            applied |= result.WasApplied;

                        FX(SkillFXHook.OnHit, target: target);
                    }

                    Timing(SkillEffectTiming.OnComplete);

                    if (owner.IsTargetable && !runtime.IsStopped)
                        FX(SkillFXHook.OnComplete);
                }
                finally
                {
                    batch.CleanupConditionalFX();

                    FX(SkillFXHook.OnComplete, cleanup: true);
                }
            }

            return applied;
        }

        private bool ExecuteEffectAction(
            PassiveEffectActionData action,
            PassiveContext context)
        {
            if (action.Effects == null || action.Effects.Count == 0)
                return false;

            return action.TargetType switch
            {
                PassiveSkillTargetType.Self => ExecuteSelfEffect(action, context),
                PassiveSkillTargetType.TriggerTarget => ExecuteTriggerTargetEffect(action, context),
                PassiveSkillTargetType.Search => ExecuteTargetEffects(action, context),
                _ => false
            };
        }


        // ============================================================
        // Self Effect
        // ============================================================

        private bool ExecuteSelfEffect(
            PassiveEffectActionData action,
            PassiveContext context)
        {
            return CombatTargetUtility.IsValid(context.Owner) && ApplyEffects(context.Owner, action.Effects);
        }


        // ============================================================
        // Target Effect
        // ============================================================

        private bool ExecuteTargetEffects(
            PassiveEffectActionData action,
            PassiveContext context)
        {
            var owner = new CombatTargetSnapshot(context.Owner);

            if (!owner.IsTargetable || _targetResolver == null)
                return false;

            var targets = _targetResolver.ResolveCandidates(new TargetCandidateRequest(owner.Position, action.AreaRadius, GetTargetTeam(owner.Team, action.TargetRelation)));

            var snapshots = new List<CombatTargetSnapshot>();

            foreach (var target in targets)
                snapshots.Add(new CombatTargetSnapshot(target));

            int count = 0;

            bool applied = false;

            foreach (var target in snapshots)
            {
                if (!owner.IsTargetable)
                    break;

                if (!target.IsTargetable || !CanApplyTarget(
                    action,
                    context,
                    target.Target
                ))
                    continue;

                applied |= ApplyEffects(target.Target, action.Effects);

                if (++count >= GetMaxTargetCount(action))
                    break;
            }

            return applied;
        }


        // ============================================================
        // Trigger Target Effect
        // ============================================================

        private bool ExecuteTriggerTargetEffect(
            PassiveEffectActionData action,
            PassiveContext context)
        {
            return CombatTargetUtility.IsValid(context.Target) && ApplyEffects(context.Target, action.Effects);
        }


        // ============================================================
        // Target Validation
        // ============================================================

        private bool CanApplyTarget(
            PassiveEffectActionData action,
            PassiveContext context,
            ICombatTarget target)
        {
            if (target == null || target.Transform == null || !target.IsTargetable)
            {
                return false;
            }

            // Friendly 범위 효과에서는 자신을 제외한다.
            if (action.TargetRelation == SkillTargetRelation.Friendly && ReferenceEquals(target, context.Owner))
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
            if (owner == null || owner.Transform == null || target == null || target.Transform == null)
            {
                return false;
            }

            UnityEngine.Vector2 origin = owner.Transform.position;

            UnityEngine.Vector2 forward = owner.FacingDirection;

            UnityEngine.Vector2 targetDirection = (UnityEngine.Vector2)target.Transform.position - origin;

            if (targetDirection.sqrMagnitude <= 0f)
                return true;

            float targetAngle = UnityEngine.Vector2.Angle(forward, targetDirection);

            return targetAngle <= angle * 0.5f;
        }

        private int GetMaxTargetCount(PassiveEffectActionData action)
        {
            if (action.AreaType == PassiveSkillAreaType.Single)
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
                    return ownerTeam == UnitTeam.Ally ? UnitTeam.Enemy : UnitTeam.Ally;

                default:
                    return ownerTeam;
            }
        }


        // ============================================================
        // Skill Effect
        // ============================================================

        private bool ApplyEffects(
            ICombatTarget target,
            IReadOnlyList<SkillEffectData> effects)
        {
            if (SkillEffectResolver.Instance == null)
                return false;

            bool applied = false;

            foreach (var result in SkillEffectResolver.Instance.ResolveWithResults(new SkillEffectRequest(_core.CombatTarget, target, effects)))
                applied |= result.WasApplied;

            return applied;
        }


        // ============================================================
        // Validation
        // ============================================================

        private bool CanExecute(RuntimePassiveSkill runtimePassive)
        {
            return _core != null && runtimePassive != null && runtimePassive.Data != null;
        }
    }
}
