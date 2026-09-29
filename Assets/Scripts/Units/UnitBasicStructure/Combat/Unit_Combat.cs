using System;
using System.Collections.Generic;
using Units.Skills;
using UnityEngine;


namespace Units
{
    public class Unit_Combat : MonoBehaviour
    {
        // ============================================================
        // Reference
        // ============================================================

        private Unit_Core _core;


        // ============================================================
        // Data
        // ============================================================

        private BasicAttackData _basicAttackData;

        private ActiveSkillData _activeSkillData;


        // ============================================================
        // Executor
        // ============================================================

        private BasicAttackExecutor _basicAttackExecutor;

        private ActiveSkillExecutor _activeSkillExecutor;


        // ============================================================
        // Selector
        // ============================================================

        private SkillTargetSelector _skillTargetSelector;


        // ============================================================
        // Resolver
        // ============================================================

        private TargetResolver _targetResolver;


        // ============================================================
        // Runtime State
        // ============================================================

        private bool _isPaused;

        private bool _isBusy;

        private enum ExecutionOwner
        {
            None,
            BasicAttack,
            ActiveSkill
        }

        private ExecutionOwner _executionOwner;

        private int _executionVersion;

        public SkillExecutionResult LastSkillResult { get; private set; }

        private float _basicAttackDelayRemaining;

        private float _skillCooldownRemaining;

        private bool _isPreparingSkill;

        private float _skillRetryRemaining;

        private bool _reevaluateAfterSkillMovement;

        private readonly List<ICombatTarget> _skillCandidates = new List<ICombatTarget>();


        // ============================================================
        // Restriction Runtime State
        // ============================================================

        private bool _isBasicAttackBlocked;

        private bool _isActiveSkillBlocked;


        // ============================================================
        // Properties & Conditions
        // ============================================================

        public bool IsBusy => _isBusy;

        public bool IsBasicAttackReady => !_isBusy && !_isPreparingSkill && _basicAttackDelayRemaining <= 0f;

        public bool IsActiveSkillReady => !_isBusy && !_isPreparingSkill && _skillRetryRemaining <= 0f && _skillCooldownRemaining <= 0f;

        public bool CanUseActiveSkill
        {
            get
            {
                if (_isPaused)
                    return false;

                if (_isActiveSkillBlocked)
                    return false;

                if (_core == null || !_core.IsAlive || !isActiveAndEnabled)
                {
                    return false;
                }

                if (_activeSkillData == null)
                    return false;

                if (_activeSkillExecutor == null)
                    return false;

                if (_skillTargetSelector == null)
                    return false;

                if (!IsActiveSkillReady)
                    return false;

                return true;
            }
        }

        public bool CanUseBasicAttack
        {
            get
            {
                if (_isPaused)
                    return false;

                if (_isBasicAttackBlocked)
                    return false;

                if (_core == null || !_core.IsAlive || !isActiveAndEnabled)
                {
                    return false;
                }

                if (_basicAttackData == null)
                    return false;

                if (_basicAttackExecutor == null)
                    return false;

                if (!IsBasicAttackReady)
                    return false;

                return true;
            }
        }

        public float PreferredCombatRange
        {
            get
            {
                if (_core == null)
                    return 0f;

                if (_core.RuntimeStatus == null)
                    return 0f;

                if (CanUseActiveSkill)
                    return _activeSkillData.SkillRange;

                if (_basicAttackData == null)
                    return 0f;

                return _basicAttackData.BasicAttackRange;
            }
        }


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void FixedUpdate()
        {
            if (_isPaused)
                return;

            _activeSkillExecutor?.FixedTick(Time.fixedDeltaTime);
        }

        private void OnDisable()
        {
            Stop();
        }

        private void OnDestroy()
        {
            Stop();
        }

        private void Update()
        {
            if (_isPaused)
                return;

            _skillRetryRemaining = Mathf.Max(0f, _skillRetryRemaining - Time.deltaTime);

            UpdateTimers();

            _activeSkillExecutor?.Tick(Time.deltaTime);
        }


