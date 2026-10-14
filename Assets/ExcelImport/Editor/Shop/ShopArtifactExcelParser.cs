using System;
using System.Collections.Generic;
using System.Globalization;

// 구매와 교환 시트의 공통 등급·가중치 검사. 구매 시에는 가격도 검사
public static class ShopArtifactExcelParser
{
    public struct Row
    {
        public ArtifactRarity Rarity;
        public int Weight;
        public int MinPrice;
        public int MaxPrice;
        public int SellPrice;
    }

    public static bool TryParse(string path, string sheetName, bool prices, out List<Row> rows, out string error)
    {
        rows = new List<Row>();
        if (!ExcelSheetReader.TryRead(path, sheetName, out List<ExcelSheetReader.Row> source, out error))
        {
            return false;
        }
        string[] headers = prices
            ? new[] { "Rarity", "Weight", "MinPrice", "MaxPrice", "SellPrice" }
            : new[] { "Rarity", "Weight" };
        if (source.Count < 2 || source[0].Number != 1)
        {
            error = sheetName + ": 첫 행의 열 이름과 데이터가 필요합니다.";
            return false;
        }
        for (int i = 0; i < headers.Length; i++)
        {
            string column = ((char)('A' + i)).ToString();
            if (!source[0].Cells.TryGetValue(column, out string header) || header != headers[i])
            {
                error = $"{sheetName}!{column}1: {headers[i]} 열이 필요합니다.";
                return false;
            }
        }
        HashSet<ArtifactRarity> registered = new HashSet<ArtifactRarity>();
        string[] rarityNames = Enum.GetNames(typeof(ArtifactRarity));
        for (int index = 1; index < source.Count; index++)
        {
            ExcelSheetReader.Row input = source[index];
            if (!input.Cells.TryGetValue("A", out string name) || Array.IndexOf(rarityNames, name) < 0)
            {
                error = $"{sheetName}!A{input.Number}: Common, Rare, Legendary, Mythic 중 입력하세요.";
                return false;
            }
            ArtifactRarity rarity = (ArtifactRarity)Enum.Parse(typeof(ArtifactRarity), name);
            if (!registered.Add(rarity))
            {
                error = $"{sheetName}!A{input.Number}: {name} 등급 중복입니다.";
                return false;
            }
            int[] values = new int[4];
            for (int i = 1; i < headers.Length; i++)
            {
                string column = ((char)('A' + i)).ToString();
                if (!input.Cells.TryGetValue(column, out string value) ||
                    !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out values[i - 1]))
                {
                    error = $"{sheetName}!{column}{input.Number}: {headers[i]}에 0 이상의 정수를 입력하세요.";
                    return false;
                }
            }
            if (prices && (values[2] < values[1] || values[3] > values[1]))
            {
                error = $"{sheetName} {input.Number}행: 최소 구매가 ≤ 최대 구매가, 판매가 ≤ 최소 구매가여야 합니다.";
                return false;
            }
            rows.Add(new Row { Rarity = rarity, Weight = values[0], MinPrice = values[1], MaxPrice = values[2], SellPrice = values[3] });
        }
        return true;
    }
}
