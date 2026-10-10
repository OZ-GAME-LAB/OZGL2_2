using UnityEngine;
using System.Collections.Generic;



namespace Units.Skills
{
    // ============================================================
    // Basic Attack Data
    // ============================================================

    [CreateAssetMenu(
        fileName = "BasicAttackData",
        menuName = "Units/Combat/Basic Attack Data"
    )]
    public class BasicAttackData : ScriptableObject
    {
        [SerializeField, Tooltip("피해/투사체 발사를 공격 애니메이션 시작 또는 끝에 실행합니다.")]
        private AnimationImpactTiming _impactTiming;
        public AnimationImpactTiming ImpactTiming => _impactTiming;


        // ============================================================
        // Basic
        // ============================================================

        [Header("Basic")]

        [SerializeField]
        private float _basicAttackRange = 1.5f;

        [SerializeField]
        private float _basicAttackDelay = 1f;

        [SerializeField]
        private DamageType _damageType =
            DamageType.Physical;


        // ============================================================
        // Execution
        // ============================================================

        [Header("Execution")]

        [SerializeField]
        private BasicAttackExecutionType _executionType =
            BasicAttackExecutionType.Direct;


        // ============================================================
        // Target
        // ============================================================

        [Header("Target")]

        [SerializeField]
        private BasicAttackAreaType _areaType =
            BasicAttackAreaType.Single;

        [SerializeField]
        [Tooltip("Projectile을 발사할 최대 목표 수")]
        private int _maxTargetCount = 1;


        [SerializeField]
        [Tooltip("광역 판정 1회에 피해를 적용할 최대 인원")]
        private int _maxDamageableCount = 1;


        // ============================================================
        // Area
        // ============================================================

        [Header("Area")]

        [SerializeField]
        private float _areaRadius = 1f;

        [SerializeField]
        private float _areaAngle = 90f;


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
        // FX
        // ============================================================

        [Header("FX")]
        [SerializeField]
        private List<SkillFXEntry> _fxEntries = new();

        public IReadOnlyList<SkillFXEntry> FXEntries => _fxEntries;

        // 기존 에셋 이전용 값. 실행 경로는 FXEntries만 사용한다.
        [SerializeField, HideInInspector]
        private BasicAttackFXType _attackFXType =
            BasicAttackFXType.None;

        // 기존 에셋 이전용 값. 실행 경로는 FXEntries만 사용한다.
        [SerializeField, HideInInspector]
        private BasicAttackFXType _hitFXType =
            BasicAttackFXType.None;


        // ============================================================
        // Properties
        // ============================================================

        public float BasicAttackRange =>
            _basicAttackRange;

        public float BasicAttackDelay =>
            _basicAttackDelay;

        public DamageType DamageType =>
            _damageType;

        public BasicAttackExecutionType ExecutionType =>
            _executionType;

        public BasicAttackAreaType AreaType =>
            _areaType;

        // 광역 판정 1회에 피해를 적용할 최대 인원이다.
        public int MaxDamageableCount =>
            _maxDamageableCount;


        // Projectile을 발사할 최대 목표 수이다.
        public int MaxTargetCount =>
            _maxTargetCount;

        public float AreaRadius =>
            _areaRadius;

        public float AreaAngle =>
            _areaAngle;

        public Projectile_Controller ProjectilePrefab => _projectilePrefab;

        public float ProjectileSpeed =>
            _projectileSpeed;

        public BasicAttackFXType AttackFXType =>
            _attackFXType;

        public BasicAttackFXType HitFXType =>
            _hitFXType;



        // ============================================================
        // Validation
        // ============================================================

#if UNITY_EDITOR
        private void OnValidate()
        {
            _maxDamageableCount =
                Mathf.Max(1, _maxDamageableCount);


            _basicAttackRange =
                Mathf.Max(0f, _basicAttackRange);

            _basicAttackDelay =
                Mathf.Max(0.01f, _basicAttackDelay);

            _maxTargetCount =
                Mathf.Max(1, _maxTargetCount);

            _areaRadius =
                Mathf.Max(0f, _areaRadius);

            _areaAngle =
                Mathf.Clamp(
                    _areaAngle,
                    0f,
                    360f
                );


            _projectileSpeed =
                Mathf.Max(0f, _projectileSpeed);
        }
#endif
    }
}