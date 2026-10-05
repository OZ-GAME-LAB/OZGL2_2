using System;
using System.Threading;

namespace Units.FX
{
    // 재생 키와 별도로 실행·액션·투사체 한 발의 소유 범위를 구분한다.
    public readonly struct FXScope : IEquatable<FXScope>
    {
        private static long _nextProjectile;
        public int OwnerId { get; }
        public int LifetimeVersion { get; }
        public long ExecutionId { get; }
        public int ActionIndex { get; }
        public long ProjectileId { get; }
        public bool IsProjectile => ProjectileId != 0;

        public FXScope(CombatEventMetadata metadata, long projectileId = 0)
        {
            OwnerId = metadata.Owner.ObjectId;
            LifetimeVersion = metadata.Owner.LifetimeVersion;
            ExecutionId = metadata.ExecutionId != 0 ? metadata.ExecutionId : metadata.RootEventId;
            ActionIndex = metadata.ActionIndex;
            ProjectileId = projectileId;
        }

        public static FXScope Projectile(CombatEventMetadata metadata)
            => new FXScope(metadata, Interlocked.Increment(ref _nextProjectile));

        public bool Equals(FXScope other) => OwnerId == other.OwnerId
            && LifetimeVersion == other.LifetimeVersion && ExecutionId == other.ExecutionId
            && ActionIndex == other.ActionIndex && ProjectileId == other.ProjectileId;
        public override bool Equals(object obj) => obj is FXScope scope && Equals(scope);
        public override int GetHashCode() => HashCode.Combine(OwnerId, LifetimeVersion, ExecutionId, ActionIndex, ProjectileId);
    }
}
