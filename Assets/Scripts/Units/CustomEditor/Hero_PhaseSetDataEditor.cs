#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
namespace Units.Editor
{
    [CustomEditor(typeof(Hero_PhaseSetData))]
    public sealed class Hero_PhaseSetDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_initialPhaseId"));
            SkillAuthoringGUI.Draw(serializedObject.FindProperty("_phases"));
            serializedObject.ApplyModifiedProperties();
            if (!((Hero_PhaseSetData)target).Validate(out var error))
                EditorGUILayout.HelpBox(error, MessageType.Warning);
            EditorGUILayout.HelpBox("전환 조건은 모두 만족해야 합니다. 우선순위가 같으면 목록 순서를 따릅니다. 회귀는 돌아갈 페이즈로 전환을 명시하세요.", MessageType.Info);
        }
    }
}
#endif
