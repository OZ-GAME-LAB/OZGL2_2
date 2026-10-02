using System;
using System.Collections.Generic;
using UnityEngine;
namespace Units
{
    public enum Hero_PhaseTransitionMode { AfterExecution, InterruptExecution }
    [Serializable]
    public sealed class Hero_PhaseTransitionData
    {
        [SerializeField] private string _destination;
        [SerializeField] private int _priority;
        [SerializeField] private Hero_PhaseTransitionMode _mode;
        [SerializeReference] private List<Hero_PhaseConditionData> _conditions = new();
        public string Destination => _destination;
        public int Priority => _priority;
        public Hero_PhaseTransitionMode Mode => _mode;
        public bool Matches(Unit_Core core, float elapsed)
        {
            // 빈 조건은 전환하지 않는다. 무조건 전환에는 Always 조건을 지정한다.
            if (_conditions.Count == 0) return false;
            foreach (var condition in _conditions)
                if (condition == null || !condition.Evaluate(core, elapsed)) return false;
            return true;
        }
    }
}
