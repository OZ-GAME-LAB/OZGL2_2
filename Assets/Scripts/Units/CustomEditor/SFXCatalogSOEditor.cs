#if UNITY_EDITOR
using UnityEditor;
using Units.FX;

namespace Units.Editor
{
    [CustomEditor(typeof(SFXCatalogSO))]
    public sealed class SFXCatalogSOEditor : FXCatalogEditor
    {
        protected override string CatalogName => "SFX";
        protected override string AssetField => "_clip";

        // 새 항목은 SFXDefinition의 기본 재생 설정으로 시작한다.
        protected override void InitializeEntry(SerializedProperty entry)
        {
            entry.FindPropertyRelative("_clip").objectReferenceValue = null;
            entry.FindPropertyRelative("_mixerGroup").objectReferenceValue = null;
            entry.FindPropertyRelative("_volume").floatValue = 1f;
            entry.FindPropertyRelative("_pitch").floatValue = 1f;
            entry.FindPropertyRelative("_loop").boolValue = false;
            entry.FindPropertyRelative("_spatialBlend").floatValue = 0f;
            entry.FindPropertyRelative("_minDistance").floatValue = 1f;
            entry.FindPropertyRelative("_maxDistance").floatValue = 30f;
            entry.FindPropertyRelative("_fadeOut").floatValue = 0.05f;
            entry.FindPropertyRelative("_maxLifetime").floatValue = 0.3f;
            entry.FindPropertyRelative("_maxConcurrent").intValue = 8;
            entry.FindPropertyRelative("_minInterval").floatValue = 0.03f;
        }

        protected override void DrawEntry(SerializedProperty entry)
        {
            Section("식별 / 오디오"); Field(entry, "_key"); Field(entry, "_clip");
            Section("재생"); Field(entry, "_volume"); Field(entry, "_pitch"); Field(entry, "_loop"); Field(entry, "_mixerGroup");
            Section("공간 음향"); Field(entry, "_spatialBlend");
            if (entry.FindPropertyRelative("_spatialBlend").floatValue > 0f)
            {
                Field(entry, "_minDistance"); Field(entry, "_maxDistance");
                if (entry.FindPropertyRelative("_maxDistance").floatValue < entry.FindPropertyRelative("_minDistance").floatValue)
                    EditorGUILayout.HelpBox("Max Distance는 Min Distance 이상으로 설정하세요.", MessageType.Warning);
            }
            Section("종료 / 재생 제한"); Field(entry, "_fadeOut"); Field(entry, "_maxLifetime");
            Field(entry, "_maxConcurrent"); Field(entry, "_minInterval");
        }
    }
}
#endif
