using System;
using System.Collections.Generic;
using Units.Skills;
using Units.Effects;
using UnityEngine;

namespace Units
{
    public class ActiveSkillExecutor
    {
        // ============================================================
        // Reference / Data
        // ============================================================
        private readonly Unit_Core _core;

        private readonly TargetResolver _targetResolver;

        private readonly ActiveSkillData _data;

        // ============================================================
        // Action Controllers
        // ============================================================
        private readonly CastController _castController = new();

        private readonly DashController _dashController;

        // ============================================================
        // Runtime State
        // ============================================================
        private sealed class Execution
        {

            public CombatTargetSnapshot Owner, Initial, Current, Previous;

            public IReadOnlyList<SkillActionData> Actions;

            public SkillTargetResult Targets;

            public SkillEngagementSession Engagement;

            public CombatEventMetadata Metadata;

            public SkillEffectLedger Ledger = new();

            public readonly List<ActionExecutionResult> Results = new();

            public readonly List<ProjectileLaunchResult> Launches = new();

            public readonly List<CombatTargetSnapshot> Hits = new();

            public readonly List<CombatApplicationResult> Applications = new();

            public ActionExecutionResult PreviousResult;

            public Action<SkillExecutionResult> Callback;

            public int Index, Reselections, Successes;

            public bool Waiting, Moved, HitsObserved, Entered, DashStarted;

            public Vector2 Position;

            public SkillEffectBatch Batch;
        }

        private Execution _execution;

        private bool _pumping;

        public SkillExecutionResult LastResult { get; private set; }

        public event Action<SkillFXRequest> FXRequested;

        // ============================================================
        // Properties / Constructor
        // ============================================================
        public bool IsCasting => _castController.IsCasting;

        public bool IsDashing => _dashController.IsDashing;

        public bool IsExecuting => _execution != null;

        public ActiveSkillExecutor(
            Unit_Core core,
            ActiveSkillData data,
            TargetResolver targetResolver)
        {
            _core = core;

            _data = data;

            _targetResolver = targetResolver;

            _dashController = new DashController(core);
        }

        // ============================================================
        // Execute
        // ============================================================
        public bool CanExecute(ICombatTarget target)
        {
            if (_core == null || !_core.IsAlive || !_core.isActiveAndEnabled || _data == null || _core.RuntimeStatus == null || SkillEffectResolver.Instance == null)
                return false;

            var actions = SkillActionPlan.Create(_data);

            if (actions.Count == 0 || actions[0] == null || !actions[0].IsConfigured)
                return false;

            var source = actions[0].Target.Source;

            return source == SkillTargetSource.None || source == SkillTargetSource.Self || source == SkillTargetSource.Search || CombatTargetUtility.IsValid(target);
        }

        public bool TryPrepare(
            ICombatTarget initial,
            SkillEngagementSession engagement,
            out SkillTargetResult targets)
        {
            targets = null;

            if (!CanExecute(initial))
                return false;

            var preview = new Execution
            {
                Owner = new CombatTargetSnapshot(_core.CombatTarget),
                Initial = new CombatTargetSnapshot(initial),
                Current = new CombatTargetSnapshot(initial),
                Engagement = engagement,
                Actions = SkillActionPlan.Create(_data)
            };

            targets = SelectTargets(
                preview,
                preview.Actions[0],
                false
            );

            return targets.Success && preview.Owner.IsTargetable;
        }

        public void Execute(
            ICombatTarget target,
            Action onCompleted,
            Predicate<ICombatTarget> targetFilter = null)
        {
            var session = new SkillEngagementSession(_ => SkillEngagementResult.AlreadyEngaged, () => targetFilter ?? (_ => true));

            if (!TryPrepare(
                target,
                session,
                out var prepared
            ))
            {
                onCompleted?.Invoke();

                return;
            }

            Execute(
                target,
                _ => onCompleted?.Invoke(),
                session,
                prepared
            );
        }

