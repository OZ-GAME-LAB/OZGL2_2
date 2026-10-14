using System;
using System.Threading;

// 동기 반응의 원인을 전달하고 지연 요청에는 보관 가능한 출처 메타데이터를 제공한다.
namespace Units
{
    public readonly struct CombatEventMetadata
    {

        // ============================================================
        // Data / Runtime State
        // ============================================================

        private static long _nextId;

        // ============================================================
        // Properties
        // ============================================================

        public long EventId { get; }

        public long RootEventId { get; }

        public long ParentEventId { get; }

        public CombatTargetSnapshot Owner { get; }

        public bool IsAdditionalAttack { get; }

        public long ExecutionId { get; }

        public int ActionIndex { get; }

        public long ImpactId { get; }

        public int EntryId { get; }

        // ============================================================
        // Constructor
        // ============================================================

        private CombatEventMetadata(
            ICombatTarget owner,
            CombatEventMetadata parent,
            bool additionalAttack,
            long executionId,
            int actionIndex,
            long impactId,
            int entryId)
        {
            EventId = Interlocked.Increment(ref _nextId);

            RootEventId = parent.EventId == 0 ? EventId : parent.RootEventId;

            ParentEventId = parent.EventId;

            Owner = parent.EventId != 0 && ReferenceEquals(owner, parent.Owner.Target) ? parent.Owner : new CombatTargetSnapshot(owner);

            IsAdditionalAttack = additionalAttack || parent.IsAdditionalAttack;

            ExecutionId = executionId != 0 ? executionId : parent.ExecutionId;

            ActionIndex = executionId != 0 ? actionIndex : parent.ActionIndex;

            ImpactId = impactId != 0 ? impactId : parent.ImpactId;

            EntryId = entryId != 0 ? entryId : parent.EntryId;
        }

        private CombatEventMetadata(
            CombatEventMetadata source,
            bool additionalAttack)
        {
            EventId = source.EventId;

            RootEventId = source.RootEventId;

            ParentEventId = source.ParentEventId;

            Owner = source.Owner;

            IsAdditionalAttack = source.IsAdditionalAttack || additionalAttack;

            ExecutionId = source.ExecutionId;

            ActionIndex = source.ActionIndex;

            ImpactId = source.ImpactId;

            EntryId = source.EntryId;
        }

        // ============================================================
        // Execution
        // ============================================================

        public CombatEventMetadata WithAdditionalAttack(bool additionalAttack) => new CombatEventMetadata(this, additionalAttack);

        public static CombatEventMetadata Create(
            ICombatTarget owner,
            CombatEventMetadata parent = default,
            bool additionalAttack = false,
            long executionId = 0,
            int actionIndex = -1,
            long impactId = 0,
            int entryId = 0)
        {
            if (parent.EventId == 0)
                parent = CombatEventContext.Current;

            return new CombatEventMetadata(
                owner,
                parent,
                additionalAttack,
                executionId,
                actionIndex,
                impactId,
                entryId
            );
        }
    }

    // 동기 반응의 출처만 전달한다. 투사체/DoT 같은 지연 작업은 Metadata를 명시적으로 보관한다.
    // 신규 글로벌 매니저나 이벤트 버스가 아니며 using 범위 종료 시 이전 출처를 복원한다.
    public static class CombatEventContext
    {

        // ============================================================
        // Data / Runtime State
        // ============================================================

        [ThreadStatic]
        private static CombatEventMetadata _current;

        // ============================================================
        // Properties
        // ============================================================

        public static CombatEventMetadata Current => _current;

        // ============================================================
        // Execution
        // ============================================================

        public static IDisposable Enter(CombatEventMetadata metadata)
        {
            return new Scope(metadata);
        }

        private sealed class Scope : IDisposable
        {

            // ============================================================
            // Data / Runtime State
            // ============================================================

            private readonly CombatEventMetadata _previous;

            private bool _disposed;

            // ============================================================
            // Constructor
            // ============================================================

            public Scope(CombatEventMetadata metadata)
            {
                _previous = _current;

                _current = metadata;
            }

            // ============================================================
            // Cleanup
            // ============================================================

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;

                _current = _previous;
            }
        }
    }
}
