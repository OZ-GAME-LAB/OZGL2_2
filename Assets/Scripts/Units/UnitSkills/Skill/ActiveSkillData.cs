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
        // Attack
        // ============================================================

        [Header("Attack")]
        [SerializeField]
        private ActiveSkillAttackType _attackType =
            ActiveSkillAttackType.Direct;

        [SerializeField]
        private int _maxTargetCount =
            1;


        // ============================================================
        // Area
        // ============================================================

        [Header("Area")]
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


        public ActiveSkillActionType ActionType =>
            _actionType;

        public float CastTime =>
            _castTime;


        public ActiveSkillAttackType AttackType =>
            _attackType;

        public int MaxTargetCount =>
            _maxTargetCount;


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