using System;
using System.Threading;

namespace Units.FX
{
    // 대여할 때마다 새 ID를 발급하여 과거 핸들이 재사용된 인스턴스를 정지하지 못하게 한다.
    public readonly struct FXHandle : IEquatable<FXHandle>
    {
        private static long _nextId;
        public long Id { get; }
        public bool IsValid => Id != 0;
        private FXHandle(long id) { Id = id; }
        internal static FXHandle Create() => new FXHandle(Interlocked.Increment(ref _nextId));
        public bool Equals(FXHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is FXHandle handle && Equals(handle);
        public override int GetHashCode() => Id.GetHashCode();
    }
}
