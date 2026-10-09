using System;
using System.Collections.Generic;
using UnityEngine;

// Cast·Dash·Attack의 동작 및 효과 설정을 정의하고 기존 스킬을 실행 계획으로 변환한다.
namespace Units.Skills
{
    public enum SkillTargetLostPolicy
    {
        Fail,
        Skip,
        Reselect
    }
    public enum SkillFailurePolicy
    {
        Stop,
        Skip
    }
    public enum SkillAreaOrigin
    {
        Owner,
        Target,
        PreviousResult
    }

    [Serializable]
    public abstract class SkillActionData
    {
        // ============================================================
        // Target / Failure
        // ============================================================
        [SerializeField]
        private SkillTargetSettings _target = new(
            SkillTargetSource.InitialTarget,
            SkillTargetRelation.Hostile,
            false,
            3f,
            1,
            1f
        );

        [SerializeField]
        private SkillTargetLostPolicy _targetLostPolicy;

        [SerializeField]
        private SkillFailurePolicy _failurePolicy;

        [SerializeField]
        private SkillAreaOrigin _origin;

        [SerializeReference]
        private List<SkillConditionData> _conditions = new();

        [SerializeField]
        private List<SkillEffectEntry> _baseEffects = new();

        [SerializeField]
        private List<SkillConditionalEffectEntry> _conditionalEffects = new();

        [SerializeField]
        private List<SkillFXEntry> _fxEntries = new();

        // ============================================================
        // Properties
        // ============================================================

        public SkillTargetSettings Target => _target;

        public SkillTargetLostPolicy TargetLostPolicy => _targetLostPolicy;

        public SkillFailurePolicy FailurePolicy => _failurePolicy;

        public SkillAreaOrigin Origin => _origin;

        public IReadOnlyList<SkillConditionData> Conditions => _conditions;

        public IReadOnlyList<SkillEffectEntry> BaseEffects => _baseEffects;

        public IReadOnlyList<SkillConditionalEffectEntry> ConditionalEffects => _conditionalEffects;

        public IReadOnlyList<SkillFXEntry> FXEntries => _fxEntries;

        public virtual bool IsConfigured => _target != null;

        // ============================================================
        // Execution
        // ============================================================

        internal void SetLegacyTarget(SkillTargetSettings target)
        {
            _target = target;
        }

        internal void AddLegacyEffects(IReadOnlyList<SkillEffectData> effects)
        {
            _baseEffects.Add(new SkillEffectEntry(1, SkillEffectTiming.OnHit, effects));
        }
    }

    [Serializable]
    public sealed class SkillCastActionData : SkillActionData
    {

        // ============================================================
        // Data / Runtime State
        // ============================================================

        [SerializeField, Min(0f)]
        private float _duration = 1f;

        // ============================================================
        // Properties
        // ============================================================

        public float Duration => _duration;

        // ============================================================
        // Constructor
        // ============================================================

        public SkillCastActionData()
        {
        }

        internal SkillCastActionData(float duration)
        {
            _duration = duration;
        }

        // ============================================================
        // Properties
        // ============================================================

        public override bool IsConfigured => base.IsConfigured && _duration >= 0f && !float.IsInfinity(_duration);
    }

    [Serializable]
    public sealed class SkillDashActionData : SkillActionData
    {

        // ============================================================
        // Data / Runtime State
        // ============================================================

        [SerializeField, Min(0f)]
        private float _distance = 3f;

        [SerializeField, Min(0f)]
        private float _speed = 10f;

        // ============================================================
        // Properties
        // ============================================================

        public float Distance => _distance;

        public float Speed => Mathf.Min(_speed, 20f);

        // ============================================================
        // Constructor
        // ============================================================

        public SkillDashActionData()
        {
        }

        internal SkillDashActionData(
            float distance,
            float speed)
        {
            _distance = distance;

            _speed = speed;
        }

        // ============================================================
        // Properties
        // ============================================================

        public override bool IsConfigured => base.IsConfigured && _distance > 0f && _speed > 0f && !float.IsInfinity(_distance) && !float.IsInfinity(_speed) && Target.Source != SkillTargetSource.None;
    }

