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
        // Runtime State
        // ============================================================

        private bool _isBusy;

        private float _basicAttackDelayRemaining;

        private float _skillCooldownRemaining;


        // ============================================================
        // Properties
        // ============================================================

        public bool IsBusy =>
            _isBusy;

        public bool IsBasicAttackReady =>
            !_isBusy
            && _basicAttackDelayRemaining <= 0f;

        public bool IsActiveSkillReady =>
            !_isBusy
            && _skillCooldownRemaining <= 0f;


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
            _basicAttackExecutor =
                _basicAttackData != null
                    ? new BasicAttackExecutor(
                        _core,
                        _basicAttackData
                    )
                    : null;


            _activeSkillExecutor =
                _activeSkillData != null
                    ? new ActiveSkillExecutor(
                        _core,
                        _activeSkillData
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
            GameObject target)
        {
            if (!CanBasicAttack())
                return false;

            if (!IsValidTarget(target))
                return false;


            ExecuteBasicAttack(
                target
            );

            return true;
        }


        private bool CanBasicAttack()
        {
            if (_core == null)
                return false;

            if (_core.RuntimeStatus == null)
                return false;

            if (_basicAttackData == null)
                return false;

            if (_basicAttackExecutor == null)
                return false;

            if (_isBusy)
                return false;

            if (_basicAttackDelayRemaining > 0f)
                return false;

            return true;
        }


        private void ExecuteBasicAttack(
            GameObject target)
        {
            _isBusy =
                true;


            _basicAttackExecutor.Execute(
                target
            );


            StartBasicAttackDelay();


            _isBusy =
                false;
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


        // ============================================================
        // Active Skill
        // ============================================================

        public bool TryActiveSkill(
            GameObject target)
        {
            if (!CanActiveSkill())
                return false;

            if (!IsValidTarget(target))
                return false;


            ExecuteActiveSkill(
                target
            );


            return true;
        }


        private bool CanActiveSkill()
        {
            if (_core == null)
                return false;

            if (_core.RuntimeStatus == null)
                return false;

            if (_activeSkillData == null)
                return false;

            if (_activeSkillExecutor == null)
                return false;

            if (_isBusy)
                return false;

            if (_skillCooldownRemaining > 0f)
                return false;


            return true;
        }

        private void ExecuteActiveSkill(
            GameObject target)
        {
            _isBusy =
                true;


            _activeSkillExecutor.Execute(
                target
            );


            StartSkillCooldown();


            // TODO:
            // Cast / Dash / Skill Animation 실행 종료 시점에
            // Busy 해제하도록 변경

            _isBusy =
                false;
        }

        private void StartSkillCooldown()
        {
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


        // ============================================================
        // Runtime State
        // ============================================================

        public void SetBusy(
            bool isBusy)
        {
            _isBusy =
                isBusy;
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
        // Validation
        // ============================================================

        private bool IsValidTarget(
            GameObject target)
        {
            if (target == null)
                return false;

            if (!target.activeInHierarchy)
                return false;

            return true;
        }
    }
}