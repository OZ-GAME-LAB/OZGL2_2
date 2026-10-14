using System;
using Units.Skills;

// 마지막 트리거 대상의 상태를 구독하고 활성 패시브의 유지·해제 검사만 요청한다.
namespace Units
{
    // 정상 트리거만 관찰 대상을 교체한다. 통지는 유지/해제만 요청한다.
    public sealed class PassiveConditionWatch : IDisposable
    {

        // ============================================================
        // Data / Runtime State
        // ============================================================

        private readonly CombatTargetSnapshot _owner, _target;

        private readonly Action _changed;

        private readonly CombatStateChange _ownerMask, _targetMask;

        // ============================================================
        // Properties
        // ============================================================

        public bool Spatial { get; }

        public PassiveContext Context { get; }

        // ============================================================
        // Data / Runtime State
        // ============================================================

        private bool _invalidated;

        // ============================================================
        // Properties
        // ============================================================

        public bool IsValid => !_invalidated && _owner.IsTargetable && (_target.Target == null || _target.IsTargetable);

        // ============================================================
        // Data / Runtime State
        // ============================================================

        private float _elapsed;

        // ============================================================
        // Constructor
        // ============================================================

        public PassiveConditionWatch(
            PassiveSkillData data,
            PassiveContext context,
            Action changed)
        {
            Context = context;

            _owner = new CombatTargetSnapshot(context.Owner);

            _target = new CombatTargetSnapshot(context.Target);

            _changed = changed;

            foreach (var condition in data.Conditions)
            {
                if (condition == null)
                    continue;

                _ownerMask |= condition.OwnerDependencies;

                _targetMask |= condition.TargetDependencies;

                Spatial |= condition.UsesSpatialQuery;
            }

            _owner.Target.CombatStateChanged += OwnerChanged;

            if (_target.Target != null)
                _target.Target.CombatStateChanged += TargetChanged;
        }

        // ============================================================
        // Execution
        // ============================================================

        private void OwnerChanged(CombatStateChange change)
        {
            if ((change & CombatStateChange.Lifetime) != 0)
                _invalidated = true;

            if ((change & (_ownerMask | CombatStateChange.Lifetime)) != 0)
                _changed();
        }

        private void TargetChanged(CombatStateChange change)
        {
            if ((change & CombatStateChange.Lifetime) != 0)
                _invalidated = true;

            if ((change & (_targetMask | CombatStateChange.Lifetime)) != 0)
                _changed();
        }

        // 공간 조건만 지정 간격으로 재검사하고 수치 변화는 구독 이벤트로 처리한다.
        public void Tick(
            float delta,
            float interval)
        {
            if (!Spatial)
                return;

            _elapsed += delta;

            if (_elapsed < interval)
                return;

            _elapsed = 0f;

            _changed(); // 대규모 프레임 지연에도 한 프레임에 따라잡기 루프를 만들지 않는다.
        }

        // ============================================================
        // Cleanup
        // ============================================================

        // 패시브 종료 시 소유자와 대상의 상태 구독을 함께 해제한다.
        public void Dispose()
        {
            _owner.Target.CombatStateChanged -= OwnerChanged;

            if (_target.Target != null)
                _target.Target.CombatStateChanged -= TargetChanged;
        }
    }
}
