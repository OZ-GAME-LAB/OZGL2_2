using System;
using UnityEngine;

namespace Units.FX
{
    // 외부 셰이더의 에셋 참조 없이 이름으로 식별하고, 호환 셰이더는 프로젝트 에셋으로 참조한다.
    [Serializable]
    public sealed class VFXShaderReplacement
    {
        // ============================================================
        // Settings
        // ============================================================

        [SerializeField, Tooltip("교체할 원본 셰이더의 전체 이름. 예: Shader Graphs/M_Flipbook")]
        private string _sourceShaderName;

        [SerializeField]
        private Shader _replacementShader;

        // ============================================================
        // Material Conversion
        // ============================================================

        internal bool Matches(Material material)
        {
            return material != null && material.shader != null && _replacementShader != null
                && !string.IsNullOrWhiteSpace(_sourceShaderName)
                && string.Equals(material.shader.name, _sourceShaderName, StringComparison.Ordinal);
        }

        internal Material CreateMaterial(Material original)
        {
            // 동일한 프로퍼티 이름을 사용하는 호환 셰이더에 텍스처·HDR 색상·UV 설정을 보존한다.
            var copy = new Material(original)
            {
                name = original.name + " (VFX Runtime)",
                hideFlags = HideFlags.DontSave,
                shader = _replacementShader
            };
            // 원본 Lit의 키워드·큐·패스 비활성화가 새 셰이더의 투명 렌더링을 덮지 않게 한다.
            copy.shaderKeywords = Array.Empty<string>();
            copy.renderQueue = _replacementShader.renderQueue;
            copy.SetOverrideTag("RenderType", "Transparent");
            copy.SetShaderPassEnabled("Universal2D", true);
            copy.SetShaderPassEnabled("SRPDefaultUnlit", true);
            return copy;
        }
    }
}
