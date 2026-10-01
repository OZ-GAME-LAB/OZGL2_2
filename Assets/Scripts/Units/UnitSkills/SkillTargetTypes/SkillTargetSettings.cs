using System;
using System.Collections.Generic;
using UnityEngine;

// 대상 출처·관계·수치 필터·우선순위를 직렬화 가능한 설정으로 묶는다.
namespace Units.Skills
{
    public enum SkillTargetSource
    {
        None,
        Self,
        InitialTarget,
        CurrentTarget,
        PreviousTarget,
        TriggerTarget,
        Search
    }
    public enum SkillTargetMetric
    {
        Distance,
        CurrentHp,
        HpRatio,
        MissingHpRatio,
        Shield,
        Defense
    }
    public enum SkillTargetComparison
    {
        Less,
        LessOrEqual,
        Greater,
        GreaterOrEqual,
        Equal
    }
    public enum SkillAreaShape
    {
        Single,
        Circle,
        Cone
    }

    [Serializable]
    public sealed class SkillTargetNumericFilter
    {

        // ============================================================
        // Data / Runtime State
        // ============================================================

        [SerializeField]
        private SkillTargetMetric _metric;

        [SerializeField]
        private SkillTargetComparison _comparison;

        [SerializeField]
        private float _value;

        // ============================================================
        // Properties
        // ============================================================

        public SkillTargetMetric Metric => _metric;

        public SkillTargetComparison Comparison => _comparison;

        public float Value => _value;

        // ============================================================
        // Constructor
        // ============================================================

        public SkillTargetNumericFilter(
            SkillTargetMetric metric,
            SkillTargetComparison comparison,
            float value)
        {
            _metric = metric;

            _comparison = comparison;

            _value = value;
        }
    }

    [Serializable]
    public sealed class SkillTargetPriority
    {

        // ============================================================
        // Data / Runtime State
        // ============================================================

        [SerializeField]
        private SkillTargetPolicy _policy;

        // ============================================================
        // Properties
        // ============================================================

        public SkillTargetPolicy Policy => _policy;

        // ============================================================
        // Constructor
        // ============================================================

        public SkillTargetPriority(SkillTargetPolicy policy)
        {
            _policy = policy;
        }
    }

    [Serializable]
    public sealed class SkillAreaSettings
    {

        // ============================================================
        // Data / Runtime State
        // ============================================================

        [SerializeField]
        private SkillAreaShape _shape;

        [SerializeField, Min(0f)]
        private float _radius = 1f;

        [SerializeField, Range(0f, 360f)]
        private float _angle = 90f;

        // ============================================================
        // Properties
        // ============================================================

        public SkillAreaShape Shape => _shape;

        public float Radius => _radius;

        public float Angle => _angle;

        // ============================================================
        // Constructor
        // ============================================================

        public SkillAreaSettings(
            SkillAreaShape shape,
            float radius,
            float angle)
        {
            _shape = shape;

            _radius = Mathf.Max(0f, radius);

            _angle = Mathf.Clamp(
                angle,
                0f,
                360f
            );
        }
    }

    // 기존 SO 필드를 대체하지 않는다. G2의 Action 및 호환 어댑터에서 사용한다.
    [Serializable]
    public sealed class SkillTargetSettings
    {

        // ============================================================
        // Data / Runtime State
        // ============================================================

        [SerializeField]
        private SkillTargetSource _source = SkillTargetSource.Search;

        [SerializeField]
        private SkillTargetRelation _relation = SkillTargetRelation.Hostile;

        [SerializeField]
        private bool _includeSelf;

        [SerializeField, Min(0f)]
        private float _range = 3f;

        [SerializeField, Min(1)]
        private int _maxTargetCount = 1;

        [SerializeField, Min(0f)]
        private float _clusterRadius = 1f;

        [SerializeField]
        private List<SkillTargetNumericFilter> _filters = new();

        [SerializeField]
        private List<SkillTargetPriority> _priorities = new();

        // ============================================================
        // Properties
        // ============================================================

        public SkillTargetSource Source => _source;

        public SkillTargetRelation Relation => _relation;

        public bool IncludeSelf => _includeSelf;

        public float Range => _range;

        public int MaxTargetCount => _maxTargetCount;

        public float ClusterRadius => _clusterRadius;

        public IReadOnlyList<SkillTargetNumericFilter> Filters => _filters;

        public IReadOnlyList<SkillTargetPriority> Priorities => _priorities;

        // ============================================================
        // Execution
        // ============================================================

        public SkillTargetSettings WithSource(SkillTargetSource source) => new(
            source,
            _relation,
            _includeSelf,
            _range,
            _maxTargetCount,
            _clusterRadius,
            _filters,
            _priorities
        );

        // ============================================================
        // Constructor
        // ============================================================

        public SkillTargetSettings(
            SkillTargetSource source,
            SkillTargetRelation relation,
            bool includeSelf,
            float range,
            int maxTargetCount,
            float clusterRadius,
            IEnumerable<SkillTargetNumericFilter> filters = null,
            IEnumerable<SkillTargetPriority> priorities = null)
        {
            _source = source;

            _relation = relation;

            _includeSelf = includeSelf;

            _range = Mathf.Max(0f, range);

            _maxTargetCount = Mathf.Max(1, maxTargetCount);

            _clusterRadius = Mathf.Max(0f, clusterRadius);

            _filters = filters == null ? new() : new(filters);

            _priorities = priorities == null ? new() : new(priorities);
        }
    }
}