        public void Execute(
            ICombatTarget initial,
            Action<SkillExecutionResult> onCompleted,
            SkillEngagementSession engagement,
            SkillTargetResult prepared)
        {
            Cancel();

            var root = CombatEventMetadata.Create(_core.CombatTarget);

            var run = new Execution
            {
                Owner = new CombatTargetSnapshot(_core.CombatTarget),
                Initial = new CombatTargetSnapshot(initial),
                Current = new CombatTargetSnapshot(initial),
                Actions = SkillActionPlan.Create(_data),
                Engagement = engagement,
                Targets = prepared,
                Callback = onCompleted,
                Metadata = CombatEventMetadata.Create(
                    _core.CombatTarget,
                    root,
                    executionId: root.EventId,
                    actionIndex: 0
                )
            };

            _execution = run;

            Notify(run, PassiveSkillTriggerType.ActiveSkillStarted);

            Pump();
        }

        // 동기 Cast(0)/Direct가 연달아 완료되어도 콜백 재귀로 다음 Action을 쌓지 않는다.
        private void Pump()
        {
            if (_pumping)
                return;

            var run = _execution;

            if (run == null)
                return;

            _pumping = true;

            try
            {
                while (_execution == run && !run.Waiting)
                {
                    if (!OwnerValid(run))
                    {
                        Finish(
                            run,
                            SkillCompletionKind.Interrupted,
                            "Owner unavailable"
                        );

                        break;
                    }

                    if (run.Index >= run.Actions.Count)
                    {
                        Finish(
                            run,
                            run.Successes > 0 ? SkillCompletionKind.Success : SkillCompletionKind.Failed,
                            "All actions finished"
                        );

                        break;
                    }

                    var action = run.Actions[run.Index];

                    run.Entered = false;

                    run.Reselections = 0;

                    run.Launches.Clear();

                    run.Hits.Clear();

                    run.Applications.Clear();

                    run.HitsObserved = true;

                    run.Metadata = CombatEventMetadata.Create(
                        run.Owner.Target,
                        run.Metadata,
                        executionId: run.Metadata.ExecutionId,
                        actionIndex: run.Index
                    );

                    if (action == null || !action.IsConfigured)
                    {
                        FailAction(
                            run,
                            false,
                            "Invalid action configuration"
                        );

                        continue;
                    }

                    if (action.Origin == SkillAreaOrigin.PreviousResult && (run.PreviousResult == null || !run.PreviousResult.HasPosition))
                    {
                        FailAction(
                            run,
                            false,
                            "Previous result position unavailable"
                        );

                        continue;
                    }

                    if (run.Index != 0 || run.Targets == null)
                        run.Targets = SelectTargets(
                            run,
                            action,
                            false
                        );

                    if (!EnsureTarget(run))
                        continue;

                    run.Batch = new SkillEffectBatch(
                        action,
                        run.Ledger,
                        run.PreviousResult,
                        DispatchFX,
                        () => _execution == run && OwnerValid(run),
                        run.Engagement.HostileFilter
                    );

                    run.Position = Origin(run, action);

                    run.Entered = true;

                    run.Batch.EventTemplate = Event(run, PassiveSkillTriggerType.ActiveSkillActionHit);

                    Notify(run, PassiveSkillTriggerType.ActiveSkillActionStarted);

                    if (_execution != run || !OwnerValid(run))
                    {
                        if (_execution == run)
                            Finish(
                                run,
                                SkillCompletionKind.Interrupted,
                                "Interrupted at action entry"
                            );

                        continue;
                    }

                    EmitFX(run, SkillFXHook.OnStart);

                    ApplyTiming(run, SkillEffectTiming.OnStart);

                    if (_execution != run || !OwnerValid(run))
                    {
                        if (_execution == run)
                            Finish(
                                run,
                                SkillCompletionKind.Interrupted,
                                "Interrupted during OnStart"
                            );

                        continue;
                    }

                    if (!EnsureTarget(run))
                        continue;

                    run.Waiting = true;

                    int index = run.Index;

                    switch (action)
                    {
                        case SkillCastActionData cast:
                            _core.StopMovement();

                            EmitFX(run, SkillFXHook.Cast);

                            if (_execution != run)
                                break;

                            _castController.StartCast(cast.Duration / Mathf.Max(0.01f, _core.RuntimeStatus.AttackSpeed), () => CompleteDelayed(run, index));

                            break;

                        case SkillDashActionData dash:
                            EmitFX(run, SkillFXHook.Dash);

                            if (_execution != run)
                                break;

                            // Dash 이동은 별도 컨트롤러가 담당하며 일반 MovementCompleted를 발생시키지 않는다.
                            run.DashStarted = true;

                            _dashController.StartDash(
                                run.Targets.PrimaryTarget.Target,
                                dash.Distance,
                                dash.Speed,
                                () => CompleteDelayed(run, index)
                            );

                            break;

                        case SkillAttackActionData attack:
                            bool success = ExecuteAttack(run, attack);

                            if (_execution != run)
                                break;

                            if (!OwnerValid(run))
                                Finish(
                                    run,
                                    SkillCompletionKind.Interrupted,
                                    "Interrupted during impact"
                                );

                            else if (success)
                                CompleteAction(run, ActionCompletionKind.Success);

                            else
                                FailAction(
                                    run,
                                    false,
                                    "No valid hit or successful projectile"
                                );

                            break;

                        default:
                            FailAction(
                                run,
                                false,
                                "Unsupported action"
                            );

                            break;
                    }
                }
            }
            catch (Exception exception)
            {
                if (_execution == run)
                    Finish(
                        run,
                        SkillCompletionKind.Failed,
                        exception.Message
                    );

                Debug.LogException(exception);
            }
            finally
            {
                _pumping = false;

                if (_execution != null && _execution != run && !_execution.Waiting)
                    Pump();
            }
        }

