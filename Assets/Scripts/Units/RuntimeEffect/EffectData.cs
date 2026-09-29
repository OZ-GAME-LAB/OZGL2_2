using System.Collections.Generic;
using UnityEngine;


namespace Units.Effects
{
    [CreateAssetMenu(
        fileName = "EffectData",
        menuName = "Units/Effects/EffectData"
    )]
    public class EffectData : ScriptableObject
    {

        [SerializeField, HideInInspector]
        private int _skillSchemaVersion;

        public int SkillSchemaVersion => _skillSchemaVersion;

#if UNITY_EDITOR
        public void UpgradeSkillSchema()
        {
            if (_skillSchemaVersion >= 1)
                return;

            if (_categories.Count == 0)
                _categories = new List<string>(Categories);

            _skillSchemaVersion = 1;
        }
#endif

        // ============================================================
        // Identity
        // ============================================================

        [SerializeField]
        private string _effectId;

        [SerializeField]
        private EffectAlignment _alignment;


        // ============================================================
        // Duration
        // ============================================================

        [SerializeField]
        private EffectDurationType _durationType;

        [SerializeField]
        private float _duration;


        // ============================================================
        // Stack
        // ============================================================

        [SerializeField]
        private EffectStackType _stackType;

        [SerializeField]
        private int _maxStack = 1;


        // ============================================================
        // Actions
        // ============================================================

        [SerializeReference]
        private List<EffectActionData> _actions = new();


        // ============================================================
        // Properties
        // ============================================================

        [SerializeField]
        private List<string> _categories = new();

        // 명시 분류가 없는 기존 SO는 기존 Alignment/Action에서 조회용 분류만 유도한다.
        // Category 자체가 상태 효과를 부여하지 않는다.
        public IReadOnlyList<string> Categories
        {
            get
            {
                if (_categories.Count > 0)
                    return _categories.AsReadOnly();

                var inferred = new List<string>
                {
                    "Alignment." + _alignment
                };

                foreach (var action in _actions)
                {
                    string category = action switch
                    {
                        PeriodicDamageEffectActionData => "PeriodicDamage",
                        PeriodicHealEffectActionData => "PeriodicHeal",
                        StatEffectActionData => "StatModifier",
                        StatusEffectActionData status => "Status." + status.StatusType,
                        _ => null
                    };

                    if (category != null && !inferred.Contains(category))
                        inferred.Add(category);
                }

                return inferred.AsReadOnly();
            }
        }

        public bool HasCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return false;

            foreach (var current in Categories)
                if (current == category)
                    return true;

            return false;
        }

        public string EffectId => _effectId;

        public EffectAlignment Alignment => _alignment;

        public EffectDurationType DurationType => _durationType;

        public float Duration => _duration;

        public EffectStackType StackType => _stackType;

        public int MaxStack => _maxStack;

        public IReadOnlyList<EffectActionData> Actions => _actions;


        // ============================================================
        // Validation
        // ============================================================

#if UNITY_EDITOR
        private void OnValidate()
        {
            _duration = Mathf.Max(0f, _duration);

            _maxStack = Mathf.Max(1, _maxStack);

            ValidateActions();
        }

        private void ValidateActions()
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                _actions[i]?.Validate();
            }
        }
#endif
    }
}