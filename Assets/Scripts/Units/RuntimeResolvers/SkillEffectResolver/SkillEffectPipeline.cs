using System;
using System.Collections.Generic;
using Units.Effects;
using UnityEngine;

// Before/Base/After 순서로 조건·스택 소비·효과를 처리하며 엔트리별 중복 실행을 막는다.
namespace Units.Skills
{
    // 진행 컨텍스트와 독립적이다. 실행과 투사체 요청이 함께 참조하며 마지막 참조가 끝나면 회수된다.
    public sealed class SkillEffectLedger
    {

        // ============================================================
        // Data / Runtime State
        // ============================================================

        private readonly HashSet<int> _once = new();

        private readonly HashSet<int> _onceRunning = new();

        private readonly HashSet<(int, long, int, int)> _seen = new();

        // ============================================================
        // Execution
        // ============================================================

        internal bool Enter(
            SkillEffectEntry entry,
            CombatEventMetadata metadata,
            CombatTargetSnapshot target)
        {
            bool once = entry.Subject == SkillEffectSubject.Self && entry.Frequency == SkillEffectFrequency.OncePerExecution;

            if (once && (_once.Contains(entry.EntryId) || !_onceRunning.Add(entry.EntryId)))
                return false;

            if (!_seen.Add((entry.EntryId, metadata.ImpactId, target.ObjectId, target.LifetimeVersion)))
            {
                if (once)
                    _onceRunning.Remove(entry.EntryId);

                return false;
            }

            return true;
        }

        internal void Exit(
            SkillEffectEntry entry,
            bool applied)
        {
            if (entry.Subject != SkillEffectSubject.Self || entry.Frequency != SkillEffectFrequency.OncePerExecution)
                return;

            _onceRunning.Remove(entry.EntryId);

            if (applied)
                _once.Add(entry.EntryId);
        }
    }

    // 발사 정의/Owner 조건은 복사하되 실행별 횟수 기록만 공유한다.
    public sealed class SkillEffectBatch
    {
        // ============================================================
        // FX Ownership / Cleanup
        // ============================================================

        private readonly List<SkillFXRequest> _conditionalFX = new();

        private bool _conditionalFXClosed;
        internal Units.FX.FXScope? FXScope { get; set; }
        internal Transform FXFollowTarget { get; set; }
        internal Vector2 FXDirection { get; set; }

        // 조건이 성립한 요청만 기록한다. 정리 콜백 재진입 전에 소유권을 먼저 해제한다.
        internal void RequestConditionalFX(SkillFXRequest request)
        {
            if (_conditionalFXClosed)
                return;

            _conditionalFX.Add(request);

            FXRequested?.Invoke(request);
        }

        public void CleanupConditionalFX()
        {
            if (_conditionalFXClosed)
                return;

            _conditionalFXClosed = true;

            var pending = _conditionalFX.ToArray();

            _conditionalFX.Clear();

            foreach (var request in pending)
                FXRequested?.Invoke(request.AsCleanup(FXScope.HasValue ? Units.FX.FXCleanupReason.ScopeEnded : Units.FX.FXCleanupReason.ActionEnded));
        }

        // ============================================================
        // Properties
        // ============================================================

        public IReadOnlyList<SkillConditionData> Conditions { get; }

        public IReadOnlyList<SkillEffectEntry> BaseEffects { get; }

        public IReadOnlyList<SkillConditionalEffectEntry> ConditionalEffects { get; }

        public SkillEffectLedger Ledger { get; }

        public ActionExecutionResult PreviousResult { get; }

        public Action<SkillFXRequest> FXRequested { get; }

        public Func<bool> CanContinue { get; }

        public CombatSourceSnapshot SourceSnapshot { get; }

        public CombatSkillEvent EventTemplate { get; set; }

        public bool IsActiveSkill { get; }

        public IReadOnlyList<SkillFXEntry> FXEntries { get; }

        public Predicate<ICombatTarget> HostileFilter { get; }

        // ============================================================
        // Constructor
        // ============================================================

