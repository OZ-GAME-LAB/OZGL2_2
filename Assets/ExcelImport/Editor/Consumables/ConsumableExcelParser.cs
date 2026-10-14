using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Units;
using Units.Effects;
using Units.Skills;

// 전체 시트를 먼저 검증합니다. 파싱 단계에서는 에셋을 변경하지 않습니다.
public static class ConsumableExcelParser
{
    public sealed class Book
    {
        public readonly Dictionary<string, Item> Items = new();
        public readonly Dictionary<string, Runtime> RuntimeEffects = new();
    }
    public sealed class Item
    {
        public string Id, Name, Description;
        public ConsumableTargetTeam Team;
        public ConsumableTargetMode Mode;
        public float Radius;
        public readonly List<Effect> Effects = new();
    }
    public sealed class Effect
    {
        public string Kind, RuntimeId;
        public int Scaling;
        public DamageType DamageType;
        public float Value;
    }
    public sealed class Runtime
    {
        public string Id;
        public EffectAlignment Alignment;
        public EffectDurationType DurationType;
        public EffectStackType StackType;
        public float Duration;
        public int MaxStacks;
        public readonly List<ActionData> Actions = new();
    }
    public sealed class ActionData
    {
        public string Kind;
        public UnitStatType Stat;
        public UnitStatModifierType Modifier;
        public UnitStatusEffectType Status;
        public float Value, Interval;
    }

    public static bool TryParse(string path, out Book book, out string error)
    {
        book = null;
        error = "";
        try
        {
            var parsed = new Book();
            foreach (var row in Read(path, "Items", "ItemId", "Name", "Description", "TargetTeam", "TargetMode", "Radius"))
            {
                var item = new Item {
                    Id = row.Required("ItemId"), Name = row.Required("Name"), Description = row.Get("Description"),
                    Team = row.Enum<ConsumableTargetTeam>("TargetTeam"), Mode = row.Enum<ConsumableTargetMode>("TargetMode"),
                    Radius = row.Number("Radius", 0, 100000)
                };
                if (!parsed.Items.TryAdd(item.Id, item)) row.Fail("ItemId가 중복됩니다: " + item.Id);
            }
            if (parsed.Items.Count == 0) throw new FormatException("Items: 아이템을 한 개 이상 입력하세요.");
            foreach (var row in Read(path, "RuntimeEffects", "EffectId", "Alignment", "DurationType", "Duration", "StackType", "MaxStacks"))
            {
                var effect = new Runtime {
                    Id = row.Required("EffectId"), Alignment = row.Enum<EffectAlignment>("Alignment"),
                    DurationType = row.Enum<EffectDurationType>("DurationType"), Duration = row.Number("Duration", 0, 100000000),
                    StackType = row.Enum<EffectStackType>("StackType"), MaxStacks = row.Integer("MaxStacks", 1)
                };
                if (effect.DurationType == EffectDurationType.Timed && effect.Duration <= 0) row.Fail("Timed의 Duration은 0보다 커야 합니다.");
                if (!parsed.RuntimeEffects.TryAdd(effect.Id, effect)) row.Fail("EffectId가 중복됩니다: " + effect.Id);
            }
            foreach (var row in Read(path, "RuntimeActions", "EffectId", "Kind", "Stat", "Modifier", "Value", "Interval", "Status"))
            {
                if (!parsed.RuntimeEffects.TryGetValue(row.Required("EffectId"), out var runtime)) row.Fail("RuntimeEffects에 없는 EffectId입니다.");
                var action = new ActionData { Kind = row.Required("Kind") };
                switch (action.Kind)
                {
                    case "Stat":
                        row.Empty("Interval", "Status");
                        action.Stat = row.Enum<UnitStatType>("Stat");
                        action.Modifier = row.Enum<UnitStatModifierType>("Modifier");
                        action.Value = action.Modifier == UnitStatModifierType.Percent ? row.Number("Value", -1, 10) : row.Number("Value", -100000000, 100000000);
                        break;
                    case "PeriodicHeal":
                    case "PeriodicDamage":
                        row.Empty("Stat", "Modifier", "Status");
                        action.Interval = row.Number("Interval", .01f, 100000000);
                        action.Value = row.Number("Value", 0, action.Kind == "PeriodicHeal" ? 10 : 100000000);
                        if (action.Value == 0) row.Fail("주기 효과 Value는 0보다 커야 합니다.");
                        if (runtime.Actions.Any(a => a.Kind == "PeriodicHeal" || a.Kind == "PeriodicDamage"))
                            row.Fail("현재 RuntimeEffectManager는 효과 하나당 주기 Action 하나만 처리합니다. EffectId를 분리하세요.");
                        break;
                    case "Status":
                        row.Empty("Stat", "Modifier", "Value", "Interval");
                        action.Status = row.Enum<UnitStatusEffectType>("Status");
                        break;
                    default: row.Fail("Kind는 Stat / PeriodicHeal / PeriodicDamage / Status 중 하나입니다."); break;
                }
                runtime.Actions.Add(action);
            }
            foreach (var row in Read(path, "ItemEffects", "ItemId", "Kind", "Value", "ScalingStat", "DamageType", "EffectId"))
            {
                if (!parsed.Items.TryGetValue(row.Required("ItemId"), out var item)) row.Fail("Items에 없는 ItemId입니다.");
                var effect = new Effect { Kind = row.Required("Kind") };
                switch (effect.Kind)
                {
                    case "Damage":
                        row.Empty("ScalingStat", "EffectId");
                        effect.DamageType = row.Enum<DamageType>("DamageType");
                        break;
                    case "Heal":
                        row.Empty("DamageType", "EffectId");
                        effect.Scaling = (int)row.Enum<HealScalingStatType>("ScalingStat");
                        break;
                    case "Shield":
                        row.Empty("DamageType", "EffectId");
                        effect.Scaling = (int)row.Enum<ShieldScalingStatType>("ScalingStat");
                        break;
                    case "Runtime":
                        row.Empty("Value", "ScalingStat", "DamageType");
                        effect.RuntimeId = row.Required("EffectId");
                        if (!parsed.RuntimeEffects.ContainsKey(effect.RuntimeId)) row.Fail("RuntimeEffects에 없는 EffectId입니다.");
                        break;
                    default: row.Fail("Kind는 Damage / Heal / Shield / Runtime 중 하나입니다."); break;
                }
                if (effect.Kind != "Runtime")
                {
                    effect.Value = row.Number("Value", 0, 100000000);
                    if (effect.Value == 0) row.Fail("Value는 0보다 커야 합니다.");
                }
                item.Effects.Add(effect);
            }
            foreach (var item in parsed.Items.Values)
                if (item.Effects.Count == 0) throw new FormatException("ItemEffects: 효과가 없는 아이템 " + item.Id);
            foreach (var effect in parsed.RuntimeEffects.Values)
                if (effect.Actions.Count == 0) throw new FormatException("RuntimeActions: Action이 없는 효과 " + effect.Id);
            book = parsed;
            return true;
        }
        catch (FormatException exception) { error = exception.Message; return false; }
    }