        // ============================================================
        // Action
        // ============================================================
        public void Tick(float deltaTime)
        {
            var run = _execution;

            if (!ValidateExecution(run))
                return;

            _castController.Tick(deltaTime);
        }

        public void FixedTick(float deltaTime)
        {
            var run = _execution;

            if (!ValidateExecution(run))
                return;

            _dashController.FixedTick(deltaTime);

            if (_execution == run)
                run.Moved |= run.DashStarted && _dashController.HasMoved;
        }

        private bool OwnerValid(Execution run) => run.Owner.IsTargetable && _core.isActiveAndEnabled && !_core.RuntimeStatus.HasStatus(UnitStatusEffectType.Stun) && !_core.RuntimeStatus.HasStatus(UnitStatusEffectType.Silence);

        private bool ValidateExecution(Execution run)
        {
            if (run == null || run != _execution)
                return false;

            if (!OwnerValid(run))
            {
                Finish(
                    run,
                    SkillCompletionKind.Interrupted,
                    "Owner blocked or lifetime changed"
                );

                return false;
            }

            return EnsureTarget(run);
        }

        private void CompleteDelayed(
            Execution run,
            int index)
        {
            if (_execution != run || run.Index != index || !ValidateExecution(run))
                return;

            run.Moved |= run.DashStarted && _dashController.HasMoved;

            run.Position = _core.transform.position;

            CompleteAction(run, ActionCompletionKind.Success);
        }

        public void Cancel()
        {
            if (_execution != null)
                Finish(
                    _execution,
                    SkillCompletionKind.Interrupted,
                    "Cancelled"
                );

            else
            {
                _castController.Cancel();

                _dashController.Cancel();
            }
        }

