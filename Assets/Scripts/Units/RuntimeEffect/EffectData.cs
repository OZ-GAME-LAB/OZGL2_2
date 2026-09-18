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
        private List<EffectActionData> _actions = new();


        // ============================================================
        // Properties
        // ============================================================

        public string EffectId => _effectId;

        public EffectAlignment Alignment => _alignment;

        public EffectDurationType DurationType => _durationType;

        public float Duration => _duration;

        public EffectStackType StackType => _stackType;

        public int MaxStack => _maxStack;

        public IReadOnlyList<EffectActionData> Actions => _actions;
    }
}