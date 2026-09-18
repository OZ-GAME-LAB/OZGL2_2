using System;
using Units.Effects;
using UnityEngine;



namespace Units
{
    public class Unit_Core : MonoBehaviour
    {
        // ============================================================
        // Components
        // ============================================================

        [SerializeField]
        private Unit_Gateway _gateway;

        [SerializeField]
        private Unit_RuntimeStatus _runtimeStatus;

        [SerializeField]
        private Unit_Life _life;

        [SerializeField]
        private Unit_Movement _movement;

        [SerializeField]
        private Unit_Combat _combat;

        [SerializeField]
        private Unit_Animation _animation;

        [SerializeField]
        private Unit_Detection _detection;

        [SerializeField]
        private Unit_AI _ai;


        // ============================================================
        // Team
        // ============================================================

        [SerializeField]
        private UnitTeam _team;


        // ============================================================
        // Properties
        // ============================================================

        public UnitTeam Team
            => _team;

        public Unit_RuntimeStatus RuntimeStatus
            => _runtimeStatus;

        public bool IsAlive
            => _life != null
            && !_life.IsDead;

        public bool CanMove
            => _movement != null
            && _movement.CanMove;

        public bool CanUseBasicAttack
            => _combat != null
            && _combat.CanUseBasicAttack;

        public bool CanUseActiveSkill
            => _combat != null
            && _combat.CanUseActiveSkill;


        public float PreferredCombatRange
            => _combat != null
                ? _combat.PreferredCombatRange
                : 0f;

        public float CurrentHp
            => _life != null
                ? _life.CurrentHp
                : 0f;


        // ============================================================
        // Events
        // ============================================================

        public event Action MovementCompleted;

        public event Action MovementFailed;

        public event Action BasicAttackCompleted;

        public event Action ActiveSkillCompleted;


        // ============================================================
        // Event Notification
        // ============================================================

        public void NotifyMovementCompleted()
        {
            MovementCompleted?.Invoke();
        }

        public void NotifyMovementFailed()
        {
            MovementFailed?.Invoke();
        }


        public void NotifyBasicAttackCompleted()
        {
            BasicAttackCompleted?.Invoke();
        }


        public void NotifyActiveSkillCompleted(
            bool reevaluateAfterMovement = false)
        {
            if (reevaluateAfterMovement)
            {
                _ai?.QueueTargetReevaluation();
            }

            ActiveSkillCompleted?.Invoke();
        }

        public SkillEngagementResult RequestSkillEngagement(
            ICombatTarget target)
        {
            return _gateway != null
                ? _gateway.RequestSkillEngagement(
                    target
                )
                : SkillEngagementResult.Invalid;
        }

        public Predicate<ICombatTarget> CaptureSkillTargetFilter()
        {
            return _gateway != null
                ? _gateway.CaptureSkillTargetFilter()
                : RejectSkillTarget;
        }

        private static bool RejectSkillTarget(
            ICombatTarget target)
            => false;


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void OnDestroy()
        {
            UnbindComponentEvents();
        }


        // ============================================================
        // Initialize
        // ============================================================

        public void Initialize(
            FinalStatModifier spawnModifier)
        {
            InitComponents();

            UnbindComponentEvents();


            if (_gateway != null)
            {
                _gateway.Initialize(
                    this
                );
            }


            _runtimeStatus.Initialize(
                spawnModifier
            );


            if (_runtimeStatus.UnitData != null)
            {
                _team =
                    _runtimeStatus.UnitData.Team;
            }


            if (_life != null)
            {
                _life.Initialize(
                    this
                );
            }


            if (_movement != null)
            {
                _movement.Initialize(
                    this
                );
            }


            if (_combat != null)
            {
                _combat.Initialize(
                    this
                );
            }


            if (_animation != null)
            {
                _animation.Initialize(
                    this
                );
            }


            if (_detection != null)
            {
                _detection.Initialize(
                    this
                );
            }


            if (_ai != null)
            {
                _ai.Initialize(
                    this
                );
            }


            BindComponentEvents();

            RefreshStatusRestrictions();
        }


        private void InitComponents()
        {
            if (_gateway == null)
            {
                _gateway =
                    GetComponent<Unit_Gateway>();
            }


            if (_runtimeStatus == null)
            {
                _runtimeStatus =
                    GetComponent<Unit_RuntimeStatus>();
            }


            if (_life == null)
            {
                _life =
                    GetComponent<Unit_Life>();
            }


            if (_movement == null)
            {
                _movement =
                    GetComponent<Unit_Movement>();
            }


            if (_combat == null)
            {
                _combat =
                    GetComponent<Unit_Combat>();
            }


            if (_animation == null)
            {
                _animation =
                    GetComponent<Unit_Animation>();
            }


            if (_detection == null)
            {
                _detection =
                    GetComponent<Unit_Detection>();
            }


            if (_ai == null)
            {
                _ai =
                    GetComponent<Unit_AI>();
            }
        }


        // ============================================================
        // Component Events
        // ============================================================

        private void BindComponentEvents()
        {
            if (_life != null)
            {
                _life.Damaged +=
                    OnDamaged;
            }


            if (_runtimeStatus != null)
            {
                _runtimeStatus.StatusChanged +=
                    OnStatusChanged;
            }
        }


        private void UnbindComponentEvents()
        {
            if (_life != null)
            {
                _life.Damaged -=
                    OnDamaged;
            }


            if (_runtimeStatus != null)
            {
                _runtimeStatus.StatusChanged -=
                    OnStatusChanged;
            }
        }


        // ============================================================
        // Status Restriction
        // ============================================================

        private void OnStatusChanged(
            UnitStatusEffectType statusType,
            bool isActive)
        {
            RefreshStatusRestrictions();
        }


