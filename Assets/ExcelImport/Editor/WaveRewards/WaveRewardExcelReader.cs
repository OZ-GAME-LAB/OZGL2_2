using System.Collections.Generic;
using System.Globalization;

// 공통 리더의 셀을 웨이브 보상으로 변환하고 검사
public static class WaveRewardExcelReader
{
    public struct Row
    {
        public int Quarter;
        public int Wave;
        public int Gold;
        public int Gem;
    }

    public static bool TryRead(string path, string sheetName, out List<Row> rows, out string error)
    {
        rows = new List<Row>();
        if (!ExcelSheetReader.TryRead(path, sheetName, out List<ExcelSheetReader.Row> source, out error))
        {
            return false;
        }
        string[] headers = { "Quarter", "Wave", "Gold", "Gem" };
        if (source.Count < 2 || source[0].Number != 1)
        {
            error = sheetName + ": 첫 행의 열 이름과 데이터가 필요합니다.";
            return false;
        }
        for (int i = 0; i < 4; i++)
        {
            string column = ((char)('A' + i)).ToString();
            if (!source[0].Cells.TryGetValue(column, out string header) || header != headers[i])
            {
                error = $"{sheetName}!{column}1: {headers[i]} 열이 필요합니다.";
                return false;
            }
        }
        HashSet<string> keys = new HashSet<string>();
        for (int index = 1; index < source.Count; index++)
        {
            ExcelSheetReader.Row input = source[index];
            int[] values = new int[4];
            for (int i = 0; i < 4; i++)
            {
                string column = ((char)('A' + i)).ToString();
                if (!input.Cells.TryGetValue(column, out string value) ||
                    !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out values[i]) ||
                    (i < 2 && values[i] < 1))
                {
                    error = $"{sheetName}!{column}{input.Number}: {headers[i]}에 {(i < 2 ? 1 : 0)} 이상의 정수를 입력하세요.";
                    return false;
                }
            }
            if (!keys.Add(values[0] + ":" + values[1]))
            {
                error = $"{sheetName} {input.Number}행: 분기와 웨이브 중복입니다.";
                return false;
            }
            rows.Add(new Row { Quarter = values[0], Wave = values[1], Gold = values[2], Gem = values[3] });
        }
        rows.Sort((a, b) => a.Quarter != b.Quarter ? a.Quarter.CompareTo(b.Quarter) : a.Wave.CompareTo(b.Wave));
        return true;
    }
}