    private static List<Row> Read(string path, string sheet, params string[] headers)
    {
        if (!ExcelSheetReader.TryRead(path, sheet, out var source, out string error)) throw new FormatException(error);
        if (source.Count == 0) throw new FormatException(sheet + ": 헤더가 없습니다.");
        var columns = new Dictionary<string, string>();
        foreach (var cell in source[0].Cells)
        {
            if (string.IsNullOrWhiteSpace(cell.Value)) continue;
            if (!headers.Contains(cell.Value) || columns.ContainsKey(cell.Value)) throw new FormatException(sheet + ": 알 수 없거나 중복된 헤더 " + cell.Value);
            columns.Add(cell.Value, cell.Key);
        }
        foreach (string header in headers)
            if (!columns.ContainsKey(header)) throw new FormatException(sheet + ": 헤더 누락 " + header);
        var rows = new List<Row>();
        foreach (var row in source.Skip(1))
        {
            if (row.Cells.Any(cell => !string.IsNullOrWhiteSpace(cell.Value) && !columns.ContainsValue(cell.Key)))
                throw new FormatException($"{sheet} {row.Number}행: 헤더 없는 열에 값이 있습니다.");
            rows.Add(new Row(sheet, row, columns));
        }
        return rows;
    }

    private sealed class Row
    {
        private readonly string _sheet;
        private readonly ExcelSheetReader.Row _row;
        private readonly Dictionary<string, string> _columns;
        public Row(string sheet, ExcelSheetReader.Row row, Dictionary<string, string> columns) { _sheet = sheet; _row = row; _columns = columns; }
        public void Fail(string message) => throw new FormatException($"{_sheet} {_row.Number}행: {message}");
        public string Get(string key) => _row.Cells.TryGetValue(_columns[key], out var value) ? value : "";
        public string Required(string key) { string value = Get(key); if (value.Length == 0) Fail(key + " 값을 입력하세요."); return value; }
        public void Empty(params string[] keys) { foreach (string key in keys) if (Get(key).Length != 0) Fail(key + "는 이 Kind에서 사용하지 않으므로 비워주세요."); }
        public T Enum<T>(string key) where T : struct, System.Enum
        {
            string value = Required(key);
            if (!System.Enum.GetNames(typeof(T)).Contains(value)) Fail(key + ": enum 이름을 확인하세요. " + value);
            return (T)System.Enum.Parse(typeof(T), value);
        }
        public float Number(string key, float min, float max)
        {
            if (!float.TryParse(Required(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
                Fail($"{key}: {min} ~ {max} 범위의 숫자를 입력하세요.");
            return value;
        }
        public int Integer(string key, int min)
        {
            if (!int.TryParse(Required(key), NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < min) Fail(key + ": 양의 정수를 입력하세요.");
            return value;
        }
    }
}
