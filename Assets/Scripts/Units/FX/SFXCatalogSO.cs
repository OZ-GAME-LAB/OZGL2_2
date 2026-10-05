using System;
using System.Collections.Generic;
using UnityEngine;

namespace Units.FX
{
    [CreateAssetMenu(menuName = "Units/FX/SFX Catalog")]
    public sealed class SFXCatalogSO : ScriptableObject
    {
        [SerializeField] private List<SFXDefinition> _entries = new();
        private readonly Dictionary<string, SFXDefinition> _lookup = new(StringComparer.Ordinal);
        private bool _built;

        private void OnEnable() => _built = false;
        private void OnValidate() { _built = false; Rebuild(); }

        public void Rebuild()
        {
            _lookup.Clear();
            foreach (var entry in _entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || entry.Clip == null)
                { Debug.LogWarning("[SFXCatalog] Key 또는 재생 에셋이 비어 있습니다.", this); continue; }
                if (!_lookup.TryAdd(entry.Key, entry))
                    Debug.LogWarning($"[SFXCatalog] 중복 Key: {entry.Key}", this);
            }
            _built = true;
        }

        public bool TryGet(string key, out SFXDefinition definition)
        {
            if (!_built) Rebuild();
            definition = null;
            return !string.IsNullOrWhiteSpace(key) && _lookup.TryGetValue(key, out definition);
        }
    }
}