        private bool EnsureTarget(Execution run)
        {
            var action = run.Actions[run.Index];

            if (action == null)
            {
                FailAction(
                    run,
                    false,
                    "Missing action"
                );

                return false;
            }

            if (action.Target.Source == SkillTargetSource.None)
                return true;

            var target = run.Targets?.PrimaryTarget ?? default;

            bool valid = target.IsTargetable && (action.Target.Relation != SkillTargetRelation.Hostile || (run.Engagement.HostileFilter != null && run.Engagement.HostileFilter(target.Target)));

            if (valid)
            {
                run.Current = target;

                return true;
            }

            if (action.TargetLostPolicy == SkillTargetLostPolicy.Reselect && run.Reselections++ == 0)
            {
                run.Targets = SelectTargets(
                    run,
                    action,
                    true
                );

                if (run.Targets.Success && run.Targets.PrimaryTarget.IsTargetable)
                {
                    run.Current = run.Targets.PrimaryTarget;

                    if (_dashController.IsDashing)
                        _dashController.Retarget(run.Targets.PrimaryTarget.Target);

                    return true;
                }
            }

            FailAction(
                run,
                true,
                "Target lost or engagement denied"
            );

            return false;
        }

        private void FailAction(
            Execution run,
            bool targetLost,
            string reason)
        {
            if (_execution != run)
                return;

            var action = run.Index < run.Actions.Count ? run.Actions[run.Index] : null;

            bool skip = targetLost ? action?.TargetLostPolicy == SkillTargetLostPolicy.Skip : action?.FailurePolicy == SkillFailurePolicy.Skip;

            _castController.Cancel();

            run.Moved |= run.DashStarted && _dashController.HasMoved;

            _dashController.Cancel();

            if (skip)
                CompleteAction(
                    run,
                    ActionCompletionKind.Skipped,
                    reason
                );

            else
            {
                run.Results.Add(Result(run, ActionCompletionKind.Failed, reason));

                Finish(
                    run,
                    SkillCompletionKind.Failed,
                    reason
                );
            }
        }

        private ActionExecutionResult Result(
            Execution run,
            ActionCompletionKind kind,
            string reason = null) => new(
            kind,
            run.Index,
            run.Targets?.PrimaryTarget ?? default,
            run.Position,
            run.Entered,
            run.HitsObserved,
            run.Hits,
            run.Applications,
            reason,
            run.Launches
        );

        private void CompleteAction(
            Execution run,
            ActionCompletionKind kind,
            string reason = null)
        {
            if (_execution != run)
                return;

            if (kind == ActionCompletionKind.Success)
            {
                ApplyTiming(run, SkillEffectTiming.OnComplete);

                if (_execution != run)
                    return;

                if (!OwnerValid(run))
                {
                    Finish(
                        run,
                        SkillCompletionKind.Interrupted,
                        "Interrupted during OnComplete"
                    );

                    return;
                }

                EmitFX(run, SkillFXHook.OnComplete);
            }

            if (_execution != run)
                return;

            var result = Result(
                run,
                kind,
                reason
            );

            run.Results.Add(result);

            if (kind == ActionCompletionKind.Success)
            {
                run.Successes++;

                run.Previous = result.Target;

                run.PreviousResult = result;

                Notify(run, PassiveSkillTriggerType.ActiveSkillActionCompleted);

                if (_execution != run)
                    return;
            }

            CleanupFX(run);

            run.Waiting = false;

            run.Index++;

            run.Targets = null;

            Pump();
        }

        private void Finish(
            Execution run,
            SkillCompletionKind kind,
            string reason)
        {
            if (_execution != run)
                return;

            _execution = null; // 정리/이벤트 재진입보다 먼저 이 실행의 진행 소유권을 해제한다.
            run.Moved |= run.DashStarted && _dashController.HasMoved;

            _castController.Cancel();

            _dashController.Cancel();

            if (kind == SkillCompletionKind.Interrupted && run.Index < run.Actions.Count)
                run.Results.Add(Result(run, ActionCompletionKind.Interrupted, reason));

            if (run.Entered)
            {
                if (kind != SkillCompletionKind.Success)
                    EmitFX(run, kind == SkillCompletionKind.Interrupted ? SkillFXHook.OnInterrupted : SkillFXHook.OnFailed);

                CleanupFX(run);
            }

            LastResult = new SkillExecutionResult(
                run.Metadata.ExecutionId,
                kind,
                run.Moved,
                run.Results,
                reason
            );

            var finalResult = LastResult;

            if (kind == SkillCompletionKind.Success)
                Notify(
                    run,
                    PassiveSkillTriggerType.ActiveSkillCompleted,
                    kind
                );

            run.Callback?.Invoke(finalResult);
        }