    [Serializable]
    public sealed class SkillAttackActionData : SkillActionData
    {

        // ============================================================
        // Data / Runtime State
        // ============================================================

        [SerializeField]
        private ActiveSkillDeliveryType _delivery;

        [SerializeField, Min(0f), Tooltip("공격 시작부터 다음 액션까지의 기준 시간(초). 공격 속도 보정 후에도 실제 행동 시간은 최소 0.1초입니다.")]
        private float _executionDuration = 0.3f;

        public float ExecutionDuration => _executionDuration;

        [SerializeField]
        private ActiveSkillAreaType _area;

        [SerializeField, Min(0f)]
        private float _radius = 1f;

        [SerializeField, Range(0f, 360f)]
        private float _angle = 90f;

        [SerializeField, Min(1)]
        private int _maxEffectTargets = 1;

        [SerializeField, Min(0f)]
        private float _projectileSpeed = 10f;

        // 비워두면 ProjectileManager의 공용 투사체를 사용한다.
        [SerializeField, Tooltip("이 공격 전용 투사체 프리팹. 루트의 Projectile_Controller를 지정합니다.")]
        private Projectile_Controller _projectilePrefab;

        // ============================================================
        // Properties
        // ============================================================

        public ActiveSkillDeliveryType Delivery => _delivery;

        public ActiveSkillAreaType Area => _area;

        public float Radius => _radius;

        public float Angle => _angle;

        public int MaxEffectTargets => _maxEffectTargets;

        public Projectile_Controller ProjectilePrefab => _projectilePrefab;

        public float ProjectileSpeed => _projectileSpeed;

        // ============================================================
        // Data / Runtime State
        // ============================================================

        [SerializeField, HideInInspector]
        private bool _isLegacy;

        // ============================================================
        // Properties
        // ============================================================

        internal bool IsLegacy => _isLegacy;

        // ============================================================
        // Constructor
        // ============================================================

        public SkillAttackActionData()
        {
        }

        internal SkillAttackActionData(ActiveSkillData legacy)
        {
            _executionDuration = 0f;
            _delivery = legacy.DeliveryType;

            _area = legacy.AreaType;

            _radius = legacy.AreaRadius;

            _angle = legacy.AreaAngle;

            _maxEffectTargets = legacy.MaxEffectTargetCount;

            _projectileSpeed = legacy.ProjectileSpeed;

            _projectilePrefab = legacy.ProjectilePrefab;

            _isLegacy = true;

            AddLegacyEffects(legacy.Effects);
        }

        // ============================================================
        // Properties
        // ============================================================

        public override bool IsConfigured => base.IsConfigured && _executionDuration >= 0f && !float.IsInfinity(_executionDuration) && _maxEffectTargets > 0 && (_delivery == ActiveSkillDeliveryType.Direct || (_delivery == ActiveSkillDeliveryType.Projectile && _projectileSpeed > 0f && !float.IsInfinity(_projectileSpeed))) && Target.Source != SkillTargetSource.None;
    }

    public static class SkillActionPlan
    {
        // 기존 SO는 변환/저장하지 않고 실행 시 Cast/Dash + Attack으로 어댑트한다.
        public static IReadOnlyList<SkillActionData> Create(ActiveSkillData data)
        {
            if (data.Actions.Count > 0)
                return new List<SkillActionData>(data.Actions).AsReadOnly();

            var actions = new List<SkillActionData>();

            if (data.ActionType == ActiveSkillActionType.Cast)
                actions.Add(new SkillCastActionData(data.CastTime));

            else if (data.ActionType == ActiveSkillActionType.Dash)
                actions.Add(new SkillDashActionData(data.DashDistance, data.DashSpeed));

            else if (data.ActionType != ActiveSkillActionType.Instant)
                return actions.AsReadOnly();

            actions.Add(new SkillAttackActionData(data));

            foreach (var action in actions)
                action.SetLegacyTarget(new SkillTargetSettings(SkillTargetSource.InitialTarget, data.TargetSide, true, data.SkillRange, action is SkillAttackActionData ? data.MaxTargetCount : 1, data.AreaRadius));

            return actions.AsReadOnly();
        }
    }
}
