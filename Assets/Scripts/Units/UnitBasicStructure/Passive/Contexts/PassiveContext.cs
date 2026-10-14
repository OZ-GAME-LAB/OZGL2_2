namespace Units
{
    public readonly struct PassiveContext
    {
        // ============================================================
        // Context
        // ============================================================

        public CombatEventMetadata Metadata { get; }

        public CombatSkillEvent SkillEvent { get; }

        public CombatSourceSnapshot FrozenTarget { get; }

        public Units.Skills.PassiveSkillTriggerType? EventType => SkillEvent?.EventType;

        public long ExecutionId => Metadata.ExecutionId;

        public int ActionIndex => Metadata.ActionIndex;

        public CombatTargetSnapshot TargetSnapshot { get; }

        public CombatTargetSnapshot InitialTarget => SkillEvent?.InitialTarget ?? default;

        public CombatTargetSnapshot ActionTarget => SkillEvent?.ActionTarget ?? default;

        public CombatTargetSnapshot HitTarget => SkillEvent?.HitTarget ?? default;

        public UnityEngine.Vector2 ResultPosition => SkillEvent?.ResultPosition ?? TargetSnapshot.Position;

        public SkillCompletionKind? CompletionKind => SkillEvent?.CompletionKind;

        private readonly ICombatTarget _owner;

        private readonly ICombatTarget _target;

        private readonly TargetResolver _targetResolver;


        // ============================================================
        // Properties
        // ============================================================

        // 패시브를 보유한 유닛
        public ICombatTarget Owner => _owner;

        // 현재 패시브 판정의 대상
        public ICombatTarget Target => _target;

        // 패시브 범위 판정 및 대상 탐색에 사용
        public TargetResolver TargetResolver => _targetResolver;


        // ============================================================
        // Constructor
        // ============================================================

        public PassiveContext(
            ICombatTarget owner,
            ICombatTarget target,
            TargetResolver targetResolver,
            CombatSkillEvent skillEvent = null,
            CombatSourceSnapshot frozenTarget = null)
        {
            FrozenTarget = frozenTarget;

            SkillEvent = skillEvent;

            TargetSnapshot = frozenTarget?.Owner ?? skillEvent?.Target ?? new CombatTargetSnapshot(target);

            Metadata = skillEvent?.Metadata ?? CombatEventContext.Current;

            _owner = owner;

            _target = target;

            _targetResolver = targetResolver;
        }
    }
}