        // ============================================================
        // Initialize
        // ============================================================

        public void Initialize(Unit_Core core)
        {
            if (core == null)
            {
                Debug.LogError($"[Unit_Combat] {name} : Unit_Core가 없습니다.");

                return;
            }

            Stop();

            _core = core;

            InitializeData();

            InitializeCombatModules();

            ResetRuntimeState();
        }

        private void InitializeData()
        {
            if (_core.RuntimeStatus == null)
            {
                Debug.LogError($"[Unit_Combat] {name} : RuntimeStatus가 없습니다.");

                _basicAttackData = null;

                _activeSkillData = null;

                return;
            }

            _basicAttackData = _core.RuntimeStatus.BasicAttackData;

            _activeSkillData = _core.RuntimeStatus.ActiveSkillData;

            if (_basicAttackData == null)
            {
                Debug.LogWarning($"[Unit_Combat] {name} : BasicAttackData가 없습니다.");
            }

            if (_activeSkillData == null)
            {
                Debug.LogWarning($"[Unit_Combat] {name} : ActiveSkillData가 없습니다.");
            }
        }

        private void InitializeCombatModules()
        {
            _targetResolver = new TargetResolver();

            _skillTargetSelector = _activeSkillData != null ? new SkillTargetSelector(_core, _activeSkillData) : null;

            _basicAttackExecutor = _basicAttackData != null ? new BasicAttackExecutor(
                _core,
                _basicAttackData,
                _targetResolver
            ) : null;

            _activeSkillExecutor = _activeSkillData != null ? new ActiveSkillExecutor(
                _core,
                _activeSkillData,
                _targetResolver
            ) : null;
        }

        private void ResetRuntimeState()
        {
            _isPaused = false;

            _isPreparingSkill = false;

            _skillRetryRemaining = 0f;

            _reevaluateAfterSkillMovement = false;

            _isBusy = false;

            _basicAttackDelayRemaining = 0f;

            _skillCooldownRemaining = 0f;

            _isBasicAttackBlocked = false;

            _isActiveSkillBlocked = false;
        }


        // ============================================================
        // Basic Attack
        // ============================================================

        public bool TryBasicAttack(ICombatTarget target)
        {
            if (!CanUseBasicAttack)
                return false;

            if (!IsValidEnemyTarget(target))
            {
                return false;
            }

            ExecuteBasicAttack(target);

            return true;
        }

        private void ExecuteBasicAttack(ICombatTarget target)
        {
            _isBusy = true;

            _executionOwner = ExecutionOwner.BasicAttack;

            int version = ++_executionVersion;

            StartBasicAttackDelay();

            _basicAttackExecutor.Execute(target, () =>
            {
                if (version == _executionVersion && _executionOwner == ExecutionOwner.BasicAttack)
                    CompleteBasicAttack();
            });
        }

        private void StartBasicAttackDelay()
        {
            if (_core == null)
                return;

            if (_core.RuntimeStatus == null)
                return;

            if (_basicAttackData == null)
                return;

            float attackSpeed = Mathf.Max(0.01f, _core.RuntimeStatus.AttackSpeed);

            _basicAttackDelayRemaining = _basicAttackData.BasicAttackDelay / attackSpeed;
        }

        private void CompleteBasicAttack()
        {
            if (_executionOwner != ExecutionOwner.BasicAttack)
                return;

            _executionOwner = ExecutionOwner.None;

            if (!_isBusy)
                return;

            _isBusy = false;

            _core?.NotifyBasicAttackCompleted();
        }


        // ============================================================
        // Active Skill
        // ============================================================

