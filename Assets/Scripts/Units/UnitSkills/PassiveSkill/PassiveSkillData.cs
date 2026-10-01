using System.Collections.Generic;
using UnityEngine;



namespace Units.Skills
{
    [CreateAssetMenu(
        fileName = "PassiveSkillData",
        menuName = "Units/Combat/Passive Skill Data"
    )]
    public class PassiveSkillData : ScriptableObject
    {

        [SerializeField, HideInInspector]
        private int _skillSchemaVersion;

        public int SkillSchemaVersion => _skillSchemaVersion;

#if UNITY_EDITOR
        public void UpgradeSkillSchema()
        {
            if (_skillSchemaVersion >= 1)
                return;

            EnsureEntryIds();

            _skillSchemaVersion = 1;
        }
#endif

        // ============================================================
        // Trigger
        // ============================================================

        [Header("Trigger")]
        [SerializeField]
        private PassiveSkillTriggerType _triggerType = PassiveSkillTriggerType.Initialize;

        [SerializeField]
        [Min(0f)]
        private float _tickInterval = 1f;


        // ============================================================
        // Conditions
        // ============================================================

        [Header("Conditions")]
        [SerializeReference]
        private List<PassiveSkillConditionData> _conditions = new();


        // ============================================================
        // Mode
        // ============================================================

        [Header("Mode")]
        [SerializeField]
        private PassiveSkillEffectMode _effectMode = PassiveSkillEffectMode.WhileCondition;


        // ============================================================
        // Actions
        // ============================================================

        [Header("Actions")]
        [SerializeReference]
        private List<PassiveSkillActionData> _actions = new();


        // ============================================================
        // Properties
        // ============================================================

        public PassiveSkillTriggerType TriggerType => _triggerType;

        [SerializeField, Min(0.05f)]
        private float _conditionCheckInterval = 0.2f;

        public void EnsureEntryIds()
        {
            var entries = new List<SkillEffectEntry>();

            foreach (var action in _actions)
                if (action is PassiveAdditionalAttackActionData extra && extra.Attack != null)
                {
                    entries.AddRange(extra.Attack.BaseEffects);

                    entries.AddRange(extra.Attack.ConditionalEffects);
                }

            var used = new HashSet<int>();

            int next = 1;

            foreach (var entry in entries)
                if (entry != null)
                    next = Mathf.Max(next, entry.EntryId + 1);

            foreach (var entry in entries)
                if (entry != null && (entry.EntryId <= 0 || !used.Add(entry.EntryId)))
                {
                    entry.SetEntryId(next++);

                    used.Add(entry.EntryId);
                }
        }

        public float ConditionCheckInterval => Mathf.Max(0.05f, _conditionCheckInterval);

        public float TickInterval => _tickInterval;

        public IReadOnlyList<PassiveSkillConditionData> Conditions => _conditions;

        public PassiveSkillEffectMode EffectMode => _effectMode;

        public IReadOnlyList<PassiveSkillActionData> Actions => _actions;


        // ============================================================
        // Validation
        // ============================================================

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureEntryIds();

            _tickInterval = Mathf.Max(0.1f, _tickInterval);

            ValidateConditions();

            ValidateActions();
        }

        private void ValidateConditions()
        {
            for (int i = 0; i < _conditions.Count; i++)
            {
                if (_conditions[i] is PassiveUnitCountConditionData unitCountCondition)
                {
                    unitCountCondition.Validate();
                }
            }
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