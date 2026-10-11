using System;
using System.Collections.Generic;
using System.Globalization;
using Units;

// 기본 시트와 선택적인 패시브 시트를 검사한 후 가져오기 데이터 반환
public static class ArtifactExcelParser
{
    public class Data
    {
        public string Id;
        public string Name;
        public string Description;
        public int Rarity;
        public int MaxStacks;
        public List<Effect> UnitEffects = new List<Effect>();
        public List<Effect> CurrencyEffects = new List<Effect>();
        public List<int> ConsumableSlotEffects = new List<int>();
        // 시트가 없으면 기존 Inspector 설정을 유지하고, 있으면 해당 목록으로 교체합니다.
        public List<PassiveEffect> PassiveEffects;
    }

    public class PassiveEffect
    {
        public string SkillPath;
        public int Row;
        // Team, ApplyType, AllyClass, AllyType, AllyTier, EnemyClass, EnemyType, EnemyFaction
        public int[] Types;
    }

    public class Effect
    {
        public int[] Types;
        public float Value;
    }

    public static bool TryParse(string path, out List<Data> data, out string error)
    {
        data = new List<Data>();
        if (!Read(path, "Artifacts", new[] { "ArtifactId", "DisplayName", "Description", "Rarity", "MaxStacks" }, out var rows, out error))
        {
            return false;
        }
        if (rows.Count == 1)
        {
            error = "Artifacts: 아티팩트를 한 개 이상 작성하세요.";
            return false;
        }
        Dictionary<string, Data> byId = new Dictionary<string, Data>();
        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            string id = Cell(row, 0);
            if (string.IsNullOrWhiteSpace(id) || byId.ContainsKey(id) || string.IsNullOrWhiteSpace(Cell(row, 1)) ||
                !TryEnum(typeof(ArtifactRarity), Cell(row, 3), out int rarity) ||
                !int.TryParse(Cell(row, 4), NumberStyles.None, CultureInfo.InvariantCulture, out int stacks) || stacks < 1)
            {
                error = $"Artifacts {row.Number}행: ID 중복·빈 ID·이름·등급·최대 중첩(1 이상)을 확인하세요.";
                return false;
            }
            Data item = new Data { Id = id, Name = Cell(row, 1), Description = Cell(row, 2), Rarity = rarity, MaxStacks = stacks };
            data.Add(item);
            byId.Add(id, item);
        }
        if (!ReadEffects(path, true, byId, out error) || !ReadEffects(path, false, byId, out error) ||
            !ReadConsumableSlotEffects(path, byId, out error) || !ReadPassiveEffects(path, byId, out error))
        {
            return false;
        }
        return true;
    }

    private static bool ReadPassiveEffects(string path, Dictionary<string, Data> byId, out string error)
    {
        const string sheet = "PassiveSkillEffects";
        string[] headers = { "ArtifactId", "PassiveSkillPath", "TargetTeam", "ApplyType", "AllyClass", "AllyType",
            "AllyTier", "EnemyClass", "EnemyType", "EnemyFaction" };
        if (!Read(path, sheet, headers, out var rows, out error))
        {
            if (error != sheet + " 시트가 없습니다.") return false;
            error = "";
            return true;
        }
        foreach (var owner in byId.Values) owner.PassiveEffects = new List<PassiveEffect>();
        Type[] types = { typeof(UnitTeam), typeof(UnitModifierApplyType), typeof(AllyUnitClass), typeof(AllyUnitType),
            typeof(AllyUnitTier), typeof(EnemyUnitClass), typeof(EnemyUnitType), typeof(EnemyUnitFaction) };
        for (int r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            if (!byId.TryGetValue(Cell(row, 0), out var owner))
            {
                error = $"{sheet}!A{row.Number}: Artifacts 시트에 없는 ID입니다.";
                return false;
            }
            string skillPath = Cell(row, 1).Replace('\\', '/');
            if (!skillPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !skillPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) || skillPath.Contains(".."))
            {
                error = $"{sheet}!B{row.Number}: Assets/로 시작하는 패시브 S.O 경로를 입력하세요.";
                return false;
            }
            var effect = new PassiveEffect { SkillPath = skillPath, Row = row.Number, Types = new int[types.Length] };
            for (int i = 0; i < types.Length; i++)
            {
                bool ally = effect.Types[0] == (int)UnitTeam.Ally;
                var apply = (UnitModifierApplyType)effect.Types[1];
                bool required = i < 2 ||
                    (i == (ally ? 2 : 5) && apply == UnitModifierApplyType.Class) ||
                    (i == (ally ? 3 : 6) && apply == UnitModifierApplyType.Type) ||
                    (ally && i == 4 && apply == UnitModifierApplyType.Tier) ||
                    (!ally && i == 7 && apply == UnitModifierApplyType.Faction);
                string value = Cell(row, i + 2);
                if (!required && value.Length == 0) continue;
                if (!TryEnum(types[i], value, out effect.Types[i]))
                {
                    error = $"{sheet} {row.Number}행 {headers[i + 2]}: enum 이름을 확인하세요.";
                    return false;
                }
            }
            var mode = (UnitModifierApplyType)effect.Types[1];
            if ((effect.Types[0] == (int)UnitTeam.Ally && mode == UnitModifierApplyType.Faction) ||
                (effect.Types[0] == (int)UnitTeam.Enemy && mode == UnitModifierApplyType.Tier))
            {
                error = $"{sheet} {row.Number}행: 아군은 Tier, 적군은 Faction을 사용하세요.";
                return false;
            }
            owner.PassiveEffects.Add(effect);
        }
        return true;
    }

    private static bool ReadConsumableSlotEffects(string path, Dictionary<string, Data> byId, out string error)
    {
        const string sheet = "ConsumableSlotEffects";
        if (!Read(path, sheet, new[] { "ArtifactId", "AdditionalSlots" }, out var rows, out error))
        {
            return false;
        }
        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (!byId.TryGetValue(Cell(row, 0), out Data owner))
            {
                error = $"{sheet}!A{row.Number}: Artifacts 시트에 없는 ID입니다.";
                return false;
            }
            // 양수·음수·0 허용. 소수는 슬롯 수로 사용하지 않음
            if (!int.TryParse(Cell(row, 1), NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out int value))
            {
                error = $"{sheet}!B{row.Number}: AdditionalSlots에 정수를 입력하세요. 1은 증가, -1은 감소입니다.";
                return false;
            }
            long stacked = (long)value * owner.MaxStacks;
            if (stacked < int.MinValue || stacked > int.MaxValue)
            {
                error = $"{sheet}!B{row.Number}: 최대 중첩 적용 시 슬롯 보정값이 정수 범위를 벗어납니다.";
                return false;
            }
            owner.ConsumableSlotEffects.Add(value);
        }
        return true;
    }

    private static bool ReadEffects(string path, bool unit, Dictionary<string, Data> byId, out string error)
    {
        string sheet = unit ? "UnitStatEffects" : "CurrencyEffects";
        string[] headers = unit
            ? new[] { "ArtifactId", "TargetTeam", "ApplyType", "AllyClass", "AllyType", "EnemyClass", "EnemyType", "StatType", "ModifierType", "Value" }
            : new[] { "ArtifactId", "CurrencyType", "RewardType", "ModifierType", "Value" };
        Type[] types = unit
            ? new[] { typeof(UnitTeam), typeof(UnitModifierApplyType), typeof(AllyUnitClass), typeof(AllyUnitType), typeof(EnemyUnitClass), typeof(EnemyUnitType), typeof(UnitStatType), typeof(UnitStatModifierType) }
            : new[] { typeof(CurrencyType), typeof(CurrencyRewardType), typeof(CurrencyModifierType) };
        if (!Read(path, sheet, headers, out var rows, out error))
        {
            return false;
        }
        for (int r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            if (!byId.TryGetValue(Cell(row, 0), out Data owner))
            {
                error = $"{sheet}!A{row.Number}: Artifacts 시트에 없는 ID입니다.";
                return false;
            }
            Effect effect = new Effect { Types = new int[types.Length] };
            for (int i = 0; i < types.Length; i++)
            {
                string value = Cell(row, i + 1);
                // 적용하지 않는 클래스·타입 칸은 비워도 기본값 사용
                bool optional = unit && i >= 2 && i <= 5;
                if (optional)
                {
                    bool ally = effect.Types[0] == (int)UnitTeam.Ally;
                    int apply = effect.Types[1];
                    bool required = (ally && i == 2 || !ally && i == 4) && apply == (int)UnitModifierApplyType.Class ||
                        (ally && i == 3 || !ally && i == 5) && apply == (int)UnitModifierApplyType.Type;
                    optional = !required;
                }
                if (optional && string.IsNullOrEmpty(value))
                {
                    continue;
                }
                if (!TryEnum(types[i], value, out effect.Types[i]))
                {
                    error = $"{sheet} {row.Number}행 {headers[i + 1]}: enum 이름을 확인하세요.";
                    return false;
                }
            }
            if (unit && effect.Types[1] != (int)UnitModifierApplyType.All &&
                effect.Types[1] != (int)UnitModifierApplyType.Class && effect.Types[1] != (int)UnitModifierApplyType.Type)
            {
                error = $"{sheet} {row.Number}행: 현재 효과 데이터는 All, Class, Type만 지원합니다.";
                return false;
            }
            if (!unit && effect.Types[0] == (int)CurrencyType.None)
            {
                error = $"{sheet} {row.Number}행: None 재화는 사용할 수 없습니다.";
                return false;
            }
            if (!float.TryParse(Cell(row, headers.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out effect.Value) ||
                float.IsNaN(effect.Value) || float.IsInfinity(effect.Value) || float.IsInfinity(effect.Value * owner.MaxStacks))
            {
                error = $"{sheet} {row.Number}행: Value에 유한한 숫자를 입력하세요. Percent 0.2는 20%입니다.";
                return false;
            }
            if (unit)
            {
                owner.UnitEffects.Add(effect);
            }
            else
            {
                owner.CurrencyEffects.Add(effect);
            }
        }
        return true;
    }

    private static bool Read(string path, string sheet, string[] headers, out List<ExcelSheetReader.Row> rows, out string error)
    {
        if (!ExcelSheetReader.TryRead(path, sheet, out rows, out error))
        {
            return false;
        }
        if (rows.Count == 0 || rows[0].Number != 1)
        {
            error = sheet + ": 첫 행에 열 이름을 작성하세요.";
            return false;
        }
        for (int i = 0; i < headers.Length; i++)
        {
            if (Cell(rows[0], i) != headers[i])
            {
                error = $"{sheet}!{(char)('A' + i)}1: {headers[i]} 열이 필요합니다.";
                return false;
            }
        }
        return true;
    }

    private static string Cell(ExcelSheetReader.Row row, int index)
    {
        return row.Cells.TryGetValue(((char)('A' + index)).ToString(), out string value) ? value : "";
    }

    private static bool TryEnum(Type type, string name, out int value)
    {
        value = 0;
        if (Array.IndexOf(Enum.GetNames(type), name) < 0)
        {
            return false;
        }
        value = Convert.ToInt32(Enum.Parse(type, name));
        return true;
    }
}
