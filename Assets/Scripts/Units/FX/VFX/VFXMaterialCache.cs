using System.Collections.Generic;
using UnityEngine;

namespace Units.FX
{
    // 풀 전체에서 원본별 호환 머티리얼을 공유한다. 개별 연출 값은 공유 머티리얼에 쓰지 않는다.
    internal sealed class VFXMaterialCache
    {
        // ============================================================
        // References / Runtime State
        // ============================================================

        private readonly IReadOnlyList<VFXShaderReplacement> _replacements;
        private readonly Dictionary<Material, Material> _copies = new();

        internal VFXMaterialCache(IReadOnlyList<VFXShaderReplacement> replacements)
        {
            _replacements = replacements;
        }

        // ============================================================
        // Material Conversion
        // ============================================================

        internal void Apply(GameObject instance)
        {
            if (_replacements == null || _replacements.Count == 0)
                return;

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var original = materials[i];
                    if (original == null)
                        continue;

                    if (!_copies.TryGetValue(original, out var shared))
                    {
                        foreach (var replacement in _replacements)
                        {
                            if (replacement == null || !replacement.Matches(original))
                                continue;

                            shared = replacement.CreateMaterial(original);
                            _copies.Add(original, shared);
                            break;
                        }
                    }

                    if (shared == null)
                        continue;

                    materials[i] = shared;
                    changed = true;
                }

                if (changed)
                    renderer.sharedMaterials = materials;
            }
        }

        // ============================================================
        // Cleanup
        // ============================================================

        internal void Clear()
        {
            // 활성·보관 인스턴스 정리 후 풀에서 생성한 복제본만 해제한다.
            foreach (var material in _copies.Values)
                if (material != null)
                    Object.Destroy(material);

            _copies.Clear();
        }
    }
}
