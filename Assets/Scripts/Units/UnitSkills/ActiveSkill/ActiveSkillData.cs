using System.Collections.Generic;
using UnityEngine;

namespace Units.Skills
{
    [CreateAssetMenu(
        fileName = "ActiveSkillData",
        menuName = "Units/Combat/Active Skill Data"
    )]
    public class ActiveSkillData : ScriptableObject
    {
        // ============================================================
        // Basic
        // ============================================================

        [Header("Basic")]
        [SerializeField]
        private float _skillRange = 3f;

        [SerializeField]
        private float _skillCooldown = 5f;


        // ============================================================
        // Target
        // ============================================================

        [Header("Target")]
        [SerializeField]
        private SkillTargetRelation _targetSide = SkillTargetRelation.Hostile;

        [SerializeField]
        private SkillTargetPolicy _targetPolicy = SkillTargetPolicy.Current;


        // ============================================================
        // Action
        // ============================================================

        [Header("Action")]
        [SerializeField]
        private ActiveSkillActionType _actionType = ActiveSkillActionType.Instant;

        [SerializeField]
        private float _castTime = 0f;


        // ============================================================
        // Delivery
        // ============================================================

        [Header("Delivery")]
        [SerializeField]
        private ActiveSkillDeliveryType _deliveryType = ActiveSkillDeliveryType.Direct;


        // ============================================================
        // Target Count
        // ============================================================

        [Header("Target Count")]
        [SerializeField]
        [Tooltip("Projectile을 발사할 최대 목표 수")]
        private int _maxTargetCount = 1;

        [SerializeField]
        [Tooltip("광역 판정 1회에 Effect를 적용할 최대 대상 수")]
        private int _maxEffectTargetCount = 1;


        // ============================================================
        // Area
        // ============================================================

        [Header("Area")]
        [SerializeField]
        private ActiveSkillAreaType _areaType = ActiveSkillAreaType.Single;

        [SerializeField]
        private float _areaRadius = 1f;

        [SerializeField]
        private float _areaAngle = 90f;


        // ============================================================
        // Effects
        // ============================================================

        [Header("Effects")]
        [SerializeReference]
        private List<SkillEffectData> _effects = new();


        // ============================================================
        // Projectile
        // ============================================================

        [Header("Projectile")]
        [SerializeField]
        private float _projectileSpeed = 10f;

        // 비워두면 ProjectileManager의 공용 투사체를 사용한다.
        [SerializeField, Tooltip("이 공격 전용 투사체 프리팹. 루트의 Projectile_Controller를 지정합니다.")]
        private Projectile_Controller _projectilePrefab;


        // ============================================================
        // Dash
        // ============================================================

        [Header("Dash")]
        [SerializeField]
        private float _dashDistance = 3f;

        [SerializeField]
        private float _dashSpeed = 10f;


        // ============================================================
        // FX
        // ============================================================

        [Header("FX")]
        [SerializeField]
        private ActiveSkillFXType _skillFXType = ActiveSkillFXType.None;

        [SerializeField]
        private ActiveSkillFXType _hitFXType = ActiveSkillFXType.None;


        // ============================================================
        // Properties
        // ============================================================

        [SerializeReference]
        private List<SkillActionData> _actions = new();

        public IReadOnlyList<SkillActionData> Actions => _actions;

        [SerializeField, HideInInspector]
        private int _skillSchemaVersion;

        [SerializeField, HideInInspector]
        private bool _usesLegacyTargetSelection;

        public int SkillSchemaVersion => _skillSchemaVersion;

        public bool UsesLegacyTargetSelection => _actions.Count == 0 || _usesLegacyTargetSelection;

#if UNITY_EDITOR
        public void UpgradeSkillSchema()
        {
            if (_skillSchemaVersion >= 1)
                return;

            if (_actions.Count == 0)
            {
                _actions = new List<SkillActionData>(SkillActionPlan.Create(this));

                _usesLegacyTargetSelection = true;
            }

            EnsureEntryIds();

            _skillSchemaVersion = 1;
        }
#endif

        // 엔트리 이동은 ID를 유지한다. 같은 정의 내부의 복제 엔트리만 새 ID를 얻는다.
        public void EnsureEntryIds()
        {
            var used = new HashSet<int>();

            int next = 1;

            foreach (var action in _actions)
            {
                if (action == null)
                    continue;

                foreach (var entry in action.BaseEffects)
                    if (entry != null)
                        next = Mathf.Max(next, entry.EntryId + 1);

                foreach (var entry in action.ConditionalEffects)
                    if (entry != null)
                        next = Mathf.Max(next, entry.EntryId + 1);
            }

            foreach (var action in _actions)
            {
                if (action == null)
                    continue;

                foreach (var entry in action.BaseEffects)
                    if (entry != null && (entry.EntryId <= 0 || !used.Add(entry.EntryId)))
                    {
                        entry.SetEntryId(next++);

                        used.Add(entry.EntryId);
                    }

                foreach (var entry in action.ConditionalEffects)
                    if (entry != null && (entry.EntryId <= 0 || !used.Add(entry.EntryId)))
                    {
                        entry.SetEntryId(next++);

                        used.Add(entry.EntryId);
                    }
            }
        }

        public float SkillRange => _skillRange;

        public float SkillCooldown => _skillCooldown;

        public SkillTargetRelation TargetSide => _targetSide;

        public SkillTargetPolicy TargetPolicy => _targetPolicy;

        public ActiveSkillActionType ActionType => _actionType;

        public float CastTime => _castTime;

        public ActiveSkillDeliveryType DeliveryType => _deliveryType;


        // Projectile을 발사할 최대 목표 수이다.
        public int MaxTargetCount => _maxTargetCount;

        // 광역 판정 1회에 Effect를 적용할 최대 대상 수이다.
        public int MaxEffectTargetCount => _maxEffectTargetCount;

        public ActiveSkillAreaType AreaType => _areaType;

        public float AreaRadius => _areaRadius;

        public float AreaAngle => _areaAngle;

        public IReadOnlyList<SkillEffectData> Effects => _effects;

        public Projectile_Controller ProjectilePrefab => _projectilePrefab;

        public float ProjectileSpeed => _projectileSpeed;

        public float DashDistance => _dashDistance;

        public float DashSpeed => _dashSpeed;

        public ActiveSkillFXType SkillFXType => _skillFXType;

        public ActiveSkillFXType HitFXType => _hitFXType;


#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureEntryIds();

            _skillRange = Mathf.Max(0f, _skillRange);

            _skillCooldown = Mathf.Max(0f, _skillCooldown);

            _castTime = Mathf.Max(0f, _castTime);

            _maxTargetCount = Mathf.Max(1, _maxTargetCount);

            _maxEffectTargetCount = Mathf.Max(1, _maxEffectTargetCount);

            _areaRadius = Mathf.Max(0f, _areaRadius);

            _areaAngle = Mathf.Clamp(
                _areaAngle,
                0f,
                360f
            );

            _projectileSpeed = Mathf.Max(0f, _projectileSpeed);

            _dashDistance = Mathf.Max(0f, _dashDistance);

            _dashSpeed = Mathf.Max(0f, _dashSpeed);
        }
#endif
    }
}