        private void RefreshStatusRestrictions()
        {
            if (_runtimeStatus == null)
                return;


            bool isStunned =
                _runtimeStatus.HasStatus(
                    UnitStatusEffectType.Stun
                );


            bool isRooted =
                _runtimeStatus.HasStatus(
                    UnitStatusEffectType.Root
                );


            bool isSilenced =
                _runtimeStatus.HasStatus(
                    UnitStatusEffectType.Silence
                );


            // Stun과 Root는 모두 이동을 차단한다.
            _movement?.SetMovementBlocked(
                isStunned
                || isRooted
            );


            // 현재 정의에서 BasicAttack을 차단하는 상태는 Stun이다.
            _combat?.SetBasicAttackBlocked(
                isStunned
            );


            // Stun과 Silence는 ActiveSkill을 차단한다.
            _combat?.SetActiveSkillBlocked(
                isStunned
                || isSilenced
            );
        }


        // ============================================================
        // Movement
        // ============================================================

        public void MoveTo(
            Vector2 targetPosition)
        {
            if (_movement == null)
                return;


            _movement.MoveTo(
                targetPosition
            );
        }


        public void StopMovement()
        {
            if (_movement == null)
                return;


            _movement.Stop();
        }


        public void HoldMovementPosition(
            Vector2 position)
        {
            if (_movement == null)
                return;


            _movement.HoldPosition(
                position
            );
        }


        public void ReleaseMovementPosition()
        {
            if (_movement == null)
                return;


            _movement.ReleasePosition();
        }


        // ============================================================
        // Combat
        // ============================================================

        public bool TryBasicAttack(
            ICombatTarget target)
        {
            if (_combat == null)
                return false;


            return _combat.TryBasicAttack(
                target
            );
        }


        public bool TryActiveSkill(
            ICombatTarget target)
        {
            if (_combat == null)
                return false;


            return _combat.TryActiveSkill(
                target
            );
        }


        public void NotifyCombatPositionChanged()
        {
            NotifyTargetReevaluation();
        }


        // ============================================================
        // Combat Data
        // ============================================================

        public float GetBasicAttackRange()
        {
            if (_runtimeStatus == null)
                return 0f;


            if (_runtimeStatus.BasicAttackData == null)
                return 0f;


            return _runtimeStatus
                .BasicAttackData
                .BasicAttackRange;
        }


        public float GetSkillRange()
        {
            if (_runtimeStatus == null)
                return 0f;


            if (_runtimeStatus.ActiveSkillData == null)
                return 0f;


            return _runtimeStatus
                .ActiveSkillData
                .SkillRange;
        }


        // ============================================================
        // Life
        // ============================================================

        public void TakeDamage(
            DamageResult result)
        {
            if (_life == null)
                return;


            _life.TakeDamage(
                result
            );
        }


        public void Heal(
            float amount)
        {
            if (_life == null)
                return;


            _life.Heal(
                amount
            );
        }


        public void AddShield(
            float amount)
        {
            if (_life == null)
                return;


            _life.AddShield(
                amount
            );
        }


        public void NotifyDeath()
        {
            _ai?.Stop();

            _movement?.Stop();

            _combat?.Stop();

            _animation?.PlayAnimation_Death();

            _gateway?.NotifyDeath();
        }


        private void OnDamaged(
            DamageResult result)
        {
            NotifyTargetReevaluation();
        }


        // ============================================================
        // Animation
        // ============================================================

        public void PlayAnimation_Move()
        {
            if (_animation == null)
                return;


            _animation.PlayAnimation_Move();
        }


        public void PlayAnimation_Attack()
        {
            if (_animation == null)
                return;


            _animation.PlayAnimation_Attack();
        }


        public void PlayAnimation_Skill()
        {
            if (_animation == null)
                return;


            _animation.PlayAnimation_Skill();
        }


        public void PlayAnimation_Hit()
        {
            if (_animation == null)
                return;


            _animation.PlayAnimation_Hit();
        }


        public void PlayAnimation_Death()
        {
            if (_animation == null)
                return;


            _animation.PlayAnimation_Death();
        }


        // ============================================================
        // Detection
        // ============================================================

        public float GetDistanceToTarget(
            GameObject target)
        {
            if (_detection == null)
                return float.MaxValue;


            return _detection.GetDistanceToTarget(
                target
            );
        }


        public bool CanMoveStraightToTarget(
            GameObject target)
        {
            if (_detection == null)
                return false;


            return _detection.CanMoveStraightToTarget(
                target
            );
        }


        // ============================================================
        // AI
        // ============================================================

        public void StartAI()
        {
            _ai?.StartAI();
        }


        public void PauseAI()
        {
            _ai?.PauseAI();
        }


        public void SetUnitAssignment(
            UnitAssignment assignment)
        {
            if (_ai == null)
                return;


            _ai.SetUnitAssignment(
                assignment
            );
        }


        public void ClearUnitAssignment()
        {
            if (_ai == null)
                return;


            _ai.ClearUnitAssignment();
        }


        private void NotifyTargetReevaluation()
        {
            _ai?.NotifyTargetReevaluation();
        }


        public void RequestFullAssignment()
        {
            _gateway?.RequestFullAssignment();
        }


        public void RequestTargetReevaluation()
        {
            _gateway?.RequestTargetReevaluation();
        }


        public void RequestPositionAssignment()
        {
            _gateway?.RequestPositionAssignment();
        }


        // ============================================================
        // Unit Control
        // ============================================================

        public void Pause()
        {
            _ai?.PauseAI();

            _movement?.Stop();

            _combat?.Pause();
        }


        public void Resume()
        {
            _combat?.Resume();

            _ai?.StartAI();
        }
    }
}