using System.Collections.Generic;
using System.Globalization;
using UnityEditor;

// 후보 수와 일반 전투 가중치만 갱신. 보스전 신화 고정 규칙은 기존 로직 사용
public static class ArtifactRewardExcelImporter
{
    public static bool TryImport(string path, string sheetName, ArtifactRewardTable table, out string result)
    {
        result = "";
        if (EditorApplication.isPlayingOrWillChangePlaymode || table == null)
        {
            result = "Play 종료 후 대상 ArtifactRewardTable을 연결하세요.";
            return false;
        }
        if (!TryRead(path, sheetName, out int[] values, out result))
        {
            return false;
        }
        SerializedObject serialized = new SerializedObject(table);
        serialized.Update();
        serialized.FindProperty("_candidateCount").intValue = values[0];
        serialized.FindProperty("_commonWeight").intValue = values[1];
        serialized.FindProperty("_rareWeight").intValue = values[2];
        serialized.FindProperty("_legendaryWeight").intValue = values[3];
        serialized.ApplyModifiedProperties();
        AssetDatabase.SaveAssetIfDirty(table);
        result = "아티팩트 후보 수와 등급별 가중치 갱신 완료.";
        return true;
    }

    // S.O 수정 전에 시트 전체 검사. 가중치가 모두 0인 설정도 기존 S.O와 동일하게 허용
    public static bool TryRead(string path, string sheetName, out int[] values, out string error)
    {
        values = new int[4];
        if (!ExcelSheetReader.TryRead(path, sheetName, out List<ExcelSheetReader.Row> rows, out error))
        {
            return false;
        }
        if (rows.Count != 2 || rows[0].Number != 1)
        {
            error = sheetName + ": 첫 행의 열 이름과 설정 행 1개만 작성하세요.";
            return false;
        }
        string[] headers = { "CandidateCount", "CommonWeight", "RareWeight", "LegendaryWeight" };
        for (int i = 0; i < headers.Length; i++)
        {
            string column = ((char)('A' + i)).ToString();
            if (!rows[0].Cells.TryGetValue(column, out string header) || header != headers[i])
            {
                error = $"{sheetName}!{column}1: {headers[i]} 열이 필요합니다.";
                return false;
            }
            if (!rows[1].Cells.TryGetValue(column, out string value) ||
                !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out values[i]) ||
                (i == 0 && values[i] < 1))
            {
                error = $"{sheetName}!{column}{rows[1].Number}: {headers[i]}에 {(i == 0 ? 1 : 0)} 이상의 정수를 입력하세요.";
                return false;
            }
        }
        return true;
    }
}
