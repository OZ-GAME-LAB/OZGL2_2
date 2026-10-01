#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Units.Skills;
using UnityEditor;
using UnityEngine;

namespace Units.Editor
{
    // 스킬 Inspector의 접힘 상태·요약 카드·목록 편집을 공유한다. UI 상태는 에셋에 저장하지 않는다.
    internal static class SkillInspectorUI
    {
        // ============================================================
        // View State / Labels
        // ============================================================

        private static string Key(SerializedObject owner, string path)
        {
            string assetPath = AssetDatabase.GetAssetPath(owner.targetObject);

            return "SkillInspector:" + (string.IsNullOrEmpty(assetPath)
                ? owner.targetObject.GetInstanceID().ToString() : assetPath) + ":" + path;
        }

        public static bool Foldout(SerializedProperty property, string title = null, bool initiallyOpen = false)
        {
            return Foldout(property.serializedObject, property.propertyPath,
                title ?? Label(property), initiallyOpen);
        }

        public static bool Foldout(SerializedObject owner, string path, string title, bool initiallyOpen = false)
        {
            string key = Key(owner, path);
            bool open = EditorGUILayout.Foldout(SessionState.GetBool(key, initiallyOpen), title, true);

            SessionState.SetBool(key, open);

            return open;
        }

        public static int Tabs(SerializedProperty property, params string[] titles)
        {
            string key = Key(property.serializedObject, property.propertyPath + ":tab");
            int selected = Mathf.Clamp(SessionState.GetInt(key, 0), 0, titles.Length - 1);

            selected = GUILayout.Toolbar(selected, titles);

            SessionState.SetInt(key, selected);

            EditorGUILayout.Space(6);

            return selected;
        }

        public static string Label(SerializedProperty property) => property.name switch
        {
            "_actions" => "실행 순서",
            "_baseEffects" => "기본 효과",
            "_conditionalEffects" => "조건부 효과",
            "_conditions" => "조건",
            "_effects" => "적용 효과",
            "_fxEntries" => "FX 요청",
            "_target" => "대상 설정",
            "_snapshotAreaTarget" => "스냅샷 범위 대상",
            "_filters" => "수치 필터",
            "_priorities" => "선택 우선순위",
            "_categories" => "분류 태그",
            _ => property.displayName
        };

        public static string TypeLabel(Type type)
        {
            if (type == null)
                return "타입 선택 필요";

            return type.Name switch
            {
                "SkillCastActionData" => "시전 · Cast",
                "SkillDashActionData" => "돌진 · Dash",
                "SkillAttackActionData" => "공격 · Attack",
                "SkillDamageEffectData" => "피해",
                "SkillHealEffectData" => "회복",
                "SkillShieldEffectData" => "보호막",
                "SkillRuntimeEffectData" => "상태 효과 부여",
                "PassiveAdditionalAttackActionData" => "추가 공격",
                "PassiveDistanceConditionData" => "거리 이상",
                "PassiveEffectActionData" => "효과 적용",
                _ => ObjectNames.NicifyVariableName(type.Name
                    .Replace("ConditionData", " Condition").Replace("ActionData", "")
                    .Replace("EffectData", " Effect"))
            };
        }

        public static string Summary(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference &&
                property.propertyType != SerializedPropertyType.Generic)
                return property.propertyType == SerializedPropertyType.String ? property.stringValue : Label(property);

            if (property.propertyType == SerializedPropertyType.ManagedReference)
            {
                string title = TypeLabel(property.managedReferenceValue?.GetType());
                var delivery = property.FindPropertyRelative("_delivery");

                return delivery == null ? title : title + " · " + EnumText(delivery) + " / " +
                    EnumText(property.FindPropertyRelative("_area"));
            }

            if (property.FindPropertyRelative("_entryId") != null)
                return EnumText(property.FindPropertyRelative("_timing")) + " → " +
                    EnumText(property.FindPropertyRelative("_subject")) + " · 효과 " +
                    property.FindPropertyRelative("_effects").arraySize;

            var key = property.FindPropertyRelative("_key");

            return key != null ? (string.IsNullOrEmpty(key.stringValue) ? "FX 키 미입력" : key.stringValue) : Label(property);
        }

        private static string EnumText(SerializedProperty property)
        {
            int index = property?.enumValueIndex ?? -1;

            return index >= 0 && index < property.enumDisplayNames.Length
                ? property.enumDisplayNames[index] : "미설정";
        }

        // ============================================================
        // Cards / Lists
        // ============================================================

        public static void Header(string title, string summary)
        {
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            EditorGUILayout.LabelField(summary, EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(6);
        }

        // 타입별 전용 입력기를 유지하면서 목록의 접기·순서 변경·삭제를 같은 위치에 둔다.
        public static void Cards(SerializedProperty list, Action<SerializedProperty, int> drawBody,
            Action add, string title, bool initiallyOpen = true)
        {
            bool expanded;

            using (new EditorGUILayout.HorizontalScope())
            {
                expanded = Foldout(list, title + " (" + list.arraySize + ")", initiallyOpen);

                if (GUILayout.Button(new GUIContent("+ 추가", title + " 추가"), EditorStyles.miniButton, GUILayout.Width(54)))
                {
                    add();

                    GUIUtility.ExitGUI();
                }
            }

            if (!expanded)
                return;

            if (list.arraySize == 0)
                EditorGUILayout.LabelField("아직 항목이 없습니다.", EditorStyles.miniLabel);

            for (int i = 0; i < list.arraySize; i++)
            {
                var element = list.GetArrayElementAtIndex(i);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    bool open;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        open = Foldout(element, (i + 1) + ". " + Summary(element), i == 0);

                        using (new EditorGUI.DisabledScope(i == 0))
                            if (GUILayout.Button(new GUIContent("↑", "위로 이동"), EditorStyles.miniButton, GUILayout.Width(24)))
                            {
                                Move(list, i, i - 1);

                                GUIUtility.ExitGUI();
                            }

                        using (new EditorGUI.DisabledScope(i + 1 == list.arraySize))
                            if (GUILayout.Button(new GUIContent("↓", "아래로 이동"), EditorStyles.miniButton, GUILayout.Width(24)))
                            {
                                Move(list, i, i + 1);

                                GUIUtility.ExitGUI();
                            }

                        if (GUILayout.Button(new GUIContent("×", "삭제 (Undo 가능)"), EditorStyles.miniButton, GUILayout.Width(24)))
                        {
                            Remove(list, i);

                            GUIUtility.ExitGUI();
                        }
                    }

                    if (open)
                        drawBody(element, i);
                }
            }