        public bool TryActiveSkill(ICombatTarget currentTarget)
        {
            if (!CanUseActiveSkill)
                return false;

            _isPreparingSkill = true;

            int preparationVersion = _executionVersion;

            bool started = false;

            try
            {
                CollectSkillCandidates();

                var initial = _skillTargetSelector.SelectTarget(currentTarget, _skillCandidates);

                var engagement = new SkillEngagementSession(_core.RequestSkillEngagement, _core.CaptureSkillTargetFilter);

                // 이 사용 시도에서 상위 검토 요청은 정확히 한 곳에서만 발생한다.
                // 최초 Action이 Friendly/None이면 최초 Hostile Action까지 승인을 지연한다.
                if (!_activeSkillExecutor.TryPrepare(
                    initial,
                    engagement,
                    out var prepared
                ))
                    return false;

                // Target 탐색과 Engagement 검토 사이에
                // Status가 변경될 수 있으므로 실행 직전에 다시 확인한다.
                // 이미 준비에 들어온 요청이므로 신규 진입 조건은 다시 검사하지 않는다.
                if (!CanContinueActiveSkillPreparation() || !_isPreparingSkill || preparationVersion != _executionVersion)
                    return false;

                _isBusy = true;

                _executionOwner = ExecutionOwner.ActiveSkill;

                int version = ++_executionVersion;

                _reevaluateAfterSkillMovement = false;

                StartSkillCooldown();

                started = true;

                _activeSkillExecutor.Execute(initial, result =>
                {
                    if (version != _executionVersion || _executionOwner != ExecutionOwner.ActiveSkill)
                        return;

                    LastSkillResult = result;

                    _reevaluateAfterSkillMovement = result.Moved;

                    CompleteActiveSkill();
                }, engagement, prepared);

                return true;
            }
            finally
            {
                _isPreparingSkill = false;

                // 쿨타임은 소비하지 않는다. 실패 반복이 평타/이동 판단을 굶기지 않게 잠시 양보한다.
                if (!started)
                    _skillRetryRemaining = 0.25f;
            }
        }

        private void CollectSkillCandidates()
        {
            _skillCandidates.Clear();

            if (_activeSkillData.TargetSide == SkillTargetRelation.Self)
            {
                return;
            }

            UnitTeam team = _activeSkillData.TargetSide == SkillTargetRelation.Friendly ? _core.Team : (_core.Team == UnitTeam.Ally ? UnitTeam.Enemy : UnitTeam.Ally);

            IReadOnlyList<ICombatTarget> candidates = _targetResolver.ResolveCandidates(new TargetCandidateRequest(_core.transform.position, _activeSkillData.SkillRange, team));

            // Resolver의 재사용 버퍼를 실행/재선택 과정에서 보관하지 않는다.
            for (int i = 0; i < candidates.Count; i++)
            {
                _skillCandidates.Add(candidates[i]);
            }
        }

        private void StartSkillCooldown()
        {
            if (_core == null)
                return;

            if (_core.RuntimeStatus == null)
                return;

            if (_activeSkillData == null)
                return;

            float cooldownReduction = Mathf.Clamp01(_core.RuntimeStatus.CooldownReduction);

            _skillCooldownRemaining = _activeSkillData.SkillCooldown * (1f - cooldownReduction);
        }

        private void CompleteActiveSkill()
        {
            if (_executionOwner != ExecutionOwner.ActiveSkill)
                return;

            _executionOwner = ExecutionOwner.None;

            if (!_isBusy)
                return;

            _isBusy = false;

            bool reevaluate = _reevaluateAfterSkillMovement;

            _reevaluateAfterSkillMovement = false;

            _core?.NotifySkillExecutionEnded(LastSkillResult, reevaluate);
        }


        // ============================================================
        // Timer
        // ============================================================

        private void UpdateTimers()
        {
            UpdateBasicAttackDelay();

            UpdateSkillCooldown();
        }

        private void UpdateBasicAttackDelay()
        {
            if (_basicAttackDelayRemaining <= 0f)
                return;

            _basicAttackDelayRemaining = Mathf.Max(0f, _basicAttackDelayRemaining - Time.deltaTime);
        }

