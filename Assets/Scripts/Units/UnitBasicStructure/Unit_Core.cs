using System;
using System.Collections.Generic;
using Units.Effects;
using Units.Skills;
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

        [SerializeField]
        private Unit_Passive _passive;


        // ============================================================
        // Team
        // ============================================================

        [SerializeField]
        private UnitTeam _team;

        private Vector2 _facingDirection = Vector2.left;


        // ============================================================
        // Properties
        // ============================================================

        public UnitTeam Team => _team;

        public Vector2 FacingDirection => _facingDirection;

        public ICombatTarget CombatTarget => _gateway;

        public Unit_RuntimeStatus RuntimeStatus => _runtimeStatus;

        public bool IsAlive => _life != null && !_life.IsDead;

        public bool CanMove => _movement != null && _movement.CanMove;

        public bool CanUseBasicAttack => _combat != null && _combat.CanUseBasicAttack;

        public bool CanUseActiveSkill => _combat != null && _combat.CanUseActiveSkill;

        public float PreferredCombatRange => _combat != null ? _combat.PreferredCombatRange : 0f;

        public float CurrentHp => _life != null ? _life.CurrentHp : 0f;

        public float CurrentShield => _life != null ? _life.CurrentShield : 0f;


        // ============================================================
        // Events
        // ============================================================

        public event Action MovementCompleted;

        public event Action MovementFailed;

        public event Action BasicAttackCompleted;

        public event Action ActiveSkillCompleted;

        public event Action<CombatDeathResult> DeathConfirmed;


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

        public event Action<SkillExecutionResult> SkillExecutionEnded;

        public void NotifySkillExecutionEnded(
            SkillExecutionResult result,
            bool moved)
        {
            var lifetime = new CombatTargetSnapshot(CombatTarget);

            SkillExecutionEnded?.Invoke(result);

            if (!lifetime.MatchesLifetime)
                return;

            _gateway?.NotifySkillExecutionEnded(result);

            if (!lifetime.MatchesLifetime)
                return;

            // 기존 AI 행동 종료는 결과 종류와 무관하다. Success 패시브 연결은 G4에서 분리한다.
            NotifyActiveSkillCompleted(moved);
        }

        public void NotifyActiveSkillCompleted(bool reevaluateAfterMovement = false)
        {
            if (reevaluateAfterMovement)
            {
                _ai?.QueueTargetReevaluation();
            }

            ActiveSkillCompleted?.Invoke();
        }

        public SkillEngagementResult RequestSkillEngagement(ICombatTarget target)
        {
            return _gateway != null ? _gateway.RequestSkillEngagement(target) : SkillEngagementResult.Invalid;
        }

        public Predicate<ICombatTarget> CaptureSkillTargetFilter()
        {
            return _gateway != null ? _gateway.CaptureSkillTargetFilter() : RejectSkillTarget;
        }

        private static bool RejectSkillTarget(ICombatTarget target) => false;


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

        public void Initialize(FinalStatModifier spawnModifier)
        {
            Initialize(spawnModifier, null);
        }


        // 외부 패시브는 이 수명의 초기화 시점에만 받는다.
        public void Initialize(
            FinalStatModifier spawnModifier,
            IReadOnlyList<PassiveSkillData> spawnPassiveSkills)
        {
            _facingDirection = Vector2.left;

            InitComponents();

            UnbindComponentEvents();

            if (_gateway != null)
            {
                _gateway.Initialize(this);
            }

            _runtimeStatus.Initialize(spawnModifier, spawnPassiveSkills);

            if (_runtimeStatus.UnitData != null)
            {
                _team = _runtimeStatus.UnitData.Team;
            }

            if (_life != null)
            {
                _life.Initialize(this);
            }

            if (_movement != null)
            {
                _movement.Initialize(this);
            }

            if (_combat != null)
            {
                _combat.Initialize(this);
            }

            if (_animation != null)
            {
                _animation.Initialize(this);
            }

            if (_detection != null)
            {
                _detection.Initialize(this);
            }

            if (_ai != null)
            {
                _ai.Initialize(this);
            }

            if (_passive != null)
            {
                _passive.Initialize(
                    this,
                    _gateway,
                    _runtimeStatus.PassiveSkillDatas
                );
            }

            BindComponentEvents();

            _gateway?.NotifyCombatStateChanged(CombatStateChange.Status);

            RefreshStatusRestrictions();
        }

        private void InitComponents()
        {
            if (_gateway == null)
            {
                _gateway = GetComponent<Unit_Gateway>();
            }

            if (_runtimeStatus == null)
            {
                _runtimeStatus = GetComponent<Unit_RuntimeStatus>();
            }

            if (_life == null)
            {
                _life = GetComponent<Unit_Life>();
            }

            if (_movement == null)
            {
                _movement = GetComponent<Unit_Movement>();
            }

            if (_combat == null)
            {
                _combat = GetComponent<Unit_Combat>();
            }

            if (_animation == null)
            {
                _animation = GetComponent<Unit_Animation>();
            }

            if (_detection == null)
            {
                _detection = GetComponent<Unit_Detection>();
            }

            if (_ai == null)
            {
                _ai = GetComponent<Unit_AI>();
            }

            if (_passive == null)
            {
                _passive = GetComponent<Unit_Passive>();
            }
        }


        // ============================================================
        // Component Events
        // ============================================================

        private void BindComponentEvents()
        {
            if (_life != null)
            {
                _life.HpChanged += OnHpChanged;

                _life.ShieldChanged += OnShieldChanged;

                _life.Damaged += OnDamaged;
            }

            if (_runtimeStatus != null)
            {
                _runtimeStatus.StatusChanged += OnStatusChanged;

                _runtimeStatus.StatChanged += OnObservedStatChanged;

                _runtimeStatus.EffectsChanged += OnObservedEffectsChanged;
            }
        }

        private void UnbindComponentEvents()
        {
            if (_life != null)
            {
                _life.HpChanged -= OnHpChanged;

                _life.ShieldChanged -= OnShieldChanged;

                _life.Damaged -= OnDamaged;
            }

            if (_runtimeStatus != null)
            {
                _runtimeStatus.StatusChanged -= OnStatusChanged;

                _runtimeStatus.StatChanged -= OnObservedStatChanged;

                _runtimeStatus.EffectsChanged -= OnObservedEffectsChanged;
            }
        }


        // ============================================================
        // Status Restriction
        // ============================================================

        private void OnObservedStatChanged(
            UnitStatType stat,
            float before,
            float after) => _gateway?.NotifyCombatStateChanged(CombatStateChange.Stats);

        private void OnObservedEffectsChanged() => _gateway?.NotifyCombatStateChanged(CombatStateChange.Effects);

        private void OnStatusChanged(
            UnitStatusEffectType statusType,
            bool isActive)
        {
            _gateway?.NotifyCombatStateChanged(CombatStateChange.Status);

            RefreshStatusRestrictions();
        }

        private void RefreshStatusRestrictions()
        {
            if (_runtimeStatus == null)
                return;

            bool isStunned = _runtimeStatus.HasStatus(UnitStatusEffectType.Stun);

            bool isRooted = _runtimeStatus.HasStatus(UnitStatusEffectType.Root);

            bool isSilenced = _runtimeStatus.HasStatus(UnitStatusEffectType.Silence);

            // Stun과 Root는 모두 이동을 차단한다.
            _movement?.SetMovementBlocked(isStunned || isRooted);

            // 현재 정의에서 BasicAttack을 차단하는 상태는 Stun이다.
            _combat?.SetBasicAttackBlocked(isStunned);

            // Stun과 Silence는 ActiveSkill을 차단한다.
            _combat?.SetActiveSkillBlocked(isStunned || isSilenced);

            if (isStunned)
                PlayAnimation_Stun();
            else
                StopAnimation_Stun();
        }


        // ============================================================
        // Movement
        // ============================================================

        public void MoveTo(Vector2 targetPosition)
        {
            if (_movement == null)
                return;

            _movement.MoveTo(targetPosition);
        }

        public void StopMovement()
        {
            if (_movement == null)
                return;

            _movement.Stop();
        }

        public void HoldMovementPosition(Vector2 position)
        {
            if (_movement == null)
                return;

            _movement.HoldPosition(position);
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

        public bool TryBasicAttack(ICombatTarget target)
        {
            if (_combat == null)
                return false;

            return _combat.TryBasicAttack(target);
        }

        public bool TryActiveSkill(ICombatTarget target)
        {
            if (_combat == null)
                return false;

            return _combat.TryActiveSkill(target);
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

            return _runtimeStatus.BasicAttackData.BasicAttackRange;
        }

        public float GetSkillRange()
        {
            if (_runtimeStatus == null)
                return 0f;

            if (_runtimeStatus.ActiveSkillData == null)
                return 0f;

            return _runtimeStatus.ActiveSkillData.SkillRange;
        }


        // ============================================================
        // Life
        // ============================================================

        public CombatApplicationResult TakeDamageWithResult(DamageResult result)
        {
            return _life != null ? _life.TakeDamageWithResult(result) : CombatApplicationResult.Invalid(
                CombatApplicationKind.Damage,
                CombatTarget,
                result.Metadata,
                "Life module unavailable"
            );
        }

        public CombatApplicationResult HealWithResult(
            float amount,
            CombatEventMetadata metadata)
        {
            return _life != null ? _life.HealWithResult(amount, metadata) : CombatApplicationResult.Invalid(
                CombatApplicationKind.Heal,
                CombatTarget,
                metadata,
                "Life module unavailable"
            );
        }

        public CombatApplicationResult AddShieldWithResult(
            float amount,
            CombatEventMetadata metadata)
        {
            return _life != null ? _life.AddShieldWithResult(amount, metadata) : CombatApplicationResult.Invalid(
                CombatApplicationKind.Shield,
                CombatTarget,
                metadata,
                "Life module unavailable"
            );
        }

        public bool TryConsumeEffectStacks(Units.Effects.EffectStackConsumeRequest request) => ReferenceEquals(request.Target.Target, CombatTarget) && Units.Effects.RuntimeEffectManager.Instance != null && Units.Effects.RuntimeEffectManager.Instance.TryConsumeStacks(request);

        public void TakeDamage(DamageResult result)
        {
            if (_life == null)
                return;

            _life.TakeDamage(result);
        }

        public void Heal(float amount)
        {
            if (_life == null)
                return;

            _life.Heal(amount);
        }

        public void AddShield(float amount)
        {
            if (_life == null)
                return;

            _life.AddShield(amount);
        }

        public void NotifySkillEvent(CombatSkillEvent notification)
        {
            if (notification != null && notification.Metadata.Owner.IsTargetable && ReferenceEquals(notification.Metadata.Owner.Target, CombatTarget))
                _passive?.NotifySkillEvent(notification);
        }

        internal void NotifyKillAttributed(CombatDeathResult result)
        {
            // 확정한 치명 사건당 한 번만 전달한다. 관찰자 재진입/풀 재사용보다 먼저 귀속을 검사한다.
            if (result != null && result.CanNotifyKiller && result.TryMarkKillerNotified())
                new CombatSkillEvent(
                    PassiveSkillTriggerType.EnemyKilled,
                    result.Metadata,
                    position: result.Victim.Position,
                    death: result
                ).Notify();
        }

        public void NotifyDeathConfirmed(CombatDeathResult result)
        {
            NotifyKillAttributed(result);

            DeathConfirmed?.Invoke(result);

            _gateway?.NotifyDeathConfirmed(result);
        }

        public void NotifyDeath()
        {
            _gateway?.NotifyCombatStateChanged(CombatStateChange.Lifetime);

            _ai?.Stop();

            _movement?.Stop();

            _combat?.Stop();

            _animation?.PlayAnimation_Death();

            _passive?.Stop();

            _gateway?.NotifyDeath();
        }

        private void OnHpChanged(
            float previousHp,
            float currentHp)
        {
            _gateway?.NotifyCombatStateChanged(CombatStateChange.Health);

            _passive?.NotifyHealthChanged();
        }

        private void OnShieldChanged(
            float previousShield,
            float currentShield)
        {
            _gateway?.NotifyCombatStateChanged(CombatStateChange.Shield);

            _passive?.NotifyShieldChanged();
        }

        private void OnDamaged(DamageResult result)
        {
            if (!result.TargetSnapshot.MatchesLifetime)
                return;

            if (result.Damage > 0f)
                PlayAnimation_Hit();

            NotifyTargetReevaluation();

            if (!result.TargetSnapshot.MatchesLifetime)
                return;

            // 피격자 패시브
            _passive?.NotifyDamageTaken(result.Metadata.Owner.IsTargetable ? result.Attacker : null);

            // DoT 피해는 공격자 패시브를 발동시키지 않는다.
            if (result.SourceType == DamageSourceType.Dot)
            {
                return;
            }

            // 공격자 패시브
            if (!result.TargetSnapshot.MatchesLifetime || !result.Metadata.Owner.MatchesLifetime || !result.Metadata.Owner.IsTargetable)
                return;

            result.Attacker?.NotifyDamageDealt(_gateway);
        }


        // ============================================================
        // Animation
        // ============================================================

        public void SetFacingDirection(Vector2 direction)
        {
            if (!IsAlive || float.IsNaN(direction.x) || float.IsInfinity(direction.x)
                || float.IsNaN(direction.y) || float.IsInfinity(direction.y)
                || Mathf.Abs(direction.x) <= 0.01f)
                return;

            _facingDirection = direction.x > 0f ? Vector2.right : Vector2.left;

            _animation?.SetFacingDirection(_facingDirection);
        }


        public void SetMovementFacingDirection(Vector2 velocity)
        {
            if ((_combat != null && _combat.IsBusy)
                || (_animation != null && _animation.IsFacingLocked))
                return;

            SetFacingDirection(velocity);
        }


        public void PlayAnimation_Idle()
        {
            if (_animation == null)
                return;

            _animation.PlayAnimation_Idle();
        }

        public void PlayAnimation_Dash()
        {
            if (_animation == null)
                return;

            _animation.PlayAnimation_Dash();
        }

        public void PlayAnimation_Cast()
        {
            if (_animation == null)
                return;

            _animation.PlayAnimation_Cast();
        }

        public void PlayAnimation_Buff()
        {
            if (_animation == null)
                return;

            _animation.PlayAnimation_Buff();
        }

        public void PlayAnimation_Victory()
        {
            if (_animation == null)
                return;

            _animation.PlayAnimation_Victory();
        }

        public void PlayAnimation_Stun()
        {
            if (_animation == null)
                return;

            _animation.PlayAnimation_Stun();
        }

        public void StopAnimation_Stun()
        {
            if (_animation == null)
                return;

            _animation.StopAnimation_Stun();
        }

        public void StopAnimation_SkillMotion()
        {
            if (_animation == null)
                return;

            _animation.StopAnimation_SkillMotion();
        }

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

        public float GetDistanceToTarget(GameObject target)
        {
            if (_detection == null)
                return float.MaxValue;

            return _detection.GetDistanceToTarget(target);
        }

        public bool CanMoveStraightToTarget(GameObject target)
        {
            if (_detection == null)
                return false;

            return _detection.CanMoveStraightToTarget(target);
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

        public void SetUnitAssignment(UnitAssignment assignment)
        {
            if (_ai == null)
                return;

            _ai.SetUnitAssignment(assignment);
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
        // Passive
        // ============================================================

        public void CollectDamageModifiers(
            PassiveDamageOwnerType ownerType,
            List<PassiveDamageModifier> results)
        {
            _passive?.CollectDamageModifiers(ownerType, results);
        }

        public bool EvaluateDamageModifierConditions(
            RuntimePassiveSkill runtimePassive,
            ICombatTarget target,
            CombatSourceSnapshot frozenTarget = null)
        {
            return _passive != null && _passive.EvaluateDamageModifierConditions(
                runtimePassive,
                target,
                frozenTarget
            );
        }

        public void NotifyDamageDealt(ICombatTarget target)
        {
            _passive?.NotifyDamageDealt(target);
        }


        // ============================================================
        // Unit Control
        // ============================================================

        public void Pause()
        {
            _ai?.PauseAI();

            _movement?.Stop();

            _combat?.Pause();

            _passive?.Pause();
        }

        public void Resume()
        {
            _combat?.Resume();

            _passive?.Resume();

            _ai?.StartAI();
        }


    }
}