        public SkillEffectBatch(
            SkillActionData action,
            SkillEffectLedger ledger,
            ActionExecutionResult previous,
            Action<SkillFXRequest> fxRequested = null,
            Func<bool> canContinue = null,
            Predicate<ICombatTarget> hostileFilter = null,
            bool isActiveSkill = true)
        {
            IsActiveSkill = isActiveSkill;

            FXEntries = new List<SkillFXEntry>(action.FXEntries).AsReadOnly();

            Conditions = new List<SkillConditionData>(action.Conditions).AsReadOnly();

            BaseEffects = new List<SkillEffectEntry>(action.BaseEffects).AsReadOnly();

            ConditionalEffects = new List<SkillConditionalEffectEntry>(action.ConditionalEffects).AsReadOnly();

            Ledger = ledger;

            PreviousResult = previous;

            FXRequested = fxRequested;

            CanContinue = canContinue;

            HostileFilter = hostileFilter;
        }

        private SkillEffectBatch(
            SkillEffectBatch source,
            SkillConditionContext context,
            CombatSourceSnapshot snapshot)
        {
            IsActiveSkill = source.IsActiveSkill;

            FXEntries = SkillDefinitionCopy.Copy(new List<SkillFXEntry>(source.FXEntries)).AsReadOnly();

            SourceSnapshot = snapshot;

            Ledger = source.Ledger;

            PreviousResult = source.PreviousResult;

            // 발사자의 컴포넌트 구독이 해제되어도 비행 중인 연출은 독립적으로 전달한다.
            FXRequested = Units.FX.UnitFXBridge.Dispatch;

            HostileFilter = source.HostileFilter;

            EventTemplate = source.EventTemplate;

            Conditions = CombatSourceSnapshot.FreezeConditions(source.Conditions, context);

            BaseEffects = SkillDefinitionCopy.Copy(new List<SkillEffectEntry>(source.BaseEffects)).AsReadOnly();

            var extras = SkillDefinitionCopy.Copy(new List<SkillConditionalEffectEntry>(source.ConditionalEffects));

            foreach (var entry in extras)
                entry?.FreezeConditions(context);

            ConditionalEffects = extras.AsReadOnly();
        }

        // ============================================================
        // Execution
        // ============================================================

        // 발사 시점 설정을 고정하되 실행당 중복 적용 기록은 다른 발사체와 공유한다.
        public SkillEffectBatch Freeze(
            SkillConditionContext context,
            CombatSourceSnapshot snapshot) => SourceSnapshot != null ? this : new(
            this,
            context,
            snapshot
        );
    }

    public static class SkillEffectPipeline
    {

        // ============================================================
        // Execution
        // ============================================================

        // 같은 타격의 BeforeBase → Base → AfterBase 순서로 조건과 소비·적용을 처리한다.
        public static IReadOnlyList<CombatApplicationResult> Resolve(
            SkillEffectBatch batch,
            SkillConditionContext context,
            SkillEffectTiming timing)
        {
            var results = new List<CombatApplicationResult>();

            if (batch == null || !context.CanApply || !SkillConditionData.All(batch.Conditions, context))
                return results.AsReadOnly();

            using (CombatEventContext.Enter(context.Metadata))
            {
                foreach (var entry in batch.ConditionalEffects)
                    if (entry != null && entry.Order == SkillEffectOrder.BeforeBase)
                        Apply(
                            entry,
                            batch,
                            context,
                            timing,
                            results
                        );

                foreach (var entry in batch.BaseEffects)
                    Apply(
                        entry,
                        batch,
                        context,
                        timing,
                        results
                    );

                // 변경된 현재 상태로 조건을 다시 읽는다. 죽은 대상의 값은 직접 적용 대상과 구분한다.
                if (context.Target.MatchesLifetime)
                    context = new SkillConditionContext(
                        context.Owner.Target,
                        new CombatTargetSnapshot(context.Target.Target),
                        context.Resolver,
                        context.Metadata,
                        context.IsActiveSkill,
                        context.ActionIndex,
                        context.PreviousResult,
                        context.Position,
                        context.SourceSnapshot
                    );

                foreach (var entry in batch.ConditionalEffects)
                    if (entry != null && entry.Order == SkillEffectOrder.AfterBase)
                        Apply(
                            entry,
                            batch,
                            context,
                            timing,
                            results
                        );
            }

            return results.AsReadOnly();
        }

