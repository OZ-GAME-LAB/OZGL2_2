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
        private float _skillRange =
            3f;

        [SerializeField]
        private float _skillCooldown =
            5f;


        // ============================================================
        // Target
        // ============================================================

        [Header("Target")]
        [SerializeField]
        private SkillTargetRelation _targetSide =
            SkillTargetRelation.Hostile;

        [SerializeField]
        private SkillTargetPolicy _targetPolicy =
            SkillTargetPolicy.Current;


        // ============================================================
        // Action
        // ============================================================

        [Header("Action")]
        [SerializeField]
        private ActiveSkillActionType _actionType =
            ActiveSkillActionType.Instant;

        [SerializeField]
        private float _castTime =
            0f;


        // ============================================================
        // Execution
        // ============================================================

        [Header("Execution")]
        [SerializeField]
        private ActiveSkillExecutionType _executionType =
            ActiveSkillExecutionType.Direct;


        // ============================================================
        // Target Count
        // ============================================================

        [Header("Target Count")]
        [SerializeField]
        [Tooltip("Projectile을 발사할 최대 목표 수")]
        private int _maxTargetCount =
            1;

        [SerializeField]
        [Tooltip("광역 판정 1회에 피해를 적용할 최대 인원")]
        private int _maxDamageableCount =
            1;


        // ============================================================
        // Area
        // ============================================================

        [Header("Area")]
        [SerializeField]
        private ActiveSkillAreaType _areaType =
            ActiveSkillAreaType.Single;

        [SerializeField]
        private float _areaRadius =
            1f;

        [SerializeField]
        private float _areaAngle =
            90f;


        // ============================================================
        // Projectile
        // ============================================================

        [Header("Projectile")]
        [SerializeField]
        private float _projectileSpeed =
            10f;


        // ============================================================
        // Dash
        // ============================================================

        [Header("Dash")]
        [SerializeField]
        private float _dashDistance =
            3f;

        [SerializeField]
        private float _dashSpeed =
            10f;


        // ============================================================
        // FX
        // ============================================================

        [Header("FX")]
        [SerializeField]
        private ActiveSkillFXType _skillFXType =
            ActiveSkillFXType.None;

        [SerializeField]
        private ActiveSkillFXType _hitFXType =
            ActiveSkillFXType.None;


        // ============================================================
        // Properties
        // ============================================================

        public float SkillRange =>
            _skillRange;

        public float SkillCooldown =>
            _skillCooldown;


        public SkillTargetRelation TargetSide =>
            _targetSide;

        public SkillTargetPolicy TargetPolicy =>
            _targetPolicy;


        public ActiveSkillActionType ActionType =>
            _actionType;

        public float CastTime =>
            _castTime;


        public ActiveSkillExecutionType ExecutionType =>
            _executionType;


        // Projectile을 발사할 최대 목표 수이다.
        public int MaxTargetCount =>
            _maxTargetCount;

        // 광역 판정 1회에 피해를 적용할 최대 인원이다.
        public int MaxDamageableCount =>
            _maxDamageableCount;


        public ActiveSkillAreaType AreaType =>
            _areaType;

        public float AreaRadius =>
            _areaRadius;

        public float AreaAngle =>
            _areaAngle;


        public float ProjectileSpeed =>
            _projectileSpeed;


        public float DashDistance =>
            _dashDistance;

        public float DashSpeed =>
            _dashSpeed;


        public ActiveSkillFXType SkillFXType =>
            _skillFXType;

        public ActiveSkillFXType HitFXType =>
            _hitFXType;


#if UNITY_EDITOR
        private void OnValidate()
        {
            _skillRange =
                Mathf.Max(
                    0f,
                    _skillRange
                );

            _skillCooldown =
                Mathf.Max(
                    0f,
                    _skillCooldown
                );

            _castTime =
                Mathf.Max(
                    0f,
                    _castTime
                );

            _maxTargetCount =
                Mathf.Max(
                    1,
                    _maxTargetCount
                );

            _maxDamageableCount =
                Mathf.Max(
                    1,
                    _maxDamageableCount
                );

            _areaRadius =
                Mathf.Max(
                    0f,
                    _areaRadius
                );

            _areaAngle =
                Mathf.Clamp(
                    _areaAngle,
                    0f,
                    360f
                );

            _projectileSpeed =
                Mathf.Max(
                    0f,
                    _projectileSpeed
                );

            _dashDistance =
                Mathf.Max(
                    0f,
                    _dashDistance
                );

            _dashSpeed =
                Mathf.Max(
                    0f,
                    _dashSpeed
                );
        }
#endif
    }
}