using System;
using System.Collections.Generic;
using UnityEngine;

namespace Units.Skills
{
    [Serializable]
    public sealed class SkillUsePolicyData
    {
        [SerializeField] private bool _enabled = true;
        [SerializeField] private int _priority;
        [SerializeField, Min(0)] private float _utilityWeight = 1;
        [SerializeField, Min(0)] private float _minimumUtility = 0.001f;
        [SerializeField, Min(0)] private float _baseUtility;
        [SerializeField, Min(0)] private float _runtimeEffectUtility = 10;
        [SerializeField] private int _urgentPriority = 100;
        [SerializeReference] private List<SkillConditionData> _conditions = new();
        [SerializeReference] private List<SkillConditionData> _urgentConditions = new();
        public bool Enabled => _enabled;
        public float MinimumUtility => Mathf.Max(0, _minimumUtility);
        public float BaseUtility => Mathf.Max(0, _baseUtility);
        public float UtilityWeight => Mathf.Max(0, _utilityWeight);
        public float RuntimeEffectUtility => Mathf.Max(0, _runtimeEffectUtility);
        public bool Allows(SkillConditionContext context) => _enabled && SkillConditionData.All(_conditions, context);
        public int Priority(SkillConditionContext context) => _urgentConditions.Count > 0 &&
            SkillConditionData.All(_urgentConditions, context) ? Math.Max(_priority, _urgentPriority) : _priority;
    }
}
