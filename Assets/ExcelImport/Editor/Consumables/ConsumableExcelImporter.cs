using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Units.Effects;
using Units.Skills;
using UnityEditor;
using UnityEngine;

public static class ConsumableExcelImporter
{
    public static bool TryImport(string path, ConsumableItemCatalog catalog, string folder, out string result)
    {
        result = "";
        if (EditorApplication.isPlayingOrWillChangePlaymode || catalog == null)
        { result = "Play 종료 후 ConsumableItemCatalog를 연결하세요."; return false; }
        folder = (folder ?? "").Replace('\\', '/').TrimEnd('/');
        if (!folder.StartsWith("Assets/") || folder.Split('/').Any(p => p == ".." || p == ".") ||
            !AssetDatabase.IsValidFolder(folder + "/Items") || !AssetDatabase.IsValidFolder(folder + "/Effects"))
        { result = "출력 폴더 아래에 Items와 Effects 폴더가 있어야 합니다."; return false; }
        if (!ConsumableExcelParser.TryParse(path, out var book, out result)) return false;
        if (!FindExisting<ConsumableItemData>(book.Items.Keys, a => a.Id, out var items, out result) ||
            !FindExisting<EffectData>(book.RuntimeEffects.Keys, a => a.EffectId, out var effects, out result)) return false;

        var catalogObject = new SerializedObject(catalog);
        var registered = catalogObject.FindProperty("_items");
        var ids = new HashSet<string>();
        var catalogItems = new HashSet<ConsumableItemData>();
        for (int i = 0; i < registered.arraySize; i++)
        {
            var item = registered.GetArrayElementAtIndex(i).objectReferenceValue as ConsumableItemData;
            if (item == null || string.IsNullOrWhiteSpace(item.Id) || !ids.Add(item.Id))
            { result = "카탈로그의 빈 항목 또는 중복 ID를 먼저 정리하세요."; return false; }
            catalogItems.Add(item);
        }

        // 검증 완료 후 변경. 기존 파일/GUID/아이콘과 엑셀에 없는 에셋은 유지합니다.
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Import consumable workbook");
        var createdPaths = new List<string>();
        try
        {
            foreach (var row in book.RuntimeEffects.Values)
            {
                if (!effects.TryGetValue(row.Id, out var asset))
                {
                    asset = ScriptableObject.CreateInstance<EffectData>();
                    Create(asset, folder + "/Effects", row.Id, createdPaths);
                    effects.Add(row.Id, asset);
                }
                Undo.RecordObject(asset, "Update item runtime effect");
                var serialized = new SerializedObject(asset);
                serialized.FindProperty("_effectId").stringValue = row.Id;
                serialized.FindProperty("_skillSchemaVersion").intValue = 1;
                serialized.FindProperty("_alignment").intValue = (int)row.Alignment;
                serialized.FindProperty("_durationType").intValue = (int)row.DurationType;
                serialized.FindProperty("_duration").floatValue = row.Duration;
                serialized.FindProperty("_stackType").intValue = (int)row.StackType;
                serialized.FindProperty("_maxStack").intValue = row.MaxStacks;
                // 분류는 Inspector 설정을 유지합니다. 신규 효과는 EffectData의 자동 분류를 사용합니다.
                var actions = serialized.FindProperty("_actions");
                actions.ClearArray();
                actions.arraySize = row.Actions.Count;
                for (int i = 0; i < row.Actions.Count; i++) WriteAction(actions.GetArrayElementAtIndex(i), row.Actions[i]);
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
            }
            foreach (var row in book.Items.Values)
            {
                if (!items.TryGetValue(row.Id, out var asset))
                {
                    asset = ScriptableObject.CreateInstance<ConsumableItemData>();
                    Create(asset, folder + "/Items", row.Id, createdPaths);
                    items.Add(row.Id, asset);
                }
                Undo.RecordObject(asset, "Update consumable item");
                var serialized = new SerializedObject(asset);
                serialized.FindProperty("_id").stringValue = row.Id;
                serialized.FindProperty("_displayName").stringValue = row.Name;
                serialized.FindProperty("_description").stringValue = row.Description;
                serialized.FindProperty("_targetTeam").intValue = (int)row.Team;
                serialized.FindProperty("_targetMode").intValue = (int)row.Mode;
                serialized.FindProperty("_radius").floatValue = row.Radius;
                var list = serialized.FindProperty("_effects");
                list.ClearArray();
                list.arraySize = row.Effects.Count;
                for (int i = 0; i < row.Effects.Count; i++) WriteEffect(list.GetArrayElementAtIndex(i), row.Effects[i], effects);
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
                if (catalogItems.Add(asset))
                {
                    registered.arraySize++;
                    registered.GetArrayElementAtIndex(registered.arraySize - 1).objectReferenceValue = asset;
                }
            }
            Undo.RecordObject(catalog, "Update consumable catalog");
            catalogObject.ApplyModifiedProperties();
            // 등록 수가 같아도 ID 조회 캐시를 새로 구성합니다.
            typeof(ConsumableItemCatalog).GetMethod("OnValidate",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(catalog, null);
            EditorUtility.SetDirty(catalog);
            foreach (var asset in effects.Values) AssetDatabase.SaveAssetIfDirty(asset);
            foreach (var asset in items.Values) AssetDatabase.SaveAssetIfDirty(asset);
            AssetDatabase.SaveAssetIfDirty(catalog);
            Undo.CollapseUndoOperations(undoGroup);
            result = $"아이템 {book.Items.Count}종, 지속 효과 {book.RuntimeEffects.Count}종 반영 완료. 기존 아이콘·파일 참조 유지.";
            return true;
        }
        catch (Exception exception)
        {
            Undo.RevertAllDownToGroup(undoGroup);
            foreach (string created in createdPaths) AssetDatabase.DeleteAsset(created);
            result = "가져오기 실패: " + exception.Message;
            return false;
        }
    }

    private static bool FindExisting<T>(IEnumerable<string> requested, Func<T, string> getId,
        out Dictionary<string, T> found, out string error) where T : ScriptableObject
    {
        found = new Dictionary<string, T>();
        error = "";
        var ids = new HashSet<string>(requested);
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets" }))
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset == null || !ids.Contains(getId(asset))) continue;
            if (!found.TryAdd(getId(asset), asset)) { error = "기존 S.O 중복 ID: " + getId(asset); return false; }
        }
        return true;
    }

    private static void Create(ScriptableObject asset, string folder, string id, List<string> created)
    {
        string name = Regex.Replace(id, "[^a-zA-Z0-9가-힣_-]", "_");
        string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".asset");
        AssetDatabase.CreateAsset(asset, path);
        created.Add(path);
    }

    private static void WriteEffect(SerializedProperty property, ConsumableExcelParser.Effect row, Dictionary<string, EffectData> effects)
    {
        switch (row.Kind)
        {
            case "Damage":
                property.managedReferenceValue = new SkillDamageEffectData();
                property.FindPropertyRelative("_damageType").intValue = (int)row.DamageType;
                property.FindPropertyRelative("_damageMultiplier").floatValue = row.Value;
                break;
            case "Heal":
                property.managedReferenceValue = new SkillHealEffectData();
                property.FindPropertyRelative("_scalingStatType").intValue = row.Scaling;
                property.FindPropertyRelative("_healRatio").floatValue = row.Value;
                break;
            case "Shield":
                property.managedReferenceValue = new SkillShieldEffectData();
                property.FindPropertyRelative("_scalingStatType").intValue = row.Scaling;
                property.FindPropertyRelative("_shieldRatio").floatValue = row.Value;
                break;
            case "Runtime":
                property.managedReferenceValue = new SkillRuntimeEffectData();
                property.FindPropertyRelative("_effectData").objectReferenceValue = effects[row.RuntimeId];
                break;
        }
    }

    private static void WriteAction(SerializedProperty property, ConsumableExcelParser.ActionData row)
    {
        switch (row.Kind)
        {
            case "Stat":
                property.managedReferenceValue = new StatEffectActionData();
                property.FindPropertyRelative("_statType").intValue = (int)row.Stat;
                property.FindPropertyRelative("_modifierType").intValue = (int)row.Modifier;
                property.FindPropertyRelative("_value").floatValue = row.Value;
                break;
            case "PeriodicHeal":
                property.managedReferenceValue = new PeriodicHealEffectActionData();
                property.FindPropertyRelative("_interval").floatValue = row.Interval;
                property.FindPropertyRelative("_healRatio").floatValue = row.Value;
                break;
            case "PeriodicDamage":
                property.managedReferenceValue = new PeriodicDamageEffectActionData();
                property.FindPropertyRelative("_interval").floatValue = row.Interval;
                property.FindPropertyRelative("_damage").floatValue = row.Value;
                break;
            case "Status":
                property.managedReferenceValue = new StatusEffectActionData();
                property.FindPropertyRelative("_statusType").intValue = (int)row.Status;
                break;
        }
    }
}
