#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Units.Editor
{
    // 카탈로그 목록과 선택 항목을 분리한다. 모든 데이터 변경은 SerializedProperty로 Undo를 지원한다.
    public abstract class FXCatalogEditor : UnityEditor.Editor
    {
        // ============================================================
        // Runtime State
        // ============================================================

        private SerializedProperty _entries;
        private string _search = string.Empty;
        private bool _issuesOnly;
        private int _group;
        private int _category;
        internal static readonly string[] CategoryNames =
            { "미분류", "물리 직접 공격 (OnHit)", "투사체 (공용)", "일반 명중 (OnHit)", "스킬 명중 (ActiveOnHit)", "스킬 투사체 (ActiveProjectile)", "충돌 / 즉시 폭발 (Collision)", "근접 스킬 발동", "근접 스킬 피격", "돌진 후 타격", "회복 / 버프 / 디버프" };
        private static readonly string[] CategoryFilters =
            { "전체", "미분류", "물리 직접 공격 (OnHit)", "투사체 (공용)", "일반 명중 (OnHit)", "스킬 명중 (ActiveOnHit)", "스킬 투사체 (ActiveProjectile)", "충돌 / 즉시 폭발 (Collision)", "근접 스킬 발동", "근접 스킬 피격", "돌진 후 타격", "회복 / 버프 / 디버프" };
        private int _selected;
        private Vector2 _scroll;
        private readonly Dictionary<string, int> _keyCounts = new(StringComparer.Ordinal);

        protected abstract string CatalogName { get; }
        protected abstract string AssetField { get; }
        protected abstract void InitializeEntry(SerializedProperty entry);
        protected abstract void DrawEntry(SerializedProperty entry);

        private void OnEnable()
        {
            _entries = serializedObject.FindProperty("_entries");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

            _keyCounts.Clear();
            for (int i = 0; i < _entries.arraySize; i++)
            {
                string key = _entries.GetArrayElementAtIndex(i).FindPropertyRelative("_key").stringValue;
                _keyCounts.TryGetValue(key, out int count);
                _keyCounts[key] = count + 1;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(CatalogName, $"총 {_entries.arraySize}개", EditorStyles.boldLabel);
            _search = EditorGUILayout.TextField("검색 (Key / 에셋)", _search);
            _category = EditorGUILayout.Popup("카테고리", _category, CategoryFilters);
            _group = EditorGUILayout.Popup("동작 그룹", _group,
                new[] { "전체", "미분류 / 기존", "Cast", "Dash", "Projectile Flight", "Action", "Hit", "Collision", "Fire", "Expire" });
            _issuesOnly = EditorGUILayout.ToggleLeft("설정 오류가 있는 항목만 표시", _issuesOnly);

            int shown = 0;
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(Mathf.Clamp(_entries.arraySize * 25 + 8, 60, 220)));
            for (int i = 0; i < _entries.arraySize; i++)
            {
                var entry = _entries.GetArrayElementAtIndex(i);
                string key = entry.FindPropertyRelative("_key").stringValue;
                var asset = entry.FindPropertyRelative(AssetField).objectReferenceValue;
                int operation = entry.FindPropertyRelative("_operation").intValue;
                if (_group > 0 && operation != _group - 1) continue;
                if (_category > 0 && entry.FindPropertyRelative("_category").intValue != _category - 1) continue;
                bool issue = HasIssue(entry);
                if (_issuesOnly && !issue) continue;
                if (!string.IsNullOrEmpty(_search)
                    && key.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0
                    && (asset == null || asset.name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)) continue;

                shown++;
                string label = $"{i + 1}. {(string.IsNullOrWhiteSpace(key) ? "(Key 없음)" : key)}";
                label = $"[{(Units.Skills.SkillFXOperation)operation}] " + label;
                if (issue) label = "⚠ " + label;
                if (GUILayout.Toggle(_selected == i, new GUIContent(label, asset != null ? asset.name : "재생 에셋 없음"), "Button"))
                    _selected = i;
            }
            if (shown == 0) EditorGUILayout.LabelField("표시할 항목이 없습니다.", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndScrollView();

            _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, _entries.arraySize - 1));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("추가")) Add(false);
                using (new EditorGUI.DisabledScope(_entries.arraySize == 0))
                {
                    if (GUILayout.Button("복제")) Add(true);
                    if (GUILayout.Button("삭제"))
                    {
                        _entries.DeleteArrayElementAtIndex(_selected);
                        Commit();
                    }
                }
            }

            if (_entries.arraySize > 0)
            {
                EditorGUILayout.Space();
                var entry = _entries.GetArrayElementAtIndex(_selected);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"선택 항목 {_selected + 1}", EditorStyles.boldLabel);
                    using (new EditorGUI.DisabledScope(_selected == 0))
                        if (GUILayout.Button("↑", GUILayout.Width(28))) Move(-1);
                    using (new EditorGUI.DisabledScope(_selected == _entries.arraySize - 1))
                        if (GUILayout.Button("↓", GUILayout.Width(28))) Move(1);
                }
                if (HasIssue(entry))
                    EditorGUILayout.HelpBox("Key가 비어 있거나 중복되었거나, 재생 에셋이 누락되었습니다. 중복 Key는 첫 유효 항목만 사용됩니다.", MessageType.Warning);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    var category = entry.FindPropertyRelative("_category");
                    category.intValue = EditorGUILayout.Popup("카테고리", category.intValue, CategoryNames);
                    Field(entry, "_operation", "권장 동작 그룹");
                    DrawEntry(entry);
                }
            }
            serializedObject.ApplyModifiedProperties();
        }

        // ============================================================
        // Entry Editing
        // ============================================================

        private bool HasIssue(SerializedProperty entry)
        {
            string key = entry.FindPropertyRelative("_key").stringValue;
            return string.IsNullOrWhiteSpace(key) || _keyCounts[key] > 1
                || entry.FindPropertyRelative(AssetField).objectReferenceValue == null;
        }

        private void Add(bool duplicate)
        {
            int index = duplicate ? _selected : _entries.arraySize;
            _entries.InsertArrayElementAtIndex(index);
            _selected = index;
            var entry = _entries.GetArrayElementAtIndex(index);
            if (!duplicate)
            {
                InitializeEntry(entry);
                entry.FindPropertyRelative("_category").intValue = _category > 0 ? _category - 1 : 0;
                entry.FindPropertyRelative("_operation").intValue = _group > 0 ? _group - 1 : 0;
            }
            string stem = duplicate ? entry.FindPropertyRelative("_key").stringValue + "_Copy" : CatalogName + "_New";
            string key = stem;
            for (int suffix = 2; _keyCounts.ContainsKey(key); suffix++) key = stem + "_" + suffix;
            entry.FindPropertyRelative("_key").stringValue = key;
            _search = string.Empty;
            _issuesOnly = false;
            Commit();
        }

        private void Move(int delta)
        {
            _entries.MoveArrayElement(_selected, _selected + delta);
            _selected += delta;
            Commit();
        }

        private void Commit()
        {
            serializedObject.ApplyModifiedProperties();
            GUIUtility.ExitGUI();
        }

        protected static void Field(SerializedProperty entry, string name, string label = null)
        {
            var property = entry.FindPropertyRelative(name);
            if (label == null) EditorGUILayout.PropertyField(property, true);
            else EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);
        }

        protected static void Section(string title)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }
    }
}
#endif