            EditorGUILayout.Space(4);
        }

        // ============================================================
        // Serialized Editing / Undo
        // ============================================================

        public static void Commit(SerializedObject owner)
        {
            if (!owner.ApplyModifiedProperties())
                return;

            Undo.RecordObject(owner.targetObject, "스킬 엔트리 ID 정리");

            if (owner.targetObject is ActiveSkillData active)
                active.EnsureEntryIds();

            if (owner.targetObject is PassiveSkillData passive)
                passive.EnsureEntryIds();

            EditorUtility.SetDirty(owner.targetObject);

            owner.Update();
        }

        public static void Move(SerializedProperty list, int from, int to)
        {
            list.MoveArrayElement(from, to);

            Commit(list.serializedObject);
        }

        public static void Remove(SerializedProperty list, int index)
        {
            var element = list.GetArrayElementAtIndex(index);

            if (element.propertyType == SerializedPropertyType.ManagedReference)
                element.managedReferenceValue = null;

            int count = list.arraySize;

            list.DeleteArrayElementAtIndex(index);

            if (list.arraySize == count)
                list.DeleteArrayElementAtIndex(index);

            Commit(list.serializedObject);
        }

        public static void Add(SerializedProperty list, object value)
        {
            int index = list.arraySize++;
            var element = list.GetArrayElementAtIndex(index);

            if (element.propertyType == SerializedPropertyType.ManagedReference)
                element.managedReferenceValue = value;
            else
                element.boxedValue = value;

            Commit(list.serializedObject);

            SessionState.SetBool(Key(list.serializedObject, list.propertyPath), true);
            SessionState.SetBool(Key(list.serializedObject, list.propertyPath + ".Array.data[" + index + "]"), true);
        }

        // 메뉴 콜백에서는 오래된 SerializedProperty를 보관하지 않고 경로로 다시 찾는다.
        public static void TypeMenu(SerializedProperty property, bool append)
        {
            var owner = property.serializedObject;
            string path = property.propertyPath;
            Type baseType;

            if (append)
            {
                baseType = ResolveElementType(property);
            }
            else
            {
                var name = property.managedReferenceFieldTypename.Split(' ');
                baseType = name.Length == 2 ? Type.GetType(name[1] + ", " + name[0]) : null;
            }

            if (baseType == null)
                return;

            Commit(owner);

            var menu = new GenericMenu();

            foreach (var type in TypeCache.GetTypesDerivedFrom(baseType).OrderBy(TypeLabel))
            {
                if (type.IsAbstract || type.IsGenericType || type.GetConstructor(Type.EmptyTypes) == null)
                    continue;

                var selected = type;

                menu.AddItem(new GUIContent(TypeLabel(type)), false, () =>
                {
                    if (owner.targetObject == null)
                        return;

                    owner.Update();

                    var current = owner.FindProperty(path);

                    if (current == null)
                        return;

                    if (append)
                        Add(current, Activator.CreateInstance(selected));
                    else
                    {
                        current.managedReferenceValue = Activator.CreateInstance(selected);

                        Commit(owner);
                    }
                });
            }

            menu.ShowAsContext();
        }

        internal static Type ResolveElementType(SerializedProperty list)
        {
            // 비어 있는 SerializeReference 배열은 Unity가 타입명을 제공하지 않으므로 선언 필드를 읽는다.
            int separator = list.propertyPath.LastIndexOf('.');
            object container = list.serializedObject.targetObject;

            if (separator >= 0)
            {
                var parent = list.serializedObject.FindProperty(list.propertyPath.Substring(0, separator));

                if (parent == null)
                    return null;

                container = parent.propertyType == SerializedPropertyType.ManagedReference
                    ? parent.managedReferenceValue : parent.boxedValue;
            }

            for (Type type = container?.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(list.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (field == null)
                    continue;

                return field.FieldType.IsArray ? field.FieldType.GetElementType() :
                    field.FieldType.IsGenericType ? field.FieldType.GetGenericArguments()[0] : null;
            }

            return null;
        }

        // ============================================================
        // Diagnostics
        // ============================================================

        public static void Diagnostics(SerializedObject owner)
        {
            var errors = new List<string>();

            try
            {
                SkillAssetMigration.Validate(owner.targetObject as ScriptableObject, errors);
            }
            catch (Exception error)
            {
                errors.Add(error.Message);
            }

            EditorGUILayout.Space(6);

            if (errors.Count == 0)
            {
                EditorGUILayout.LabelField("설정 검사 · 문제 없음", EditorStyles.miniLabel);

                return;
            }

            if (Foldout(owner, "diagnostics", "⚠ 확인할 설정 " + errors.Count + "개", true))
                foreach (string error in errors)
                    EditorGUILayout.HelpBox(error, MessageType.Warning);
        }
    }
}
#endif
