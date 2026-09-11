using System;
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
        // Resolver
        // ============================================================

        private HitTargetResolver _hitTargetResolver;


        // ============================================================
        // Runtime State
        // ============================================================

        private bool _isBusy;

        private float _basicAttackDelayRemaining;

        private float _skillCooldownRemaining;


        // ============================================================
        // Properties & Conditions
        // ============================================================

        public bool IsBusy
            => _isBusy;

        public bool IsBasicAttackReady
            => !_isBusy
            && _basicAttackDelayRemaining <= 0f;

        public bool IsActiveSkillReady
            => !_isBusy
            && _skillCooldownRemaining <= 0f;


        public bool CanUseActiveSkill
        {
            get
            {
                if (_core == null)
                    return false;

                if (_activeSkillData == null)
                    return false;

                if (_activeSkillExecutor == null)
                    return false;

                if (!IsActiveSkillReady)
                    return false;

                // TODO:
                // 상태이상 등으로 인한 Skill 사용 불가 조건 추가
                //
                // if (_core.RuntimeStatus.IsSilenced)
                //     return false;

                return true;
            }
        }


        public bool CanUseBasicAttack
        {
            get
            {
                if (_core == null)
                    return false;

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
                    return _core.RuntimeStatus.ActiveSkillData.SkillRange;

                return _core.RuntimeStatus.BasicAttackData.BasicAttackRange;
            }
        }


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Update()
        {
            UpdateTimers();
        }


        // ============================================================
        // Initialize
        // ============================================================

        public void Initialize(
            Unit_Core core)
        {
            if (core == null)
            {
                Debug.LogError(
                    $"[Unit_Combat] {name} : Unit_Core가 없습니다."
                );

                return;
            }


            _core =
                core;


            InitializeData();

            InitializeExecutors();

            ResetRuntimeState();
        }


        private void InitializeData()
        {
            if (_core.RuntimeStatus == null)
            {
                Debug.LogError(
                    $"[Unit_Combat] {name} : RuntimeStatus가 없습니다."
                );

                _basicAttackData =
                    null;

                _activeSkillData =
                    null;

                return;
            }


            _basicAttackData =
                _core.RuntimeStatus.BasicAttackData;

            _activeSkillData =
                _core.RuntimeStatus.ActiveSkillData;


            if (_basicAttackData == null)
            {
                Debug.LogWarning(
                    $"[Unit_Combat] {name} : BasicAttackData가 없습니다."
                );
            }


            if (_activeSkillData == null)
            {
                Debug.LogWarning(
                    $"[Unit_Combat] {name} : ActiveSkillData가 없습니다."
                );
            }
        }


        private void InitializeExecutors()
        {
            _hitTargetResolver =
                new HitTargetResolver(
                    _core
                );


            _basicAttackExecutor =
                _basicAttackData != null
                    ? new BasicAttackExecutor(
                        _core,
                        _basicAttackData,
                        _hitTargetResolver
                    )
                    : null;


            _activeSkillExecutor =
                _activeSkillData != null
                    ? new ActiveSkillExecutor(
                        _core,
                        _activeSkillData,
                        _hitTargetResolver
                    )
                    : null;
        }


        private void ResetRuntimeState()
        {
            _isBusy =
                false;

            _basicAttackDelayRemaining =
                0f;

            _skillCooldownRemaining =
                0f;
        }


        // ============================================================
        // Basic Attack
        // ============================================================

        public bool TryBasicAttack(
            ICombatTarget target)
        {
            if (!CanUseBasicAttack)
                return false;


            if (!IsValidTarget(
                target))
            {
                return false;
            }


            ExecuteBasicAttack(
                target
            );


            return true;
        }


        private void ExecuteBasicAttack(
            ICombatTarget target)
        {
            _isBusy =
                true;


            StartBasicAttackDelay();


            _basicAttackExecutor.Execute(
                target,
                CompleteBasicAttack
            );
        }


        private void StartBasicAttackDelay()
        {
            if (_core == null)
                return;

            if (_core.RuntimeStatus == null)
                return;

            if (_basicAttackData == null)
                return;


            float attackSpeed =
                Mathf.Max(
                    0.01f,
                    _core.RuntimeStatus.AttackSpeed
                );


            _basicAttackDelayRemaining =
                _basicAttackData.BasicAttackDelay
                / attackSpeed;
        }


        private void CompleteBasicAttack()
        {
            if (!_isBusy)
                return;


            _isBusy =
                false;


            _core?.NotifyBasicAttackCompleted();
        }


        // ============================================================
        // Active Skill
        // ============================================================

        public bool TryActiveSkill(
            ICombatTarget target)
        {
            if (!CanUseActiveSkill)
                return false;


            if (!IsValidTarget(
                target))
            {
                return false;
            }


            ExecuteActiveSkill(
                target
            );


            return true;
        }


        private void ExecuteActiveSkill(
            ICombatTarget target)
        {
            _isBusy =
                true;


            StartSkillCooldown();


            _activeSkillExecutor.Execute(
                target,
                CompleteActiveSkill
            );
        }


        private void StartSkillCooldown()
        {
            if (_core == null)
                return;

            if (_core.RuntimeStatus == null)
                return;

            if (_activeSkillData == null)
                return;


            float cooldownReduction =
                Mathf.Clamp01(
                    _core.RuntimeStatus.CooldownReduction
                );


            _skillCooldownRemaining =
                _activeSkillData.SkillCooldown
                * (1f - cooldownReduction);
        }


        private void CompleteActiveSkill()
        {
            if (!_isBusy)
                return;


            _isBusy =
                false;


            _core?.NotifyActiveSkillCompleted();
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


            _basicAttackDelayRemaining =
                Mathf.Max(
                    0f,
                    _basicAttackDelayRemaining
                    - Time.deltaTime
                );
        }


        private void UpdateSkillCooldown()
        {
            if (_skillCooldownRemaining <= 0f)
                return;


            _skillCooldownRemaining =
                Mathf.Max(
                    0f,
                    _skillCooldownRemaining
                    - Time.deltaTime
                );
        }


        // ============================================================
        // Stop
        // ============================================================

        public void Stop()
        {
            _isBusy =
                false;


            // TODO:
            // Cast / Dash / 공격 Animation 등
            // 실제 진행 중인 Action이 추가되면
            // Executor 쪽 Cancel 기능도 호출한다.
        }


        // ============================================================
        // Validation
        // ============================================================

        private bool IsValidTarget(
            ICombatTarget target)
        {
            if (target == null)
                return false;

            if (!target.IsTargetable)
                return false;

            if (target.Transform == null)
                return false;

            if (_core != null
                && target.Team == _core.Team)
            {
                return false;
            }


            return true;
        }
    }
}