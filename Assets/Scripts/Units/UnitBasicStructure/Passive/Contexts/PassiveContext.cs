namespace Units
{
    public readonly struct PassiveContext
    {
        // ============================================================
        // Context
        // ============================================================

        private readonly ICombatTarget _owner;

        private readonly ICombatTarget _target;

        private readonly TargetResolver _targetResolver;


        // ============================================================
        // Properties
        // ============================================================

        // 패시브를 보유한 유닛
        public ICombatTarget Owner =>
            _owner;

        // 현재 패시브 판정의 대상
        public ICombatTarget Target =>
            _target;

        // 패시브 범위 판정 및 대상 탐색에 사용
        public TargetResolver TargetResolver =>
            _targetResolver;


        // ============================================================
        // Constructor
        // ============================================================

        public PassiveContext(
            ICombatTarget owner,
            ICombatTarget target,
            TargetResolver targetResolver)
        {
            _owner =
                owner;

            _target =
                target;

            _targetResolver =
                targetResolver;
        }
    }
}