using UnityEngine;

namespace Units
{
    /// <summary>유닛 행동을 SPUM 상태와 클립 인덱스에 연결한다.</summary>
    public class Unit_Animation : MonoBehaviour
    {
        // ============================================================
        // Components
        // ============================================================

        // 기존 프리팹의 Animator 참조를 유지한다.
        [SerializeField] private Animator _animator;
        [SerializeField] private SPUM_Prefabs _spum;

        [Header("피격 애니메이션")]
        [SerializeField] private bool _hitAnimationEnabled = true;
        [SerializeField, Min(0f)] private float _hitCooldown = 0.3f;

        private AnimatorOverrideController _ownedController;
        private RuntimeAnimatorController _originalController;
        private bool _initialized;
        private bool _dead;
        private bool _stunned;
        private float _nextHitTime;
        private PlayerState _lastState;
        private int _lastIndex = -1;

        // ============================================================
        // Initialize
        // ============================================================

        public void Initialize(Unit_Core core)
        {
            if (_spum == null)
                _spum = GetComponentInChildren<SPUM_Prefabs>(true);

            if (_spum == null || _spum._anim == null)
                return;

            _animator = _spum._anim;
            if (!_initialized)
            {
                if (_animator.runtimeAnimatorController == null)
                    return;

                _originalController = _animator.runtimeAnimatorController;
                _spum.OverrideControllerInit();
                _ownedController = _spum.OverrideController;
                _initialized = true;
            }

            // 풀 재사용 시 이전 수명의 사망/기절/피격 제한을 해제한다.
            _dead = false;
            _stunned = false;
            _nextHitTime = float.NegativeInfinity;
            _lastIndex = -1;
            _animator.Rebind();
            PlayAnimation_Idle();
        }

        private void OnDestroy()
        {
            if (_ownedController == null)
                return;

            if (_animator != null && _animator.runtimeAnimatorController == _ownedController)
                _animator.runtimeAnimatorController = _originalController;

            Destroy(_ownedController);
        }

        // ============================================================
        // Animation
        // ============================================================

        public void PlayAnimation_Idle() => Play(PlayerState.IDLE, 0, true);
        public void PlayAnimation_Move() => Play(PlayerState.MOVE, 0, true);
        public void PlayAnimation_Dash() => Play(PlayerState.MOVE, 1, true);
        public void PlayAnimation_Attack() => Play(PlayerState.ATTACK, 0);
        public void PlayAnimation_Skill() => Play(PlayerState.ATTACK, 1);
        public void PlayAnimation_Cast() => Play(PlayerState.OTHER, 0, true);
        public void PlayAnimation_Buff() => Play(PlayerState.OTHER, 1);
        public void PlayAnimation_Victory() => Play(PlayerState.OTHER, 2, true);

        public void PlayAnimation_Hit()
        {
            TryPlayAnimation_Hit();
        }

        /// <summary>실제 피격 재생 요청이 전달되었을 때만 쿨타임을 소비한다.</summary>
        public bool TryPlayAnimation_Hit()
        {
            if (!_hitAnimationEnabled || Time.time < _nextHitTime)
                return false;

            if (!Play(PlayerState.DAMAGED, 0))
                return false;

            _nextHitTime = Time.time + Mathf.Max(0f, _hitCooldown);
            return true;
        }

        public void PlayAnimation_Stun()
        {
            if (_stunned || !Play(PlayerState.DEBUFF, 0, true))
                return;

            _stunned = true;
        }

        /// <summary>기절 해제 후 대기로 복귀한다. 다음 행동은 호출자가 요청한다.</summary>
        public void StopAnimation_Stun()
        {
            if (!_stunned || _dead)
                return;

            _stunned = false;
            PlayAnimation_Idle();
        }

        public void PlayAnimation_Death()
        {
            if (_dead)
                return;

            Play(PlayerState.DEATH, 0);
            // 사망 클립이 없어도 이후 일반 행동은 차단한다.
            _dead = true;
            _stunned = false;
        }

        private bool Play(PlayerState state, int index, bool skipRepeated = false)
        {
            if (!_initialized || _spum == null || _animator == null
                || !isActiveAndEnabled || !_animator.isActiveAndEnabled || _dead)
                return false;

            if (_stunned && state != PlayerState.DEATH)
                return false;

            if (skipRepeated && _lastState == state && _lastIndex == index)
                return false;

            if (!_spum.StateAnimationPairs.TryGetValue(state.ToString(), out var clips)
                || clips == null || index < 0 || index >= clips.Count || clips[index] == null)
                return false;

            // 이전 프레임의 대기 중 Trigger가 새 요청 뒤에 실행되지 않게 한다.
            foreach (var parameter in _animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger)
                    _animator.ResetTrigger(parameter.nameHash);
            }

            _spum.PlayAnimation(state, index);
            _lastState = state;
            _lastIndex = index;
            return true;
        }
    }
}
