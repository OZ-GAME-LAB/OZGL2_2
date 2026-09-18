using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class ArtifactExcelImporter
{
    public static bool TryImport(string path, ArtifactCatalog catalog, string folder, out string result)
    {
        result = "";
        if (EditorApplication.isPlayingOrWillChangePlaymode || catalog == null)
        {
            result = "Play 종료 후 대상 ArtifactCatalog를 연결하세요.";
            return false;
        }
        folder = (folder ?? "").Replace('\\', '/').TrimEnd('/');
        if (!folder.StartsWith("Assets/") || !AssetDatabase.IsValidFolder(folder))
        {
            result = "신규 S.O 폴더는 Assets 하위의 기존 폴더로 지정하세요.";
            return false;
        }
        if (!ArtifactExcelParser.TryParse(path, out List<ArtifactExcelParser.Data> data, out result))
        {
            return false;
        }

        // 전체 에셋에서 ID 조회. 동일 ID가 여러 파일이면 변경 전에 중단
        Dictionary<string, ArtifactData> existing = new Dictionary<string, ArtifactData>();
        HashSet<string> requested = new HashSet<string>();
        foreach (var item in data)
        {
            requested.Add(item.Id);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:ArtifactData", new[] { "Assets" }))
        {
            ArtifactData asset = AssetDatabase.LoadAssetAtPath<ArtifactData>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset == null || string.IsNullOrEmpty(asset.Id) || !requested.Contains(asset.Id))
            {
                continue;
            }
            if (existing.ContainsKey(asset.Id))
            {
                result = "기존 S.O에 중복된 ID가 있습니다: " + asset.Id;
                return false;
            }
            existing.Add(asset.Id, asset);
        }

        SerializedObject catalogObject = new SerializedObject(catalog);
        catalogObject.Update();
        SerializedProperty registered = catalogObject.FindProperty("_artifacts");
        HashSet<ArtifactData> catalogAssets = new HashSet<ArtifactData>();
        HashSet<string> catalogIds = new HashSet<string>();
        for (int i = 0; i < registered.arraySize; i++)
        {
            ArtifactData asset = registered.GetArrayElementAtIndex(i).objectReferenceValue as ArtifactData;
            if (asset == null || string.IsNullOrWhiteSpace(asset.Id) || !catalogIds.Add(asset.Id))
            {
                result = "대상 카탈로그의 빈 항목 또는 중복 ID를 먼저 확인하세요.";
                return false;
            }
            catalogAssets.Add(asset);
        }

        // 입력 검증이 모두 끝난 후에만 생성·수정. 엑셀에 없는 에셋과 등록 항목은 유지
        int created = 0;
        foreach (var item in data)
        {
            bool isNew = !existing.TryGetValue(item.Id, out ArtifactData asset);
            if (isNew)
            {
                asset = ScriptableObject.CreateInstance<ArtifactData>();
            }
            SerializedObject serialized = new SerializedObject(asset);
            serialized.Update();
            serialized.FindProperty("_id").stringValue = item.Id;
            serialized.FindProperty("_displayName").stringValue = item.Name;
            serialized.FindProperty("_description").stringValue = item.Description;
            serialized.FindProperty("_rarity").intValue = item.Rarity;
            serialized.FindProperty("_maxStacks").intValue = item.MaxStacks;
            WriteEffects(serialized.FindProperty("_unitStatEffects"), item.UnitEffects,
                new[] { "_targetTeam", "_applyType", "_allyClass", "_allyType", "_enemyClass", "_enemyType", "_statType", "_modifierType" });
            WriteEffects(serialized.FindProperty("_currencyEffects"), item.CurrencyEffects,
                new[] { "_currencyType", "_rewardType", "_modifierType" });
            serialized.ApplyModifiedProperties();
            if (isNew)
            {
                string fileName = Regex.Replace(item.Id, "[^a-zA-Z0-9가-힣_-]", "_");
                string assetPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + fileName + ".asset");
                AssetDatabase.CreateAsset(asset, assetPath);
                created++;
            }
            AssetDatabase.SaveAssetIfDirty(asset);
            if (catalogAssets.Add(asset))
            {
                registered.arraySize++;
                registered.GetArrayElementAtIndex(registered.arraySize - 1).objectReferenceValue = asset;
            }
        }
        catalogObject.ApplyModifiedProperties();
        // 기존 아티팩트의 등급 변경도 카탈로그 조회 목록에 반영
        EditorUtility.SetDirty(catalog);
        typeof(ArtifactCatalog).GetMethod("OnValidate",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(catalog, null);
        AssetDatabase.SaveAssetIfDirty(catalog);
        result = $"아티팩트 {data.Count - created}개 갱신, {created}개 생성 완료. 신규 아이콘은 Inspector에서 연결하세요.";
        return true;
    }

    private static void WriteEffects(SerializedProperty list, List<ArtifactExcelParser.Effect> effects, string[] fields)
    {
        list.ClearArray();
        list.arraySize = effects.Count;
        for (int i = 0; i < effects.Count; i++)
        {
            SerializedProperty effect = list.GetArrayElementAtIndex(i);
            for (int j = 0; j < fields.Length; j++)
            {
                effect.FindPropertyRelative(fields[j]).intValue = effects[i].Types[j];
            }
            effect.FindPropertyRelative("_value").floatValue = effects[i].Value;
        }
    }
}
