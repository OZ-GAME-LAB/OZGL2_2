using Units.Skills;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ConsumableItemData))]
public class ConsumableItemDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        EditorGUILayout.PropertyField(serializedObject.FindProperty("_id"), new GUIContent("ID"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_displayName"), new GUIContent("이름"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_description"), new GUIContent("설명"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_icon"), new GUIContent("아이콘"));

        EditorGUILayout.PropertyField(serializedObject.FindProperty("_targetTeam"), new GUIContent("대상 팀"));
        SerializedProperty mode = serializedObject.FindProperty("_targetMode");
        EditorGUILayout.PropertyField(mode, new GUIContent("적용 방식"));
        if ((ConsumableTargetMode)mode.intValue == ConsumableTargetMode.Area)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_radius"), new GUIContent("범위 반경"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("사용 효과", EditorStyles.boldLabel);
        SerializedProperty effects = serializedObject.FindProperty("_effects");
        if (effects.arraySize == 0)
            EditorGUILayout.HelpBox("효과 추가 버튼으로 아이템 사용 효과를 설정하세요.", MessageType.Info);

        int removeIndex = -1;
        for (int i = 0; i < effects.arraySize; i++)
        {
            SerializedProperty effect = effects.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    effect.isExpanded = EditorGUILayout.Foldout(effect.isExpanded,
                        $"{i + 1}. {GetEffectName(effect.managedReferenceValue)}", true);
                    if (GUILayout.Button("삭제", GUILayout.Width(48)))
                        removeIndex = i;
                }

                if (effect.isExpanded && effect.managedReferenceValue != null)
                {
                    // 각 SkillEffectData의 직렬화 필드를 표시하며 S.O 참조도 편집합니다.
                    SerializedProperty child = effect.Copy();
                    SerializedProperty end = effect.GetEndProperty();
                    if (child.NextVisible(true))
                    {
                        while (!SerializedProperty.EqualContents(child, end))
                        {
                            EditorGUILayout.PropertyField(child, true);
                            if (!child.NextVisible(false)) break;
                        }
                    }
                }
            }
        }

        if (removeIndex >= 0)
            effects.DeleteArrayElementAtIndex(removeIndex);

        if (GUILayout.Button("효과 추가"))
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("피해"), false, () => AddEffect(new SkillDamageEffectData()));
            menu.AddItem(new GUIContent("회복"), false, () => AddEffect(new SkillHealEffectData()));
            menu.AddItem(new GUIContent("보호막"), false, () => AddEffect(new SkillShieldEffectData()));
            menu.AddItem(new GUIContent("지속 효과 (EffectData)"), false, () => AddEffect(new SkillRuntimeEffectData()));
            menu.ShowAsContext();
        }

        // SerializedObject를 통해 변경 사항 저장 및 Undo/Redo를 지원합니다.
        serializedObject.ApplyModifiedProperties();
    }

    private void AddEffect(SkillEffectData effect)
    {
        if (target == null) return;

        serializedObject.Update();
        SerializedProperty effects = serializedObject.FindProperty("_effects");
        int index = effects.arraySize;
        effects.arraySize++;
        SerializedProperty added = effects.GetArrayElementAtIndex(index);
        added.managedReferenceValue = effect;
        added.isExpanded = true;
        serializedObject.ApplyModifiedProperties();
        Repaint();
    }

    private static string GetEffectName(object effect)
    {
        return effect switch
        {
            SkillDamageEffectData => "피해",
            SkillHealEffectData => "회복",
            SkillShieldEffectData => "보호막",
            SkillRuntimeEffectData => "지속 효과",
            null => "비어 있는 효과",
            _ => effect.GetType().Name
        };
    }
}