        private void UpdateSkillCooldown()
        {
            if (_skillCooldownRemaining <= 0f)
                return;

            _skillCooldownRemaining = Mathf.Max(0f, _skillCooldownRemaining - Time.deltaTime);
        }


        // ============================================================
        // Combat Restriction
        // ============================================================

        public void SetBasicAttackBlocked(bool isBlocked)
        {
            if (_isBasicAttackBlocked == isBlocked)
            {
                return;
            }

            _isBasicAttackBlocked = isBlocked;

            if (!isBlocked)
                return;

            InterruptBasicAttack();
        }

        public void SetActiveSkillBlocked(bool isBlocked)
        {
            if (_isActiveSkillBlocked == isBlocked)
            {
                return;
            }

            _isActiveSkillBlocked = isBlocked;

            if (!isBlocked)
                return;

            InterruptActiveSkill();
        }

        private void InterruptBasicAttack()
        {
            if (_executionOwner != ExecutionOwner.BasicAttack)
                return;

            _basicAttackExecutor?.Cancel();

            // 현재 BasicAttack이 소유한 Busy만 정리한다. 액티브 실행과 종료를 혼동하지 않는다.
            _executionVersion++;

            CompleteBasicAttack();
        }

        private void InterruptActiveSkill()
        {
            _isPreparingSkill = false;

            if (_executionOwner != ExecutionOwner.ActiveSkill)
                return;

            // 진행 중인 Cast / Dash를 중단한다.
            // 이미 발사된 투사체는 ProjectileManager의 생명주기를 따른다.
            _activeSkillExecutor?.Cancel();
        }


        // ============================================================
        // Combat Control
        // ============================================================

        public void Pause()
        {
            if (_isPaused)
                return;

            _isPaused = true;
        }

        public void Resume()
        {
            if (!_isPaused)
                return;

            _isPaused = false;
        }

        public void Stop()
        {
            _isPreparingSkill = false;

            _isPaused = true; // 종료 알림에서 새 실행에 재진입하지 못한다.
            if (_executionOwner == ExecutionOwner.ActiveSkill)
                _activeSkillExecutor?.Cancel();

            else if (_executionOwner == ExecutionOwner.BasicAttack)
                InterruptBasicAttack();

            _basicAttackExecutor?.Cancel();

            _activeSkillExecutor?.Cancel();

            // 진행 중인 Cast / Dash만 취소하고, 이미 발사된 투사체는 Manager에서 유지한다.
            _executionVersion++;

            _executionOwner = ExecutionOwner.None;

            _isBusy = false;

            _reevaluateAfterSkillMovement = false;

            _skillRetryRemaining = 0f;

            _isPaused = false;
        }


        // ============================================================
        // Validation
        // ============================================================

        private bool IsValidEnemyTarget(ICombatTarget target)
        {
            if (!IsValidTarget(target))
            {
                return false;
            }

            if (_core != null && target.Team == _core.Team)
            {
                return false;
            }

            return true;
        }

        private bool IsValidSkillTarget(ICombatTarget target)
        {
            if (!IsValidTarget(target))
            {
                return false;
            }

            // SkillTargetSelector가 TargetSide에 맞는 대상을 선정한다.
            // Unit_Combat은 선정된 대상 자체의 유효성만 검증한다.
            return true;
        }

        private bool IsValidTarget(ICombatTarget target)
        {
            if (!CombatTargetUtility.IsValid(target))
            {
                return false;
            }

            if (!target.IsTargetable)
                return false;

            if (target.Transform == null)
                return false;

            return true;
        }

        private bool CanContinueActiveSkillPreparation()
        {
            if (_isPaused)
                return false;

            if (_isActiveSkillBlocked)
                return false;

            if (_core == null || !_core.IsAlive || !isActiveAndEnabled)
            {
                return false;
            }

            if (_activeSkillData == null)
                return false;

            if (_activeSkillExecutor == null)
                return false;

            return true;
        }
    }
}
