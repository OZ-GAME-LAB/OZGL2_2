#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Units.Skills;
using Units.Effects;
using UnityEditor;
using UnityEngine;

// 중첩 액션·조건·효과의 편집 UI와 지원하지 않는 설정의 진단을 제공한다.
namespace Units.Editor
{
    // 중첩 SerializeReference 목록에도 구체 타입 생성/교체와 순서 변경을 제공한다.
    internal static class SkillAuthoringGUI
    {

        // ============================================================
        // Execution
        // ============================================================

        public static void Draw(SerializedProperty property)
        {
            if (property == null)
                return;

            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                SkillInspectorUI.Cards(property, (element, _) => DrawBody(element),
                    () => AddItem(property), SkillInspectorUI.Label(property), property.arraySize > 0);

                return;
            }

            if (property.propertyType == SerializedPropertyType.ManagedReference)
            {
                DrawBody(property);

                return;
            }

            if (property.propertyType == SerializedPropertyType.Generic && property.hasVisibleChildren)
            {
                if (SkillInspectorUI.Foldout(property, initiallyOpen: property.name == "_target"))
                    DrawBody(property);

                return;
            }

            EditorGUILayout.PropertyField(property, new GUIContent(SkillInspectorUI.Label(property)), true);
        }

        private static void DrawBody(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference &&
                property.propertyType != SerializedPropertyType.Generic)
            {
                EditorGUILayout.PropertyField(property, GUIContent.none, true);

                return;
            }

            if (property.propertyType == SerializedPropertyType.ManagedReference)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(SkillInspectorUI.TypeLabel(property.managedReferenceValue?.GetType()), EditorStyles.miniLabel);

