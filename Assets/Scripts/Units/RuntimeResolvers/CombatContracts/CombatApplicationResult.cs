// 요청량과 실제 적용량, 확정 사망 정보를 분리하여 반응형 전투 결과를 전달한다.
namespace Units
{
    public enum CombatApplicationStatus
    {
        Invalid,
        Attempted,
        NoChange,
        Applied
    }
    public enum CombatApplicationKind
    {
        Damage,
        Heal,
        Shield,
        RuntimeEffect
    }

    // 적용량은 해당 사건의 값이다. 중첩 반응 완료 후 Death가 확정될 수 있으므로 결과 핸들을 유지한다.
    public sealed class CombatApplicationResult
    {

        // ============================================================
        // Properties
        // ============================================================

        public CombatApplicationKind Kind { get; }

        public CombatApplicationStatus Status { get; internal set; }

        public CombatEventMetadata Metadata { get; }

        public CombatTargetSnapshot Target { get; }

        public float RequestedAmount { get; }

        public float HpDamage { get; internal set; }

        public float ShieldAbsorbed { get; internal set; }

        public float HealedAmount { get; internal set; }

        public float ShieldAdded { get; internal set; }

        public string FailureReason { get; internal set; }

        public CombatDeathResult Death { get; internal set; }

        public bool WasApplied => Status == CombatApplicationStatus.Applied;

        // ============================================================
        // Constructor
        // ============================================================

        public CombatApplicationResult(
            CombatApplicationKind kind,
            ICombatTarget target,
            float requestedAmount,
            CombatEventMetadata metadata,
            CombatApplicationStatus status = CombatApplicationStatus.NoChange,
            string failureReason = null)
        {
            Kind = kind;

            Target = new CombatTargetSnapshot(target);

            RequestedAmount = requestedAmount;

            Metadata = metadata;

            Status = status;

            FailureReason = failureReason;
        }

        // ============================================================
        // Execution
        // ============================================================

        public static CombatApplicationResult Invalid(
            CombatApplicationKind kind,
            ICombatTarget target,
            CombatEventMetadata metadata,
            string reason)
        {
            return new CombatApplicationResult(
                kind,
                target,
                0f,
                metadata,
                CombatApplicationStatus.Invalid,
                reason
            );
        }
    }

    public sealed class CombatDeathResult
    {

        // ============================================================
        // Properties
        // ============================================================

        public CombatTargetSnapshot Victim { get; }

        public CombatTargetSnapshot Killer { get; }

        public CombatEventMetadata Metadata { get; }

        public DamageSourceType SourceType { get; }

        public bool IsEnemyKill => Killer.Target != null && Killer.Team != Victim.Team;

        // ============================================================
        // Data / Runtime State
        // ============================================================

        private bool _killerNotified;

        // ============================================================
        // Execution
        // ============================================================

        internal bool TryMarkKillerNotified()
        {
            if (_killerNotified)
                return false;

            _killerNotified = true;

            return true;
        }

        // ============================================================
        // Properties
        // ============================================================

        public bool CanNotifyKiller => IsEnemyKill && Killer.IsTargetable;

        // ============================================================
        // Constructor
        // ============================================================

        public CombatDeathResult(
            CombatTargetSnapshot victim,
            DamageResult cause)
        {
            Victim = victim;

            Killer = cause.Metadata.Owner;

            Metadata = cause.Metadata;

            SourceType = cause.SourceType;
        }
    }
}
