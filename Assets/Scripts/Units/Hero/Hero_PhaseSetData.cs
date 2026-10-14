using System.Collections.Generic;
using UnityEngine;
namespace Units
{
    [CreateAssetMenu(fileName = "Hero_Phases", menuName = "Units/Hero/Phase Set")]
    public sealed class Hero_PhaseSetData : ScriptableObject
    {
        [SerializeField] private string _initialPhaseId;
        [SerializeField] private List<Hero_PhaseData> _phases = new();
        public string InitialPhaseId => _initialPhaseId;
        public IReadOnlyList<Hero_PhaseData> Phases => _phases.AsReadOnly();
        public Hero_PhaseData Find(string id)
        {
            foreach (var phase in _phases) if (phase != null && phase.Id == id) return phase;
            return null;
        }
        public bool Validate(out string error)
        {
            var ids = new HashSet<string>();
            foreach (var phase in _phases)
            {
                if (phase == null || string.IsNullOrWhiteSpace(phase.Id) || !ids.Add(phase.Id))
                { error = "페이즈 ID 누락 또는 중복"; return false; }
                var skills = new HashSet<string>();
                foreach (var policy in phase.SkillPolicies)
                    if (policy == null || string.IsNullOrWhiteSpace(policy.SkillId) || !skills.Add(policy.SkillId))
                    { error = "페이즈 스킬 ID 누락 또는 중복: " + phase.Id; return false; }
            }
            if (!ids.Contains(_initialPhaseId)) { error = "초기 페이즈를 찾을 수 없습니다."; return false; }
            foreach (var phase in _phases)
                foreach (var transition in phase.Transitions)
                    if (transition == null || transition.Destination == phase.Id || !ids.Contains(transition.Destination))
                    { error = "유효하지 않은 전환 목적지: " + phase.Id; return false; }
            error = null; return true;
        }
    }
}
