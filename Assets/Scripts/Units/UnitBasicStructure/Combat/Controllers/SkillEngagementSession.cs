using System;
using Units.Skills;

// 첫 적대 대상 사용 시 승인과 필터를 확보하고 같은 실행의 재승인을 방지한다.
namespace Units
{
    // 실행별 객체: 첫 적대 Action까지 승인을 지연할 수 있고 한 실행에서 재요청하지 않는다.
    public sealed class SkillEngagementSession
    {

        // ============================================================
        // Data / Runtime State
        // ============================================================

        private readonly Func<ICombatTarget, SkillEngagementResult> _request;

        private readonly Func<Predicate<ICombatTarget>> _capture;

        private bool _requested;

        private SkillEngagementResult _result;

        // ============================================================
        // Properties
        // ============================================================

        public Predicate<ICombatTarget> HostileFilter { get; private set; }

        public bool HasRequested => _requested;

        // ============================================================
        // Constructor
        // ============================================================

        public SkillEngagementSession(
            Func<ICombatTarget, SkillEngagementResult> request,
            Func<Predicate<ICombatTarget>> capture)
        {
            _request = request;

            _capture = capture;
        }

        // ============================================================
        // Execution
        // ============================================================

        public SkillEngagementResult Ensure(
            SkillTargetRelation relation,
            ICombatTarget target)
        {
            if (relation != SkillTargetRelation.Hostile)
                return SkillEngagementResult.AlreadyEngaged;

            if (_requested)
                return _result;

            _requested = true;

            _result = _request != null ? _request(target) : SkillEngagementResult.Invalid;

            HostileFilter = _result != SkillEngagementResult.Invalid ? _capture?.Invoke() : null;

            if (HostileFilter == null)
                HostileFilter = _ => false;

            return _result;
        }
    }
}