        // ============================================================
        // Target / Area
        // ============================================================
        private CombatSkillEvent Event(
            Execution run,
            PassiveSkillTriggerType type,
            SkillCompletionKind? completion = null) => new(
            type,
            run.Metadata,
            run.Initial,
            run.Targets?.PrimaryTarget ?? run.Previous,
            position: run.Position,
            completion: completion
        );

        private void Notify(
            Execution run,
            PassiveSkillTriggerType type,
            SkillCompletionKind? completion = null) => Event(
            run,
            type,
            completion
        ).Notify();

        private Vector2 Origin(
            Execution run,
            SkillActionData action) => action.Origin switch
        {
            SkillAreaOrigin.Target => run.Targets != null && run.Targets.PrimaryTarget.IsTargetable ? (Vector2)run.Targets.PrimaryTarget.Target.Transform.position : run.Initial.Position,
            SkillAreaOrigin.PreviousResult => run.PreviousResult != null && run.PreviousResult.HasPosition ? run.PreviousResult.Position : (Vector2)_core.transform.position,
            _ => _core.transform.position
        };

        private SkillTargetResult SelectTargets(
            Execution run,
            SkillActionData action,
            bool reselect)
        {
            var settings = reselect ? action.Target.WithSource(SkillTargetSource.Search) : action.Target;

            Vector2 origin = Origin(run, action);

            Vector2 direction = run.Initial.IsTargetable ? (Vector2)run.Initial.Target.Transform.position - origin : (Vector2)_core.transform.right;

            SkillTargetRequest Request(Predicate<ICombatTarget> filter) => new(
                run.Owner.Target,
                settings,
                origin,
                direction,
                initialTarget: run.Initial,
                currentTarget: run.Current,
                previousTarget: run.Previous,
                hostileFilter: filter
            );

            var selected = _targetResolver.ResolveSkillTargets(Request(run.Engagement.HostileFilter));

            if (settings.Relation != SkillTargetRelation.Hostile || settings.Source == SkillTargetSource.None || !selected.Success)
                return selected;

            var approval = run.Engagement.Ensure(settings.Relation, selected.PrimaryTarget.Target);

            if (approval == SkillEngagementResult.Invalid)
                return new SkillTargetResult(
                    null,
                    origin,
                    direction,
                    false,
                    "Invalid engagement"
                );

            selected = _targetResolver.ResolveSkillTargets(Request(run.Engagement.HostileFilter));

            // 거절 후에는 내부 후보만 사용하고 두 번째 확대 요청을 하지 않는다.
            if (!selected.Success && approval == SkillEngagementResult.Denied)
            {
                if (_data.UsesLegacyTargetSelection)
                {
                    var candidates = _targetResolver.ResolveCandidates(new TargetCandidateRequest(origin, _data.SkillRange, GetTargetTeam(settings.Relation), run.Engagement.HostileFilter));

                    var fallback = new SkillTargetSelector(_core, _data).SelectTarget(run.Initial.Target, candidates);

                    return new SkillTargetResult(
                        fallback == null ? null : new[] { fallback },
                        origin,
                        direction
                    );
                }

                if (settings.Source == SkillTargetSource.Search)
                    return selected;

                if (action.TargetLostPolicy == SkillTargetLostPolicy.Reselect)
                {
                    settings = settings.WithSource(SkillTargetSource.Search);

                    selected = _targetResolver.ResolveSkillTargets(Request(run.Engagement.HostileFilter));
                }
            }

            return selected;
        }

