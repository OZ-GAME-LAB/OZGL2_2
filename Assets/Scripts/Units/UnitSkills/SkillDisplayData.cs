using System;
using UnityEngine;

namespace Units.Skills
{
    [Serializable]
    public sealed class SkillDisplayData
    {
        [SerializeField] private string _name;
        [SerializeField] private Sprite _icon;
        [TextArea(3, 10)] [SerializeField] private string _description;

        public Sprite Icon => _icon;
        public string Description => string.IsNullOrWhiteSpace(_description) ? "설명 미등록" : _description;

        public string GetName(string fallback)
        {
            return string.IsNullOrWhiteSpace(_name) ? fallback : _name;
        }
    }
}
