using System;
using System.Collections.Generic;
using UnityEngine;



namespace Units.Skills
{
    [Serializable]
    public class PassiveEffectActionData
        : PassiveSkillActionData
    {
        // ============================================================
        // Target
        // ============================================================

        [Header("Target")]
        [SerializeField]
        private PassiveSkillTargetType _targetType =
            PassiveSkillTargetType.Self;

        [SerializeField]
        private SkillTargetRelation _targetRelation =
            SkillTargetRelation.Self;


        // ============================================================
        // Target Count
        // ============================================================

        [Header("Target Count")]
        [SerializeField]
        [Tooltip("패시브 효과를 적용할 최대 대상 수")]
        private int _maxEffectTargetCount =
            1;


        // ============================================================
        // Area
        // ============================================================

        [Header("Area")]
        [SerializeField]
        private PassiveSkillAreaType _areaType =
            PassiveSkillAreaType.Single;

        [SerializeField]
        private float _areaRadius =
            1f;

        [SerializeField]
        private float _areaAngle =
            90f;


        // ============================================================
        // Effects
        // ============================================================

        [Header("Effects")]
        [SerializeReference]
        private List<SkillEffectData> _effects =
            new();


        // ============================================================
        // Properties
        // ============================================================

        public PassiveSkillTargetType TargetType =>
            _targetType;

        public SkillTargetRelation TargetRelation =>
            _targetRelation;

        public int MaxEffectTargetCount =>
            _maxEffectTargetCount;


        public PassiveSkillAreaType AreaType =>
            _areaType;

        public float AreaRadius =>
            _areaRadius;

        public float AreaAngle =>
            _areaAngle;


        public IReadOnlyList<SkillEffectData> Effects =>
            _effects;


        // ============================================================
        // Validation
        // ============================================================

#if UNITY_EDITOR
        public override void Validate()
        {
            _maxEffectTargetCount =
                Mathf.Max(
                    1,
                    _maxEffectTargetCount
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
        }
#endif
    }
}