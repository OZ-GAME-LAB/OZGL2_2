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
        private List<EffectActionData> _actions =
            new();


        // ============================================================
        // Properties
        // ============================================================

        public string EffectId =>
            _effectId;

        public EffectAlignment Alignment =>
            _alignment;

        public EffectDurationType DurationType =>
            _durationType;

        public float Duration =>
            _duration;

        public EffectStackType StackType =>
            _stackType;

        public int MaxStack =>
            _maxStack;

        public IReadOnlyList<EffectActionData> Actions =>
            _actions;


        // ============================================================
        // Validation
        // ============================================================

#if UNITY_EDITOR
        private void OnValidate()
        {
            _duration =
                Mathf.Max(
                    0f,
                    _duration
                );

            _maxStack =
                Mathf.Max(
                    1,
                    _maxStack
                );


            ValidateActions();
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