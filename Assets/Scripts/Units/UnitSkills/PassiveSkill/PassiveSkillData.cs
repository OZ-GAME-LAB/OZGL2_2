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
        // ============================================================
        // Trigger
        // ============================================================

        [Header("Trigger")]
        [SerializeField]
        private PassiveSkillTriggerType _triggerType =
            PassiveSkillTriggerType.Initialize;


        [SerializeField]
        [Min(0f)]
        private float _tickInterval =
            1f;


        // ============================================================
        // Conditions
        // ============================================================

        [Header("Conditions")]
        [SerializeReference]
        private List<PassiveSkillConditionData> _conditions =
            new();


        // ============================================================
        // Mode
        // ============================================================

        [Header("Mode")]
        [SerializeField]
        private PassiveSkillEffectMode _effectMode =
            PassiveSkillEffectMode.WhileCondition;


        // ============================================================
        // Actions
        // ============================================================

        [Header("Actions")]
        [SerializeReference]
        private List<PassiveSkillActionData> _actions =
            new();


        // ============================================================
        // Properties
        // ============================================================

        public PassiveSkillTriggerType TriggerType =>
            _triggerType;

        public float TickInterval =>
            _tickInterval;

        public IReadOnlyList<PassiveSkillConditionData> Conditions =>
            _conditions;

        public PassiveSkillEffectMode EffectMode =>
            _effectMode;

        public IReadOnlyList<PassiveSkillActionData> Actions =>
            _actions;


        // ============================================================
        // Validation
        // ============================================================

#if UNITY_EDITOR
        private void OnValidate()
        {
            _tickInterval =
                Mathf.Max(
                0.1f,
                _tickInterval
                );

            ValidateConditions();
            ValidateActions();
        }


        private void ValidateConditions()
        {
            for (int i = 0;
                 i < _conditions.Count;
                 i++)
            {
                if (_conditions[i]
                    is PassiveUnitCountConditionData unitCountCondition)
                {
                    unitCountCondition.Validate();
                }
            }
        }

        private void ValidateActions()
        {
            for (int i = 0;
                 i < _actions.Count;
                 i++)
            {
                _actions[i]?.Validate();
            }
        }
#endif
    }
}