        // ============================================================
        // Attack / Direct / Projectile
        // ============================================================
        private bool ExecuteAttack(
            Execution run,
            SkillAttackActionData action)
        {
            var primary = run.Targets.PrimaryTarget;

            if (!primary.IsTargetable)
                return false;

            Vector2 origin = Origin(run, action);

            Vector2 direction = ((Vector2)primary.Target.Transform.position - origin).normalized;

            if (action.Delivery == ActiveSkillDeliveryType.Projectile)
                return ExecuteProjectile(
                    run,
                    action,
                    origin,
                    direction
                );

            var targets = new List<CombatTargetSnapshot>();

            if (action.Area == ActiveSkillAreaType.Single)
            {
                targets.Add(primary);

                run.Position = primary.Target.Transform.position;
            }
            else
            {
                var center = action.Area == ActiveSkillAreaType.TargetCircle ? (Vector2)primary.Target.Transform.position : origin;

                foreach (var target in _targetResolver.ResolveHitTargets(new TargetHitRequest(center, direction, action.Radius, action.Angle, action.MaxEffectTargets, action.Area == ActiveSkillAreaType.SelfCone ? HitAreaType.Cone : HitAreaType.Circle, GetTargetTeam(action.Target.Relation), action.Target.Relation == SkillTargetRelation.Hostile ? run.Engagement.HostileFilter : null)))
                {
                    if (action.Target.Relation == SkillTargetRelation.Self && !ReferenceEquals(target, run.Owner.Target))
                        continue;

                    if (action.Target.Relation == SkillTargetRelation.Friendly && !action.Target.IncludeSelf && ReferenceEquals(target, run.Owner.Target))
                        continue;

                    targets.Add(new CombatTargetSnapshot(target));
                }

                run.Position = center;
            }

            var impact = NewImpact(run);

            foreach (var target in targets)
            {
                if (_execution != run || !OwnerValid(run))
                    break;

                if (!target.IsTargetable)
                    continue;

                run.Hits.Add(target); // 유효 명중과 실제 적용량은 독립이다. 방어로 0 피해여도 명중은 남는다.
                var context = new SkillConditionContext(
                    run.Owner.Target,
                    target,
                    _targetResolver,
                    impact,
                    true,
                    run.Index,
                    run.PreviousResult,
                    run.Position
                );

                run.Applications.AddRange(SkillAttackDelivery.Hit(run.Batch, context));

                run.Batch.EventTemplate?.NotifyHit(
                    target,
                    impact,
                    run.Position
                );

                EmitFX(run, SkillFXHook.OnHit);
            }

            // TODO: Skill FX / Hit FX 실제 재생은 수신 측에서 구현한다.
            return run.Hits.Count > 0;
        }

