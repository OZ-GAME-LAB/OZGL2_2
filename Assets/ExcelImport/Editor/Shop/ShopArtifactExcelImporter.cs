using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 전체 검증 후 등급별 목록만 교체. 교환 슬롯 수와 추첨 옵션은 유지
public static class ShopArtifactExcelImporter
{
    public static bool TryImportPrices(string path, string sheetName, ShopArtifactTable table, out string result)
    {
        return TryImport(path, sheetName, table, true, out result);
    }

    public static bool TryImportExchanges(string path, string sheetName, ShopArtifactExchangeTable table, out string result)
    {
        return TryImport(path, sheetName, table, false, out result);
    }

    private static bool TryImport(string path, string sheetName, ScriptableObject table, bool prices, out string result)
    {
        result = "";
        if (EditorApplication.isPlayingOrWillChangePlaymode || table == null)
        {
            result = "Play 종료 후 해당 종류의 상점 테이블을 연결하세요.";
            return false;
        }
        if (!ShopArtifactExcelParser.TryParse(path, sheetName, prices, out List<ShopArtifactExcelParser.Row> rows, out result))
        {
            return false;
        }
        SerializedObject serialized = new SerializedObject(table);
        serialized.Update();
        SerializedProperty settings = serialized.FindProperty("_raritySettings");
        settings.ClearArray();
        settings.arraySize = rows.Count;
        for (int i = 0; i < rows.Count; i++)
        {
            ShopArtifactExcelParser.Row row = rows[i];
            SerializedProperty item = settings.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("_rarity").intValue = (int)row.Rarity;
            item.FindPropertyRelative("_weight").intValue = row.Weight;
            if (prices)
            {
                item.FindPropertyRelative("_minPrice").intValue = row.MinPrice;
                item.FindPropertyRelative("_maxPrice").intValue = row.MaxPrice;
                item.FindPropertyRelative("_sellPrice").intValue = row.SellPrice;
            }
        }
        serialized.ApplyModifiedProperties();
        AssetDatabase.SaveAssetIfDirty(table);
        result = $"{sheetName}: {rows.Count}개 등급 설정 갱신 완료.";
        return true;
    }
}