        private static void Apply(
            SkillEffectEntry entry,
            SkillEffectBatch batch,
            SkillConditionContext context,
            SkillEffectTiming timing,
            List<CombatApplicationResult> results)
        {
            if (entry == null || entry.Timing != timing || entry.EntryId <= 0 || !context.CanApply || (batch.CanContinue != null && !batch.CanContinue()))
                return;

            if (context.Target.MatchesLifetime)
                context = new SkillConditionContext(
                    context.Owner.Target,
                    new CombatTargetSnapshot(context.Target.Target),
                    context.Resolver,
                    context.Metadata,
                    context.IsActiveSkill,
                    context.ActionIndex,
                    context.PreviousResult,
                    context.Position,
                    context.SourceSnapshot
                );

            var target = entry.Subject == SkillEffectSubject.Self ? context.Owner : context.Target;

            var applicationTargets = new List<CombatTargetSnapshot>();

            if (entry.Subject == SkillEffectSubject.SnapshotArea)
            {
                var settings = entry.SnapshotAreaTarget;

                if (context.Target.ObjectId == 0 || context.Resolver == null || settings == null)
                    return;

                if (context.IsActiveSkill && settings.Relation == SkillTargetRelation.Hostile && batch.HostileFilter == null)
                    return;

                var selected = context.Resolver.ResolveSkillTargets(new SkillTargetRequest(context.Owner.Target, settings.WithSource(SkillTargetSource.Search), context.Target.Position, Vector2.right, hostileFilter: batch.HostileFilter, sourceSnapshot: context.SourceSnapshot));

                applicationTargets.AddRange(selected.Targets);
            }
            else if (target.IsTargetable)
                applicationTargets.Add(target);

            if (applicationTargets.Count == 0)
                return;

            if (entry is SkillConditionalEffectEntry conditional && !SkillConditionData.All(conditional.Conditions, context))
                return;

            if (!batch.Ledger.Enter(
                entry,
                context.Metadata,
                context.Target
            ))
                return;

            bool applied = false;

            try
            {
                var metadata = CombatEventMetadata.Create(
                    context.Owner.Target,
                    context.Metadata,
                    entryId: entry.EntryId
                );

                if (entry is SkillConditionalEffectEntry extra)
                {
                    if (extra.ConsumeStacks)
                    {
                        var subject = extra.ConsumeSubject == SkillConditionSubject.Owner ? context.Owner : context.Target;

                        if (!subject.IsTargetable || !subject.Target.TryConsumeEffectStacks(new EffectStackConsumeRequest(subject.Target, extra.StackQuery, extra.StackCount, metadata)))
                            return;
                    }

                    foreach (var fx in extra.FXEntries)
                        if (fx != null)
                            batch.RequestConditionalFX(new SkillFXRequest(fx, metadata, context.Position,
                                direction: batch.FXScope.HasValue ? batch.FXDirection : context.Target.Position - context.Owner.Position,
                                target: context.Target, followTarget: batch.FXFollowTarget, scope: batch.FXScope));
                }

                // 소비 통지도 반응을 일으킬 수 있으므로 적용 직전에 재검사한다.
                if (!context.CanApply || SkillEffectResolver.Instance == null ||
                    (batch.CanContinue != null && !batch.CanContinue()))
                    return;

                foreach (var destination in applicationTargets)
                {
                    if (!destination.IsTargetable || !context.CanApply)
                        continue;

                    foreach (var result in SkillEffectResolver.Instance.ResolveWithResults(new SkillEffectRequest(context.Owner.Target, destination.Target, entry.Effects, metadata, canContinue: batch.CanContinue, sourceSnapshot: context.SourceSnapshot)))
                    {
                        results.Add(result);

                        applied |= result.WasApplied;
                    }
                }
            }
            finally
            {
                batch.Ledger.Exit(entry, applied);
            }
        }
    }
}

