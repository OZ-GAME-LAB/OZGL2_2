using UnityEngine;



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


        // ============================================================
        // FX
        // ============================================================

        [Header("FX")]

        [SerializeField]
        private BasicAttackFXType _attackFXType =
            BasicAttackFXType.None;

        [SerializeField]
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