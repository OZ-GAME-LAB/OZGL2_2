using System;
using System.Collections.Generic;
using Units.Skills;
using UnityEngine;



namespace Units
{
    [DisallowMultipleComponent]
    public class Unit_Gateway :
        MonoBehaviour,
        ICombatTarget
    {
        // ============================================================
        // Reference
        // ============================================================

        private Unit_Core _core;

        private Unit_GroupAI _groupAI;

        public int LifetimeVersion { get; private set; }


        // ============================================================
        // Runtime State
        // ============================================================

        private bool _isRallyMoving;

        private Vector2 _rallyPosition;

        private bool _isMovingToRallyVia;


        // ============================================================
        // Events
        // ============================================================

        public event Action<Unit_Gateway> Died;

        public event Action<CombatStateChange> CombatStateChanged;

        public void NotifySkillEvent(CombatSkillEvent notification) => _core?.NotifySkillEvent(notification);

        internal void NotifyCombatStateChanged(CombatStateChange change) => CombatStateChanged?.Invoke(change);

        public event Action<CombatDeathResult> DeathConfirmed;

        public event Action<Unit_Gateway> RequestFullAssignmentEvent;

        public event Action<Unit_Gateway> RequestTargetReevaluationEvent;

        public event Action<Unit_Gateway> RequestPositionAssignmentEvent;

        public event Action<Unit_Gateway> RallyCompleted;

        public event Action<Unit_Gateway> RallyFailed;

        public event Action<float, float> HpChanged;

        public event Action<float, float> ShieldChanged;


        // ============================================================
        // Properties
        // ============================================================

        public Unit_Core Core => _core;

        public bool IsRallyMoving => _isRallyMoving;

        public UnitTeam Team => _core != null ? _core.Team : default;

        public Unit_RuntimeStatus RuntimeStatus => _core != null ? _core.RuntimeStatus : default;

        public Unit_GroupAI GroupAI => _groupAI;

        public float BodyRadius => _core != null ? _core.BodyRadius : CombatRangeUtility.StandardBodyRadius;

        public Transform Transform => transform;

        public Vector2 FacingDirection => _core != null ? _core.FacingDirection : Vector2.left;

        public bool IsAlive => _core != null && _core.IsAlive;

        public bool IsTargetable => isActiveAndEnabled && IsAlive;

        public bool CanUseActiveSkill => _core != null && _core.CanUseActiveSkill;

        public float PreferredCombatRange => _core != null ? _core.PreferredCombatRange : 0f;

        public float EngagementEntryRange => _core != null ? _core.EngagementEntryRange : 0f;

        public float CurrentHp => _core != null ? _core.CurrentHp : 0f;

        public float CurrentShield => _core != null ? _core.CurrentShield : 0f;

        public float MaxHp => _core?.RuntimeStatus?.MaxHp ?? 0f;


        // ============================================================
        // Initialize
        // ============================================================

        internal void Initialize(Unit_Core core)
        {
            if (core == null)
            {
                Debug.LogError($"[Unit_Gateway] {name} : Unit_Core가 없습니다.");

                return;
            }

            UnbindEvents();

            LifetimeVersion++;

            NotifyCombatStateChanged(CombatStateChange.Lifetime);

            _core = core;

            ResetRally();

            BindEvents();
        }


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        protected virtual void OnDisable()
        {
            ResetRally();

            NotifyCombatStateChanged(CombatStateChange.Lifetime);
        }

        protected virtual void OnDestroy()
        {
            NotifyCombatStateChanged(CombatStateChange.Lifetime);

            UnbindEvents();
        }


        // ============================================================
        // Events
        // ============================================================

        private void BindEvents()
        {
            if (_core == null)
                return;

            _core.MovementCompleted += OnMovementCompleted;

            _core.MovementFailed += OnMovementFailed;
        }

        private void UnbindEvents()
        {
            if (_core == null)
                return;

            _core.MovementCompleted -= OnMovementCompleted;

            _core.MovementFailed -= OnMovementFailed;
        }

        private void OnMovementCompleted()
        {
            if (!_isRallyMoving)
                return;

            if (_isMovingToRallyVia)
            {
                _isMovingToRallyVia = false;

                _core.MoveToRally(_rallyPosition);

                return;
            }

            Vector2 destination = _rallyPosition;

            ResetRally();

            _core?.HoldMovementPosition(destination);

            RallyCompleted?.Invoke(this);
        }

        private void OnMovementFailed()
        {
            if (!_isRallyMoving)
                return;

            string stage = _isMovingToRallyVia ? "Via" : "Destination";

            ResetRally();

            Debug.LogWarning($"[Unit_Gateway] {name} Rally movement failed: {stage}.");

            RallyFailed?.Invoke(this);
        }


        // ============================================================
        // Group
        // ============================================================

        internal SkillEngagementResult RequestSkillEngagement(ICombatTarget target)
        {
            return _groupAI != null ? _groupAI.RequestSkillEngagement(this, target) : SkillEngagementResult.Invalid;
        }

        internal Predicate<ICombatTarget> CaptureSkillTargetFilter()
        {
            return _groupAI != null ? _groupAI.CaptureSkillTargetFilter(this) : RejectSkillTarget;
        }

        private static bool RejectSkillTarget(ICombatTarget target) => false;

        internal void SetGroupAI(Unit_GroupAI groupAI)
        {
            _groupAI = groupAI;
        }

        internal void ClearGroupAI()
        {
            _groupAI = null;
        }

        // 클릭시 UI에 정보를 표시하기 위해 그룹 멤버를 가져오는 메서드
        public IReadOnlyList<Unit_Gateway> GetGroupMembers()
        {
            return _groupAI != null ? _groupAI.Members: new List<Unit_Gateway>();
        }


        // ============================================================
        // Assignment
        // ============================================================

        public void SetUnitAssignment(UnitAssignment assignment)
        {
            if (_core == null)
                return;

            _core.SetUnitAssignment(assignment);
        }

        public void ClearUnitAssignment()
        {
            if (_core == null)
                return;

            _core.ClearUnitAssignment();
        }

        public void RequestFullAssignment()
        {
            RequestFullAssignmentEvent?.Invoke(this);
        }

        public void RequestTargetReevaluation()
        {
            RequestTargetReevaluationEvent?.Invoke(this);
        }

        public void RequestPositionAssignment()
        {
            RequestPositionAssignmentEvent?.Invoke(this);
        }


        // ============================================================
        // AI
        // ============================================================

        public void StartAI()
        {
            if (_core == null)
                return;

            _core.ReleaseMovementPosition();

            _core.StartAI();
        }

        public void PauseAI()
        {
            _core?.PauseAI();
        }


        // ============================================================
        // Life
        // ============================================================

        public CombatApplicationResult TakeDamageWithResult(DamageResult result)
        {
            return _core != null ? _core.TakeDamageWithResult(result) : CombatApplicationResult.Invalid(
                CombatApplicationKind.Damage,
                this,
                result.Metadata,
                "Life module unavailable"
            );
        }

        public CombatApplicationResult HealWithResult(
            float amount,
            CombatEventMetadata metadata)
        {
            return _core != null ? _core.HealWithResult(amount, metadata) : CombatApplicationResult.Invalid(
                CombatApplicationKind.Heal,
                this,
                metadata,
                "Life module unavailable"
            );
        }

        public CombatApplicationResult AddShieldWithResult(
            float amount,
            CombatEventMetadata metadata)
        {
            return _core != null ? _core.AddShieldWithResult(amount, metadata) : CombatApplicationResult.Invalid(
                CombatApplicationKind.Shield,
                this,
                metadata,
                "Life module unavailable"
            );
        }

        public event Action<SkillExecutionResult> SkillExecutionEnded;

        internal void NotifySkillExecutionEnded(SkillExecutionResult result) => SkillExecutionEnded?.Invoke(result);

        public bool TryConsumeEffectStacks(Units.Effects.EffectStackConsumeRequest request) => _core != null && _core.TryConsumeEffectStacks(request);

        public void TakeDamage(DamageResult result)
        {
            _core?.TakeDamage(result);
        }

        public void Heal(float amount)
        {
            _core?.Heal(amount);
        }

        public void AddShield(float amount)
        {
            _core?.AddShield(amount);
        }

        internal void NotifyDeathConfirmed(CombatDeathResult result)
        {
            DeathConfirmed?.Invoke(result);
        }

        internal void NotifyDeath()
        {
            ResetRally();

            Died?.Invoke(this);
        }

        public void NotifyHpChanged(
            float previousHp,
            float currentHp)
        {
            HpChanged?.Invoke(
                previousHp,
                currentHp
            );
        }


        public void NotifyShieldChanged(
            float previousShield,
            float currentShield)
        {
            ShieldChanged?.Invoke(
                previousShield,
                currentShield
            );
        }


        // ============================================================
        // Passive
        // ============================================================

        public void CollectDamageModifiers(
            PassiveDamageOwnerType ownerType,
            List<PassiveDamageModifier> results)
        {
            _core?.CollectDamageModifiers(ownerType, results);
        }

        public bool EvaluateDamageModifierConditions(
            RuntimePassiveSkill runtimePassive,
            ICombatTarget target,
            CombatSourceSnapshot frozenTarget = null)
        {
            return _core != null && _core.EvaluateDamageModifierConditions(
                runtimePassive,
                target,
                frozenTarget
            );
        }

        public void NotifyDamageDealt(ICombatTarget target)
        {
            _core?.NotifyDamageDealt(target);
        }


        // ============================================================
        // Advance
        // ============================================================

        public void MoveTo(Vector2 destination)
        {
            ResetRally();

            _core?.MoveTo(destination);
        }

        public void StopMovement()
        {
            ResetRally();

            _core?.StopMovement();
        }


        // ============================================================
        // Rally
        // ============================================================

        public void MoveToRally(Vector2 destination)
        {
            BeginRally(destination, destination, false);
        }

        public void MoveToRally(Vector2 viaPoint, Vector2 destination)
        {
            BeginRally(viaPoint, destination, true);
        }

        private void BeginRally(Vector2 firstPoint, Vector2 destination, bool useVia)
        {
            if (_core == null)
                return;

            _rallyPosition = destination;

            _isMovingToRallyVia = useVia;

            _isRallyMoving = true;

            _core.MoveToRally(firstPoint, useVia);
        }

        private void ResetRally()
        {
            _isRallyMoving = false;

            _isMovingToRallyVia = false;

            _rallyPosition = default;
        }


        // ============================================================
        // Animation
        // ============================================================

        public void PlayAnimation_Victory()
        {
            _core?.PlayAnimation_Victory();
        }


        // ============================================================
        // Unit Control
        // ============================================================

        public void Pause()
        {
            _core?.Pause();
        }

        public void Resume()
        {
            _core?.Resume();
        }
    }
}