                    if (GUILayout.Button(new GUIContent("타입 변경", "선택한 타입의 기본 설정으로 교체합니다. Undo로 되돌릴 수 있습니다."),
                        EditorStyles.miniButton, GUILayout.Width(72)))
                        ShowTypes(property);
                }

                if (property.managedReferenceValue == null)
                    return;
            }

            if (property.propertyType == SerializedPropertyType.ManagedReference && property.managedReferenceValue is SkillStackCondition)
            {
                Field(property, "_subject");
                Field(property, "_query");
                Field(property, "_comparison");
                Field(property, "_count");
            }
            else if (property.FindPropertyRelative("_kind") != null && property.FindPropertyRelative("_statusType") != null && property.FindPropertyRelative("_key") != null)
            {
                var kind = property.FindPropertyRelative("_kind");
                EditorGUILayout.PropertyField(kind, new GUIContent("조회 기준"));
                if (kind.intValue == (int)EffectStackQueryKind.Status)
                    EditorGUILayout.PropertyField(property.FindPropertyRelative("_statusType"), new GUIContent("상태이상"));
                else
                    EditorGUILayout.PropertyField(property.FindPropertyRelative("_key"), new GUIContent("효과 ID"));
            }
            else if (property.FindPropertyRelative("_baseEffects") != null)
                DrawAction(property);
            else if (property.FindPropertyRelative("_entryId") != null)
                DrawEntry(property);
            else if (property.FindPropertyRelative("_source") != null && property.FindPropertyRelative("_relation") != null)
                DrawTarget(property);
            else if (property.propertyType is SerializedPropertyType.Generic or SerializedPropertyType.ManagedReference)
                Children(property);
            else
                EditorGUILayout.PropertyField(property, GUIContent.none, true);
        }

        private static void AddItem(SerializedProperty list)
        {
            if (list.arrayElementType.StartsWith("managedReference"))
            {
                SkillInspectorUI.TypeMenu(list, true);

                return;
            }

            object value = list.name switch
            {
                "_activeSkills" => new UnitActiveSkillEntry(),
                "_phases" => new Hero_PhaseData(),
                "_transitions" => new Hero_PhaseTransitionData(),
                "_skillPolicies" => new Hero_PhaseSkillPolicy(),
                "_statModifiers" => new Hero_PhaseStatModifier(),
                "_baseEffects" => new SkillEffectEntry(),
                "_conditionalEffects" => new SkillConditionalEffectEntry(),
                "_fxEntries" => new SkillFXEntry(),
                "_filters" => new SkillTargetNumericFilter(default, default, 0f),
                "_priorities" => new SkillTargetPriority(default),
                "_categories" => "",
                _ => null
            };

            if (value != null) SkillInspectorUI.Add(list, value);
            else if (list.name == "_maintainedEffects") SkillInspectorUI.Add(list, null);
        }
        // Action의 동작과 효과 정의를 분리해 현재 동작에 필요한 설정만 보여 준다.
        private static void DrawAction(SerializedProperty action)
        {
            int tab = SkillInspectorUI.Tabs(action, "동작 / 대상", "효과", "고급 / FX");

            if (tab == 1)
            {
                Field(action, "_baseEffects");

                Field(action, "_conditionalEffects");

                return;
            }

            if (tab == 2)
            {
                Field(action, "_origin");

                Field(action, "_targetLostPolicy");

                Field(action, "_failurePolicy");

                var target = action.FindPropertyRelative("_target");
                var source = target?.FindPropertyRelative("_source");
                var relation = target?.FindPropertyRelative("_relation");

                if (action.FindPropertyRelative("_targetLostPolicy").intValue == (int)SkillTargetLostPolicy.Reselect &&
                    source != null && source.intValue != (int)SkillTargetSource.None &&
                    source.intValue != (int)SkillTargetSource.Self && source.intValue != (int)SkillTargetSource.Search &&
                    relation.intValue != (int)SkillTargetRelation.Self)
                {
                    EditorGUILayout.LabelField("대상 소실 시 재탐색", EditorStyles.boldLabel);

                    Field(target, "_range");

                    Field(target, "_maxTargetCount");

                    Field(target, "_priorities");

                    Field(target, "_clusterRadius");
                }

                EditorGUILayout.Space(6);

                Field(action, "_conditions");

                EditorGUILayout.LabelField("공통 조건은 효과 적용만 제한합니다.", EditorStyles.wordWrappedMiniLabel);

                Field(action, "_fxEntries");

                return;
            }

            Field(action, "_duration");

            Field(action, "_distance");

            Field(action, "_speed");

            var delivery = action.FindPropertyRelative("_delivery");

            if (delivery != null)
            {
                Draw(delivery);

                Field(action, "_area");

                var area = action.FindPropertyRelative("_area");

                if (area.intValue != (int)ActiveSkillAreaType.Single)
                {
                    Field(action, "_radius");

                    Field(action, "_maxEffectTargets");

                    if (area.intValue == (int)ActiveSkillAreaType.SelfCone)
                        Field(action, "_angle");
                }

                if (delivery.intValue == (int)ActiveSkillDeliveryType.Projectile)
                {
                    Field(action, "_projectileSpeed");

                    var legacy = action.FindPropertyRelative("_isLegacy");
                    var target = action.FindPropertyRelative("_target");

                    if (legacy != null && legacy.boolValue && target != null &&
                        target.FindPropertyRelative("_source").intValue != (int)SkillTargetSource.Search)
                        EditorGUILayout.PropertyField(target.FindPropertyRelative("_maxTargetCount"),
                            new GUIContent("발사 대상 수"));
                }
            }

            EditorGUILayout.Space(6);

            Field(action, "_target");

            EditorGUILayout.LabelField("기본 효과 " + action.FindPropertyRelative("_baseEffects").arraySize +
                " · 조건부 효과 " + action.FindPropertyRelative("_conditionalEffects").arraySize,
                EditorStyles.miniLabel);
        }

        // Self 엔트리만 실행당 1회 설정을 제공하며 소비 옵션은 명시적으로 켠 경우만 노출한다.
        private static void DrawEntry(SerializedProperty entry)
        {
            Field(entry, "_timing");

            Field(entry, "_order");

            Field(entry, "_subject");

            int subject = entry.FindPropertyRelative("_subject").intValue;

            if (subject == (int)SkillEffectSubject.Self)
                Field(entry, "_frequency");

            if (subject == (int)SkillEffectSubject.SnapshotArea)
                Field(entry, "_snapshotAreaTarget");

            var conditions = entry.FindPropertyRelative("_conditions");
            if (conditions != null)
                DrawConditionalConditions(entry, conditions);

            Field(entry, "_effects");

            Field(entry, "_fxEntries");
        }

        private static void DrawConditionalConditions(SerializedProperty entry, SerializedProperty conditions)
        {
            SkillInspectorUI.Cards(conditions, (condition, _) =>
            {
                DrawBody(condition);
                if (condition.managedReferenceValue is not SkillStackCondition) return;

                var consume = condition.FindPropertyRelative("_consumeOnApply");
                EditorGUI.BeginChangeCheck();
                bool next = EditorGUILayout.Toggle("조건 충족 시 중첩 소비", consume.boolValue);
                if (EditorGUI.EndChangeCheck())
                {
                    if (next)
                        for (int i = 0; i < conditions.arraySize; i++)
                        {
                            var flag = conditions.GetArrayElementAtIndex(i).FindPropertyRelative("_consumeOnApply");
                            if (flag != null) flag.boolValue = false;
                        }
                    consume.boolValue = next;
                }
                if (consume.boolValue)
                {
                    EditorGUILayout.PropertyField(condition.FindPropertyRelative("_consumeCount"), new GUIContent("소비 개수"));
                    EditorGUILayout.LabelField("위 조건의 대상과 조회 기준을 그대로 사용합니다. 소비할 중첩이 부족하면 효과를 적용하지 않습니다.", EditorStyles.wordWrappedMiniLabel);
                }
            }, () => AddItem(conditions), "조건", conditions.arraySize > 0);
        }

        private static void DrawTarget(SerializedProperty target)
        {
            Field(target, "_source");

            var source = (SkillTargetSource)target.FindPropertyRelative("_source").intValue;

            if (source == SkillTargetSource.None || source == SkillTargetSource.Self)
                return;

            Field(target, "_relation");

            var relation = (SkillTargetRelation)target.FindPropertyRelative("_relation").intValue;

            if (relation == SkillTargetRelation.Self)
                return;

            if (relation == SkillTargetRelation.Friendly)
                Field(target, "_includeSelf");

            if (source == SkillTargetSource.Search)
            {
                Field(target, "_range");

                Field(target, "_maxTargetCount");

                Field(target, "_priorities");

                Field(target, "_clusterRadius");
            }

            Field(target, "_filters");
        }

        private static void Field(
            SerializedProperty parent,
            string name)
        {
            var property = parent.FindPropertyRelative(name);

            if (property != null)
                Draw(property);
        }

        private static void Children(SerializedProperty parent)
        {
            var child = parent.Copy();

            var end = child.GetEndProperty();

            EditorGUI.indentLevel++;

            if (child.NextVisible(true))
                do
                {
                    if (SerializedProperty.EqualContents(child, end))
                        break;

                    Draw(child);
                }
                while (child.NextVisible(false));

            EditorGUI.indentLevel--;
        }

        private static void ShowTypes(SerializedProperty property)
        {
            SkillInspectorUI.TypeMenu(property, false);
        }

        // ============================================================
        // Validation
        // ============================================================

        public static void ValidateActions(ActiveSkillData data)
        {
            var errors = new List<string>();

            SkillAssetMigration.Validate(data, errors);

            foreach (var error in errors)
                EditorGUILayout.HelpBox(error, MessageType.Warning);
        }

        public static List<string> ValidateEffectIds()
        {
            var ids = new Dictionary<string, string>();

            var errors = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:EffectData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                var data = AssetDatabase.LoadAssetAtPath<EffectData>(path);

                if (data == null)
                    continue;

                if (string.IsNullOrWhiteSpace(data.EffectId))
                    errors.Add("빈 EffectId: " + path);

                else if (ids.TryGetValue(data.EffectId, out string prior))
                    errors.Add("중복 EffectId " + data.EffectId + ": " + prior + " / " + path);

                else
                    ids[data.EffectId] = path;
            }

            return errors;
        }
    }
}
#endif
