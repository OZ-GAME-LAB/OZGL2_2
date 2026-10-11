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
        [SerializeField, Min(0f)] private float _hitCooldown = 1f;

        [Header("바라보는 방향")]
        [Tooltip("좌우 반전할 외형 자식. 비워두면 SPUM 루트를 사용합니다.")]
        [SerializeField] private Transform _facingRoot;
        [Tooltip("원본 외형이 양수 X 스케일에서 오른쪽을 보는 경우 켭니다.")]
        [SerializeField] private bool _spriteFacesRight;
        [SerializeField] private bool _initialFacingRight;

        public bool IsFacingLocked => _dead || _stunned || _victory
            || _skillMotion != 0 || Time.time < _oneShotUntil;

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
        private bool _timedAttack;
        private float _savedAnimatorSpeed;

        // ============================================================
        // Initialize
        // ============================================================

        public void Initialize(Unit_Core core)
        {
            EndTimedAttack();
            // 풀 재사용 시 이전 수명의 사망/기절/피격 제한을 해제한다.
            _dead = false;
            _stunned = false;
            _nextHitTime = float.NegativeInfinity;
            _lastIndex = -1;
            _moving = false;
            _victory = false;
            _skillMotion = 0;
            _oneShotUntil = 0f;

            if (_spum == null)
                _spum = GetComponentInChildren<SPUM_Prefabs>(true);

            if (_facingRoot == null && _spum != null)
                _facingRoot = _spum.transform;

            // 유닛 루트·외부 오브젝트의 물리와 UI는 반전하지 않는다.
            if (_facingRoot != null && (_facingRoot == transform || !_facingRoot.IsChildOf(transform)))
            {
                Debug.LogWarning($"[Unit_Animation] {name} : Facing Root는 외형 자식이어야 합니다.");
                _facingRoot = null;
            }

            core.SetFacingDirection(_initialFacingRight ? Vector2.right : Vector2.left);

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

            _animator.Rebind();
            SetFacingDirection(core.FacingDirection);
            PlayAnimation_Idle();
        }

        public void SetFacingDirection(Vector2 direction)
        {
            if (_facingRoot == null || Mathf.Abs(direction.x) <= 0.01f)
                return;

            bool facingRight = direction.x > 0f;

            // 부모가 이미 반전된 프리팹도 월드 방향에 맞춘다. 크기와 Y/Z는 유지한다.
            float parentSign = _facingRoot.parent != null
                && _facingRoot.parent.TransformVector(Vector3.right).x < 0f ? -1f : 1f;

            Vector3 scale = _facingRoot.localScale;

            scale.x = Mathf.Abs(scale.x) * (facingRight == _spriteFacesRight ? 1f : -1f) * parentSign;

            _facingRoot.localScale = scale;
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
        public void PlayAnimation_Skill(float duration = 0f) => PlayTimedAttack(PlayerState.ATTACK, duration);
        public void PlayAnimation_Buff(float duration = 0f) => PlayTimedAttack(PlayerState.OTHER, duration);

        // 클립 길이를 액션 실행 시간에 맞추고, 논리적 완료 전 대기 애니메이션으로 복귀하지 않는다.
        private void PlayTimedAttack(PlayerState state, float duration, int index = 1)
        {
            EndTimedAttack();
            if (!PlayOneShot(state, index) || duration <= 0f) return;
            _savedAnimatorSpeed = _animator.speed;
            _animator.speed = _spum.StateAnimationPairs[state.ToString()][index].length / duration;
            _timedAttack = true;
            _oneShotUntil = float.PositiveInfinity;
        }

        // 전투 타이머와 동일한 시간으로 기본 공격 클립을 재생한다.
        // 클립이 없는 경우에도 0.1초 뒤 정상적인 실행/실패 정리가 가능하다.
        public float PlayBasicAttackForExecution(float attackSpeed)
        {
            float duration = 0.1f;
            if (_spum != null && _spum.StateAnimationPairs.TryGetValue(PlayerState.ATTACK.ToString(), out var clips)
                && clips != null && clips.Count > 0 && clips[0] != null)
                duration = Mathf.Max(0.1f, clips[0].length / Mathf.Max(0.01f, attackSpeed));
            PlayTimedAttack(PlayerState.ATTACK, duration, 0);
            return duration;
        }

        private void EndTimedAttack()
        {
            if (!_timedAttack) return;
            if (_animator != null) _animator.speed = _savedAnimatorSpeed;
            _timedAttack = false;
            _oneShotUntil = 0f;
        }

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
            bool timed = _timedAttack;
            EndTimedAttack();
            if (timed) RefreshContinuousAnimation();
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
            if (_timedAttack || !_hitAnimationEnabled || Time.time < _nextHitTime)
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
            EndTimedAttack();
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

            EndTimedAttack();
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