        private bool ExecuteProjectile(
            Execution run,
            SkillAttackActionData action,
            Vector2 origin,
            Vector2 direction)
        {
            var targets = new List<CombatTargetSnapshot>(run.Targets.Targets);

            if (action.IsLegacy && action.Target.MaxTargetCount > 1)
            {
                foreach (var candidate in _targetResolver.ResolveCandidates(new TargetCandidateRequest(origin, _data.SkillRange, run.Targets.PrimaryTarget.Team, action.Target.Relation == SkillTargetRelation.Hostile ? run.Engagement.HostileFilter : null)))
                {
                    if (targets.Count >= action.Target.MaxTargetCount)
                        break;

                    if (ReferenceEquals(candidate, run.Targets.PrimaryTarget.Target))
                        continue;

                    targets.Add(new CombatTargetSnapshot(candidate));
                }
            }

            int fired = 0;

            var type = action.Area == ActiveSkillAreaType.Single ? ProjectileImpactType.Single : action.Area == ActiveSkillAreaType.SelfCone ? ProjectileImpactType.Cone : ProjectileImpactType.Circle;

            run.HitsObserved = false;

            foreach (var target in targets)
            {
                if (_execution != run || !OwnerValid(run))
                    break;

                if (!target.IsTargetable)
                {
                    run.Launches.Add(new(target, false, "Target unavailable"));

                    continue;
                }

                // 목표마다 생성된 ProjectileRequest를 ProjectileManager에 전달한다.
                // SkillEffectRequest는 발사 시점의 Skill Effect 정보를 보관하고,
                // 실제 효과 대상은 Projectile 충돌 시 ImpactType에 따라 확정된다.
                // Single
                // → 충돌 대상을 SkillEffectRequest의 대상으로 사용
                // Circle / Cone
                // → 충돌 위치 기준 범위 판정 후 SkillEffectRequest의 대상으로 사용
                var flightBatch = new SkillEffectBatch(
                    action,
                    run.Ledger,
                    run.PreviousResult,
                    DispatchFX,
                    hostileFilter: run.Engagement.HostileFilter
                );

                flightBatch.EventTemplate = Event(run, PassiveSkillTriggerType.ActiveSkillActionHit);

                // 목표마다 1발씩 발사하고, 각 투사체의 효과 적용 인원은 별도로 제한한다.
                bool launched = SkillAttackDelivery.Fire(
                    run.Owner.Target,
                    target.Target,
                    origin,
                    action,
                    flightBatch,
                    NewImpact(run),
                    action.Target.Relation == SkillTargetRelation.Hostile ? run.Engagement.HostileFilter : null
                );

                run.Launches.Add(new(target, launched, launched ? null : "Fire rejected"));

                if (launched)
                {
                    fired++;

                    EmitFX(run, SkillFXHook.Fire);
                }
            }

            // Projectile의 SelfCircle / SelfCone도 충돌 위치에서 범위를 판정한다.
            // 이미 발사된 요청은 진행 중인 Execution을 참조하지 않는다. 완료는 비행/명중을 기다리지 않는다.
            return fired > 0;
        }

        // ============================================================
        // Target Team / Skill Effect / FX
        // ============================================================
        private UnitTeam GetTargetTeam(SkillTargetRelation relation) => relation == SkillTargetRelation.Hostile ? (_core.Team == UnitTeam.Ally ? UnitTeam.Enemy : UnitTeam.Ally) : _core.Team;

        private CombatEventMetadata NewImpact(Execution run)
        {
            var id = CombatEventMetadata.Create(run.Owner.Target, run.Metadata);

            return CombatEventMetadata.Create(
                run.Owner.Target,
                id,
                impactId: id.EventId
            );
        }

        private void ApplyTiming(
            Execution run,
            SkillEffectTiming timing)
        {
            if (_execution != run || run.Batch == null)
                return;

            var impact = NewImpact(run);

            var targets = run.Targets != null && run.Targets.Targets.Count > 0 ? run.Targets.Targets : new[]
            {
                default(CombatTargetSnapshot)
            };

            foreach (var target in targets)
            {
                if (_execution != run || !OwnerValid(run))
                    break;

                var context = new SkillConditionContext(
                    run.Owner.Target,
                    target,
                    _targetResolver,
                    impact,
                    true,
                    run.Index,
                    run.PreviousResult,
                    run.Position
                );

                run.Applications.AddRange(SkillEffectPipeline.Resolve(run.Batch, context, timing));
            }
        }

        private void EmitFX(
            Execution run,
            SkillFXHook hook)
        {
            if (run.Index >= run.Actions.Count || run.Actions[run.Index] == null)
                return;

            foreach (var entry in run.Actions[run.Index].FXEntries)
                if (entry != null && entry.Hook == hook)
                    DispatchFX(new SkillFXRequest(entry, run.Metadata, run.Position));
        }

        private void DispatchFX(SkillFXRequest request)
        {
            if (FXRequested == null)
                return;

            foreach (Action<SkillFXRequest> listener in FXRequested.GetInvocationList())
            {
                try
                {
                    listener(request);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private void CleanupFX(Execution run)
        {
            if (!run.Entered || run.Index >= run.Actions.Count)
                return;

            run.Entered = false;

            run.Batch?.CleanupConditionalFX();

            foreach (var entry in run.Actions[run.Index].FXEntries)
                if (entry != null)
                    DispatchFX(new SkillFXRequest(entry, run.Metadata, run.Position, true));

        }
    }
}
