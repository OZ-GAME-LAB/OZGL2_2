#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Units.FX;

namespace Units.Editor
{
    [CustomEditor(typeof(VFXCatalogSO))]
    public sealed class VFXCatalogSOEditor : FXCatalogEditor
    {
        protected override string CatalogName => "VFX";
        protected override string AssetField => "_prefab";

        // 배열 추가 시 직전 항목의 설정이 복사되지 않도록 기본값을 명시한다.
        protected override void InitializeEntry(SerializedProperty entry)
        {
            entry.FindPropertyRelative("_prefab").objectReferenceValue = null;
            entry.FindPropertyRelative("_offset").vector2Value = Vector2.zero;
            entry.FindPropertyRelative("_scale").vector2Value = Vector2.one;
            entry.FindPropertyRelative("_referenceRadius").floatValue = 1f;
            entry.FindPropertyRelative("_rotationOffset").floatValue = 0f;
            entry.FindPropertyRelative("_directionMode").intValue = 0;
            entry.FindPropertyRelative("_nativeAngle").floatValue = 0f;
            entry.FindPropertyRelative("_spriteFacesRight").boolValue = true;
            entry.FindPropertyRelative("_maxLifetime").floatValue = 0.3f;
            entry.FindPropertyRelative("_sortingOrderOffset").intValue = 0;
            entry.FindPropertyRelative("_particleStartMode").intValue = 0;
            entry.FindPropertyRelative("_immediateEmitCount").intValue = 1;
            entry.FindPropertyRelative("_particleScalingMode").intValue = 0;
            entry.FindPropertyRelative("_particleSimulationMode").intValue = 0;
        }

        protected override void DrawEntry(SerializedProperty entry)
        {
            Field(entry, "_key"); Field(entry, "_prefab");
            Field(entry, "_referenceRadius", "기준 반경");
            Field(entry, "_offset"); Field(entry, "_scale"); Field(entry, "_rotationOffset");
            Field(entry, "_directionMode");
            var mode = (VFXDirectionMode)entry.FindPropertyRelative("_directionMode").intValue;
            if (mode == VFXDirectionMode.RotateToDirection)
            {
                Field(entry, "_nativeAngle");
                EditorGUILayout.HelpBox("Native Angle은 원본 방향입니다. 오른쪽 0°, 위쪽 90°. 최종 회전 = 공격 방향 − 원본 방향 + Rotation Offset.", MessageType.Info);
            }
            else if (mode == VFXDirectionMode.FlipHorizontal) Field(entry, "_spriteFacesRight");
            Field(entry, "_maxLifetime"); Field(entry, "_sortingOrderOffset");
            Field(entry, "_particleStartMode");
            if ((VFXParticleStartMode)entry.FindPropertyRelative("_particleStartMode").intValue == VFXParticleStartMode.EmitImmediately)
                Field(entry, "_immediateEmitCount");
            Field(entry, "_particleScalingMode"); Field(entry, "_particleSimulationMode");
        }
    }
}
#endif
