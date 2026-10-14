using System;
using System.Collections.Generic;
using Units.Skills;
using UnityEngine;

namespace Units.FX
{
    [CreateAssetMenu(menuName = "Units/FX/Basic Attack Mapping")]
    public sealed class BasicAttackFXMappingSO : ScriptableObject
    {
        [Serializable]
        private sealed class Mapping
        {
            public BasicAttackFXType Type;
            public List<SkillFXEntry> Entries = new();
        }
        [SerializeField] private List<Mapping> _mappings = new();

        public IReadOnlyList<SkillFXEntry> Get(BasicAttackFXType type)
        {
            if (type == BasicAttackFXType.None) return Array.Empty<SkillFXEntry>();
            foreach (var mapping in _mappings) if (mapping != null && mapping.Type == type) return mapping.Entries;
            return Array.Empty<SkillFXEntry>();
        }

        public SkillFXEntry[] Capture(BasicAttackFXType attack, BasicAttackFXType hit)
        {
            var result = new List<SkillFXEntry>();
            foreach (var entry in Get(attack)) if (entry != null && entry.Hook != SkillFXHook.OnHit) result.Add(entry);
            foreach (var entry in Get(hit)) if (entry != null && entry.Hook == SkillFXHook.OnHit) result.Add(entry);
            return SkillDefinitionCopy.Copy(result).ToArray();
        }
    }
}
