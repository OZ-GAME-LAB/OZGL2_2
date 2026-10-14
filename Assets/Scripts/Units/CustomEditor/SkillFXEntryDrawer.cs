#if UNITY_EDITOR
using System;
using Units.FX;
using Units.Skills;
using UnityEditor;
using UnityEngine;

namespace Units.Editor
{
    // 기본공격·액티브·조건부 효과에서 동일한 FX 편집 규칙을 사용한다.
    [CustomPropertyDrawer(typeof(SkillFXEntry))]
    public sealed class SkillFXEntryDrawer : PropertyDrawer
    {
        private const float Gap = 3f;
        private static float Line => EditorGUIUtility.singleLineHeight;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            bool legacy = property.FindPropertyRelative("_operation").intValue == 0;
            return (Line + Gap) * (legacy ? 12 : 11) + 36f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var row = new Rect(position.x, position.y, position.width, Line);
            var operation = property.FindPropertyRelative("_operation");
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(row, operation, new GUIContent("FX 동작", "Projectile Flight: 비행 중 / Collision: 투사체 충돌 위치 또는 즉시 공격 중심에서 한 번 / Hit: 투사체로 실제 피해(보호막 포함)를 입은 대상마다 한 번"));
            if (EditorGUI.EndChangeCheck()) ApplyDefaults(property);
            Next(ref row);
            if (GUI.Button(row, "선택한 동작의 기본값 적용")) ApplyDefaults(property);
            Next(ref row);
            Draw(ref row, property, "_kind", "종류");
            Draw(ref row, property, "_key", "카탈로그 Key");
            if (GUI.Button(row, "카탈로그에서 선택")) ShowKeys(property);
            Next(ref row);
            if (operation.intValue == 0) Draw(ref row, property, "_hook", "기존 Hook (고급)");
            Draw(ref row, property, "_attachment", "부착 대상");
            Draw(ref row, property, "_followDirection", "방향 추적");
            bool sharedScale = operation.intValue == (int)SkillFXOperation.Hit
                || operation.intValue == (int)SkillFXOperation.ProjectileFlight
                || (operation.intValue == 0 && (property.FindPropertyRelative("_hook").intValue == (int)SkillFXHook.OnHit
                    || property.FindPropertyRelative("_hook").intValue == (int)SkillFXHook.Flight));
            using (new EditorGUI.DisabledScope(sharedScale))
            {
                var scaleMode = property.FindPropertyRelative("_scaleMode");
                if (sharedScale) scaleMode.intValue = 0;
                scaleMode.intValue = EditorGUI.Popup(row, "크기 기준", scaleMode.intValue,
                    new[] { "카탈로그 유지", "요청별 배율", "공격 반경 연동" });
                Next(ref row);
                using (new EditorGUI.DisabledScope(scaleMode.intValue == 0))
                    Draw(ref row, property, "_scaleMultiplier", "크기 보정 배율");
            }
            var end = property.FindPropertyRelative("_endPolicy");
            // 기존 값 1은 읽을 수 있게 하되 신규 항목에서 다시 선택하지 않도록 한다.
            string[] labels = end.intValue == 1
                ? new[] { "방출 중지 / 잔여 재생", "기존 유지 정책 (호환)", "독립 재생", "즉시 제거 / 풀 반환" }
                : new[] { "방출 중지 / 잔여 재생", "독립 재생", "즉시 제거 / 풀 반환" };
            int[] values = end.intValue == 1 ? new[] { 0, 1, 2, 3 } : new[] { 0, 2, 3 };
            end.intValue = EditorGUI.IntPopup(row, "종료 정책", end.intValue, labels, values);
            Next(ref row);
            string description = end.intValue switch
            {
                0 => "동작 종료 시 새 방출을 멈추고 남은 파티클·오디오 페이드가 끝나면 반환합니다.",
                1 => "기존 데이터 호환용입니다. 새 설정은 다른 종료 정책을 선택하세요.",
                2 => "동작 종료 후에도 자체 재생 완료 또는 최대 수명까지 재생합니다.",
                _ => "동작 종료 시 파티클·트레일·오디오를 지우고 즉시 반환합니다."
            };
            row.height = 36f;
            EditorGUI.HelpBox(row, description, MessageType.Info);
            EditorGUI.EndProperty();
        }

        private static void Next(ref Rect row) => row.y += Line + Gap;
        private static void Draw(ref Rect row, SerializedProperty property, string name, string label)
        {
            EditorGUI.PropertyField(row, property.FindPropertyRelative(name), new GUIContent(label));
            Next(ref row);
        }

        private static void ApplyDefaults(SerializedProperty property)
        {
            var operation = (SkillFXOperation)property.FindPropertyRelative("_operation").intValue;
            if (operation == SkillFXOperation.Legacy) return;
            var entry = new SkillFXEntry();
            entry.ApplyDefaults(operation);
            property.FindPropertyRelative("_hook").intValue = (int)entry.Hook;
            property.FindPropertyRelative("_endPolicy").intValue = (int)entry.EndPolicy;
            property.FindPropertyRelative("_attachment").intValue = (int)entry.Attachment;
            property.FindPropertyRelative("_followDirection").boolValue = entry.FollowDirection;
        }

        private static void ShowKeys(SerializedProperty property)
        {
            var menu = new GenericMenu();
            bool vfx = property.FindPropertyRelative("_kind").intValue == (int)FXKind.VFX;
            int operation = property.FindPropertyRelative("_operation").intValue;
            var owner = property.serializedObject.targetObject;
            string path = property.propertyPath + "._key";
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets(vfx ? "t:VFXCatalogSO" : "t:SFXCatalogSO"))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                using var catalog = new SerializedObject(asset);
                var entries = catalog.FindProperty("_entries");
                for (int i = 0; i < entries.arraySize; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    string key = entry.FindPropertyRelative("_key").stringValue;
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    int group = entry.FindPropertyRelative("_operation").intValue;
                    string prefix = group == operation ? "현재 동작/" : "다른 동작/";
                    int category = entry.FindPropertyRelative("_category").intValue;
                    string categoryName = category >= 0 && category < FXCatalogEditor.CategoryNames.Length
                        ? FXCatalogEditor.CategoryNames[category] : "미분류";
                    menu.AddItem(new GUIContent(prefix + categoryName + "/" + asset.name + "/" + key), false, () =>
                    {
                        if (owner == null) return;
                        using var serialized = new SerializedObject(owner);
                        var destination = serialized.FindProperty(path);
                        if (destination == null) return;
                        destination.stringValue = key;
                        serialized.ApplyModifiedProperties();
                    });
                    count++;
                }
            }
            if (count == 0) menu.AddDisabledItem(new GUIContent("등록된 Key 없음"));
            menu.ShowAsContext();
        }
    }
}
#endif
