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
        [SerializeField, Min(0f)] private float _hitCooldown = 0.5f;

        private AnimatorOverrideController _ownedController;
        private RuntimeAnimatorController _originalController;
        private bool _initialized;
        private bool _dead;
        private bool _stunned;
        private float _nextHitTime;
        private PlayerState _lastState;
        private int _lastIndex = -1;
        private bool _moving;
        private bool _victory;
        private int _skillMotion; // 0: 없음, 1: 캐스팅, 2: 돌진
        private float _oneShotUntil;

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
            _moving = false;
            _victory = false;
            _skillMotion = 0;
            _oneShotUntil = 0f;
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

        // 논리적 공격 완료는 즉시 발생할 수 있다. 클립 재생 시간과 별도로 관리한다.
        private void Update() => RefreshContinuousAnimation();

        public void PlayAnimation_Idle()
        {
            _moving = false;
            RefreshContinuousAnimation();
        }

        public void PlayAnimation_Move()
        {
            _moving = true;
            RefreshContinuousAnimation();
        }

        public void PlayAnimation_Dash() => StartSkillMotion(2);
        public void PlayAnimation_Cast() => StartSkillMotion(1);
        public void PlayAnimation_Attack() => PlayOneShot(PlayerState.ATTACK, 0);
        public void PlayAnimation_Skill() => PlayOneShot(PlayerState.ATTACK, 1);
        public void PlayAnimation_Buff() => PlayOneShot(PlayerState.OTHER, 1);

        public void PlayAnimation_Victory()
        {
            if (_dead || _stunned || _victory)
                return;
            _victory = true;
            _skillMotion = 0;
            _oneShotUntil = 0f;
            RefreshContinuousAnimation();
        }

        private void StartSkillMotion(int motion)
        {
            if (_dead || _stunned || _victory)
                return;
            _skillMotion = motion;
            _oneShotUntil = 0f;
            RefreshContinuousAnimation();
        }

        public void StopAnimation_SkillMotion()
        {
            if (_skillMotion == 0)
                return;
            _skillMotion = 0;
            RefreshContinuousAnimation();
        }

        private void RefreshContinuousAnimation()
        {
            if (_dead || _stunned || Time.time < _oneShotUntil)
                return;
            if (_victory)
                Play(PlayerState.OTHER, 2, true);
            else if (_skillMotion == 1)
                Play(PlayerState.OTHER, 0, true);
            else if (_skillMotion == 2)
                Play(PlayerState.MOVE, 1, true);
            else
                Play(_moving ? PlayerState.MOVE : PlayerState.IDLE, 0, true);
        }

        private bool PlayOneShot(PlayerState state, int index)
        {
            if (_victory || !Play(state, index))
                return false;
            var clip = _spum.StateAnimationPairs[state.ToString()][index];
            _oneShotUntil = Time.time + clip.length / Mathf.Max(0.01f, _animator.speed);
            return true;
        }

        public void PlayAnimation_Hit()
        {
            TryPlayAnimation_Hit();
        }

        /// <summary>실제 피격 재생 요청이 전달되었을 때만 쿨타임을 소비한다.</summary>
        public bool TryPlayAnimation_Hit()
        {
            if (!_hitAnimationEnabled || Time.time < _nextHitTime)
                return false;

            if (!PlayOneShot(PlayerState.DAMAGED, 0))
                return false;

            _nextHitTime = Time.time + Mathf.Max(0f, _hitCooldown);
            return true;
        }

        public void PlayAnimation_Stun()
        {
            if (_dead || _stunned || _victory)
                return;
            _oneShotUntil = 0f;
            _skillMotion = 0;
            Play(PlayerState.DEBUFF, 0, true);
            _stunned = true;
        }

        /// <summary>기절 해제 후 대기로 복귀한다. 다음 행동은 호출자가 요청한다.</summary>
        public void StopAnimation_Stun()
        {
            if (!_stunned || _dead)
                return;

            _stunned = false;
            RefreshContinuousAnimation();
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
            {
                // 캐스팅 또는 승리 중 OTHER가 끝나면 다시 재생한다.
                bool restartOther = (_skillMotion == 1 || _victory)
                    && state == PlayerState.OTHER
                    && !_animator.IsInTransition(0)
                    && !_animator.GetCurrentAnimatorStateInfo(0).IsName("OTHER");

                if (!restartOther)
                    return false;
            }

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
