using System.Collections.Generic;
using System.IO;
using UnityEditor;

// 수동 버튼과 자동 갱신에서 공통으로 사용하는 가져오기
public static class WaveRewardExcelImporter
{
    public static bool TryImport(string path, string sheetName, WaveRewardTable table, CurrencyCatalog catalog, out string result)
    {
        result = "";
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            result = "Play 종료 후 가져오세요.";
            return false;
        }
        if (table == null || catalog == null || !File.Exists(path))
        {
            result = "엑셀 파일, 테이블, 카탈로그를 연결하세요.";
            return false;
        }
        if (!TryGetCurrencies(catalog, out CurrencyData gold, out CurrencyData gem, out result))
        {
            return false;
        }
        if (!WaveRewardExcelReader.TryRead(path, sheetName, out List<WaveRewardExcelReader.Row> rows, out result))
        {
            return false;
        }

        SerializedObject serialized = new SerializedObject(table);
        serialized.Update();
        SerializedProperty quarters = serialized.FindProperty("_quarters");
        quarters.ClearArray();
        int lastQuarter = -1;
        SerializedProperty waves = null;
        foreach (WaveRewardExcelReader.Row row in rows)
        {
            if (row.Quarter != lastQuarter)
            {
                quarters.arraySize++;
                SerializedProperty quarter = quarters.GetArrayElementAtIndex(quarters.arraySize - 1);
                quarter.FindPropertyRelative("_quarterNumber").intValue = row.Quarter;
                waves = quarter.FindPropertyRelative("_waves");
                waves.ClearArray();
                lastQuarter = row.Quarter;
            }
            waves.arraySize++;
            SerializedProperty wave = waves.GetArrayElementAtIndex(waves.arraySize - 1);
            wave.FindPropertyRelative("_waveNumber").intValue = row.Wave;
            SerializedProperty rewards = wave.FindPropertyRelative("_rewards");
            rewards.ClearArray();
            AddReward(rewards, gold, row.Gold);
            AddReward(rewards, gem, row.Gem);
        }
        serialized.ApplyModifiedProperties();
        AssetDatabase.SaveAssetIfDirty(table);
        result = $"{rows.Count}개 웨이브 갱신 완료. 기존 에셋 참조는 유지됩니다.";
        return true;
    }

    private static bool TryGetCurrencies(CurrencyCatalog catalog, out CurrencyData gold, out CurrencyData gem, out string result)
    {
        result = "";
        gold = null;
        gem = null;
        HashSet<CurrencyType> types = new HashSet<CurrencyType>();
        HashSet<string> ids = new HashSet<string>();
        if (catalog.Currencies == null)
        {
            result = "카탈로그 재화 목록이 없습니다.";
            return false;
        }
        foreach (CurrencyData currency in catalog.Currencies)
        {
            if (currency == null || string.IsNullOrWhiteSpace(currency.Id) ||
                !types.Add(currency.Type) || !ids.Add(currency.Id))
            {
                result = "카탈로그의 빈 항목, ID 및 타입 중복을 확인하세요.";
                return false;
            }
            if (currency.Type == CurrencyType.Gold)
            {
                gold = currency;
            }
            if (currency.Type == CurrencyType.Gem)
            {
                gem = currency;
            }
        }
        if (gold == null || gem == null || gold.Lifetime != CurrencyLifetime.Run || gem.Lifetime != CurrencyLifetime.Run)
        {
            result = "카탈로그에 Run 재화인 Gold와 Gem이 필요합니다.";
            return false;
        }
        return true;
    }

    private static void AddReward(SerializedProperty rewards, CurrencyData currency, int amount)
    {
        if (amount == 0)
        {
            return;
        }
        rewards.arraySize++;
        SerializedProperty reward = rewards.GetArrayElementAtIndex(rewards.arraySize - 1);
        reward.FindPropertyRelative("_currency").objectReferenceValue = currency;
        reward.FindPropertyRelative("_amount").intValue = amount;
    }
}
