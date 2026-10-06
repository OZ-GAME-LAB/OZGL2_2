using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Units;
using Units.Skills;
using Units.Effects;

// 아티팩트 전용 패시브/지속 효과 시트. 모든 행을 임시 객체로 검증한 뒤 기존 GUID를 유지해 저장합니다.
public sealed class ArtifactSkillExcelImport : IDisposable
{
    private readonly Dictionary<string, ScriptableObject> drafts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ScriptableObject> targets = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> effectIds = new(StringComparer.Ordinal);
    public int Count => drafts.Count;

    public static bool TryPrepare(string file, out ArtifactSkillExcelImport plan, out string error)
    {
        plan = new ArtifactSkillExcelImport();
        error = "";
        try { plan.Read(file); return true; }
        catch (Exception ex) { error = ex.Message; plan.Dispose(); plan = null; return false; }
    }

    public PassiveSkillData FindSkill(string path) => targets.TryGetValue(path, out var value)
        ? value as PassiveSkillData : AssetDatabase.LoadAssetAtPath<PassiveSkillData>(path);

    private void Read(string file)
    {
        // 기존 엑셀 호환: 새 시트가 모두 없으면 Inspector 설정을 그대로 사용합니다.
        string[] names = { "PassiveSkills", "PassiveConditions", "PassiveActions", "RuntimeEffects", "RuntimeActions" };
        int present = 0;
        foreach (string name in names)
        {
            if (ExcelSheetReader.TryRead(file, name, out _, out var error)) present++;
            else if (error != name + " 시트가 없습니다.") throw new FormatException(error);
        }
        if (present == 0) return;
        if (present != names.Length) throw new FormatException("패시브 편집 시 PassiveSkills, PassiveConditions, PassiveActions, RuntimeEffects, RuntimeActions 시트가 모두 필요합니다. 빈 목록은 헤더만 유지하세요.");

        foreach (var r in Rows(file, "RuntimeEffects", "EffectPath,EffectId,Alignment,DurationType,Duration,StackType,MaxStacks,Categories"))
        {
            var effect = New<EffectData>(r, "EffectPath");
            string id = r.Required("EffectId");
            if (!effectIds.Add(id)) r.Fail("EffectId 중복: " + id);
            Set(effect, "_effectId", id);
            r.Enum(effect, "_alignment", "Alignment"); r.Enum(effect, "_durationType", "DurationType");
            Set(effect, "_duration", r.Number("Duration", 0));
            r.Enum(effect, "_stackType", "StackType"); Set(effect, "_maxStack", r.Integer("MaxStacks", 1));
            Set(effect, "_categories", r.Get("Categories").Split(';').Select(x => x.Trim()).Where(x => x.Length > 0).Distinct().ToList());
            if (effect.DurationType == EffectDurationType.Timed && effect.Duration <= 0) r.Fail("Timed의 Duration은 0보다 커야 합니다.");
            r.Finish();
        }
        foreach (var r in Rows(file, "RuntimeActions", "EffectPath,Kind,Stat,Modifier,Value,Interval,Status"))
        {
            var effect = Owner<EffectData>(r, "EffectPath");
            EffectActionData action;
            switch (r.Required("Kind"))
            {
                case "Stat": action = new StatEffectActionData(); Stat(r, action); break;
                case "Status": action = new StatusEffectActionData(); r.Enum(action, "_statusType", "Status"); break;
                case "PeriodicHeal":
                    action = new PeriodicHealEffectActionData();
                    Set(action, "_healRatio", r.Number("Value", .000001f, 10));
                    Set(action, "_interval", r.Number("Interval", .01f)); break;
                case "PeriodicDamage":
                    action = new PeriodicDamageEffectActionData();
                    Set(action, "_damage", r.Number("Value", .000001f));
                    Set(action, "_interval", r.Number("Interval", .01f)); break;
                default: throw r.Error("Kind: Stat / Status / PeriodicHeal / PeriodicDamage 중 하나를 입력하세요.");
            }
            var actions = List<EffectActionData>(effect, "_actions");
            if ((action is PeriodicHealEffectActionData || action is PeriodicDamageEffectActionData) &&
                actions.Any(x => x is PeriodicHealEffectActionData || x is PeriodicDamageEffectActionData))
                r.Fail("하나의 지속 효과에는 주기 Action을 하나만 등록하세요.");
            actions.Add(action); r.Finish();
        }
        foreach (var r in Rows(file, "PassiveSkills", "SkillPath,Trigger,Mode,TickInterval,ConditionInterval"))
        {
            var skill = New<PassiveSkillData>(r, "SkillPath");
            r.Enum(skill, "_triggerType", "Trigger"); r.Enum(skill, "_effectMode", "Mode");
            Set(skill, "_tickInterval", r.Number("TickInterval", .1f));
            Set(skill, "_conditionCheckInterval", r.Number("ConditionInterval", .05f)); r.Finish();
        }
        foreach (var r in Rows(file, "PassiveConditions", "SkillPath,Kind,Subject,Comparison,Value,Relation,Radius,Status,ShouldExist"))
        {
            var skill = Owner<PassiveSkillData>(r, "SkillPath");
            PassiveSkillConditionData condition;
            switch (r.Required("Kind"))
            {
                case "Health":
                    condition = new PassiveHealthConditionData();
                    r.Enum(condition, "_subjectType", "Subject"); r.Enum(condition, "_comparisonType", "Comparison");
                    Set(condition, "_healthRatio", r.Number("Value", 0, 1)); break;
                case "UnitCount":
                    condition = new PassiveUnitCountConditionData();
                    r.Enum(condition, "_targetRelation", "Relation"); r.Enum(condition, "_comparisonType", "Comparison");
                    Set(condition, "_range", r.Number("Radius", .000001f));
                    Set(condition, "_count", r.Integer("Value", 0)); break;
                case "Status":
                    condition = new PassiveRuntimeEffectConditionData();
                    SetEnum(condition, "_conditionType", "Status"); r.Enum(condition, "_subjectType", "Subject");
                    r.Enum(condition, "_statusType", "Status");
                    if (!bool.TryParse(r.Required("ShouldExist"), out bool exists)) r.Fail("ShouldExist는 True 또는 False입니다.");
                    Set(condition, "_shouldExist", exists); break;
                default: throw r.Error("Kind: Health / UnitCount / Status 중 하나를 입력하세요.");
            }
            if ((condition is PassiveHealthConditionData || condition is PassiveUnitCountConditionData) &&
                Field(condition, "_comparisonType").GetValue(condition).ToString() == "NotEqual")
                r.Fail("현재 유닛 조건 평가는 NotEqual을 지원하지 않습니다.");
            List<PassiveSkillConditionData>(skill, "_conditions").Add(condition); r.Finish();
        }
        foreach (var r in Rows(file, "PassiveActions", "SkillPath,Kind,Stat,Modifier,Value,Target,Relation,Radius,MaxTargets,Scaling,RuntimeEffectPath,Delivery,DamageType,ProjectileSpeed"))
        {
            var skill = Owner<PassiveSkillData>(r, "SkillPath");
            string kind = r.Required("Kind");
            if (kind == "Stat" && skill.EffectMode != PassiveSkillEffectMode.WhileCondition)
                r.Fail("Stat Action은 WhileCondition에서 사용하세요. 시간 제한 버프는 Runtime으로 등록하세요.");
            if (skill.EffectMode == PassiveSkillEffectMode.WhileCondition && kind != "Stat" && kind != "DamageModifier")
                r.Fail("회복·보호막·지속 효과·추가 공격은 Trigger 또는 Once에서 사용하세요.");
            PassiveSkillActionData action;
            if (kind == "Stat") { action = new PassiveStatModifierActionData(); Stat(r, action); }
            else if (kind == "DamageModifier")
            {
                action = new PassiveDamageValueModifierActionData();
                r.Enum(action, "_ownerType", "Target"); Modifier(r, action);
            }
            else if (kind == "AdditionalAttack") action = Attack(r);
            else
            {
                var effectAction = new PassiveEffectActionData();
                r.Enum(effectAction, "_targetType", "Target"); r.Enum(effectAction, "_targetRelation", "Relation");
                Set(effectAction, "_areaRadius", r.Number("Radius", 0));
                Set(effectAction, "_maxEffectTargetCount", r.Integer("MaxTargets", 1));
                SetEnum(effectAction, "_areaType", effectAction.TargetType == PassiveSkillTargetType.Search ? "Circle" : "Single");
                Set(effectAction, "_areaAngle", 360f);
                SkillEffectData value;
                switch (kind)
                {
                    case "Heal": value = new SkillHealEffectData(); r.Enum(value, "_scalingStatType", "Scaling"); Set(value, "_healRatio", r.Number("Value", .000001f, 10)); break;
                    case "Shield": value = new SkillShieldEffectData(); r.Enum(value, "_scalingStatType", "Scaling"); Set(value, "_shieldRatio", r.Number("Value", .000001f, 10)); break;
                    case "Runtime":
                        value = new SkillRuntimeEffectData();
                        string path = r.Required("RuntimeEffectPath");
                        var runtime = targets.TryGetValue(path, out var target) ? target as EffectData : AssetDatabase.LoadAssetAtPath<EffectData>(path);
                        if (runtime == null) r.Fail("RuntimeEffectPath에 정의 또는 기존 EffectData가 없습니다: " + path);
                        Set(value, "_effectData", runtime); break;
                    default: throw r.Error("Kind: Stat / DamageModifier / AdditionalAttack / Heal / Shield / Runtime 중 하나를 입력하세요.");
                }
                List<SkillEffectData>(effectAction, "_effects").Add(value); action = effectAction;
            }
            List<PassiveSkillActionData>(skill, "_actions").Add(action); r.Finish();
        }
        foreach (var pair in drafts)
        {
            if (pair.Value is PassiveSkillData p && p.Actions.Count == 0 || pair.Value is EffectData e && e.Actions.Count == 0)
                throw new FormatException(pair.Key + ": Action을 한 개 이상 등록하세요.");
        }
        // 이름이 같은 효과를 별도 경로에 만들어 스택 판정이 충돌하지 않도록 방지합니다.
        foreach (var guid in AssetDatabase.FindAssets("t:EffectData", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (drafts.ContainsKey(path)) continue;
            var other = AssetDatabase.LoadAssetAtPath<EffectData>(path);
            if (other != null && effectIds.Contains(other.EffectId)) throw new FormatException("기존 EffectId 중복: " + other.EffectId + " (" + path + ")");
        }
    }

    private static PassiveSkillActionData Attack(Row r)
    {
        var action = new PassiveAdditionalAttackActionData();
        var attack = action.Attack;
        var target = new SkillTargetSettings(SkillTargetSource.Search, SkillTargetRelation.Hostile, false,
            r.Number("Radius", .000001f), r.Integer("MaxTargets", 1), 1f);
        Set(target, "_priorities", new List<SkillTargetPriority> { new(SkillTargetPolicy.Nearest) });
        Set(attack, "_target", target);
        r.Enum(attack, "_delivery", "Delivery");
        if (attack.Delivery != ActiveSkillDeliveryType.Direct && attack.Delivery != ActiveSkillDeliveryType.Projectile)
            r.Fail("AdditionalAttack Delivery는 Direct 또는 Projectile만 지원합니다.");
        Set(attack, "_projectileSpeed", r.Number("ProjectileSpeed", .000001f));
        var damage = new SkillDamageEffectData(); r.Enum(damage, "_damageType", "DamageType");
        Set(damage, "_damageMultiplier", r.Number("Value", .000001f, 100));
        List<SkillEffectEntry>(attack, "_baseEffects").Add(new SkillEffectEntry(1, SkillEffectTiming.OnHit, new[] { damage }));
        return action;
    }

    private static void Stat(Row r, object value) { r.Enum(value, "_statType", "Stat"); Modifier(r, value); }
    private static void Modifier(Row r, object value)
    {
        r.Enum(value, "_modifierType", "Modifier");
        bool percent = Field(value, "_modifierType").GetValue(value).ToString() == "Percent";
        Set(value, "_value", r.Number("Value", percent ? -1 : -100000000, percent ? 10 : 100000000));
    }

    private T New<T>(Row r, string column) where T : ScriptableObject
    {
        string path = r.Required(column);
        if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".asset", StringComparison.Ordinal) ||
            path.Contains("..") || path.Contains('\\') || !AssetDatabase.IsValidFolder(Path.GetDirectoryName(path).Replace('\\', '/')))
            r.Fail(column + ": Assets 하위 기존 폴더의 .asset 경로를 입력하세요.");
        if (drafts.ContainsKey(path)) r.Fail("S.O 경로 중복: " + path);
        var existing = AssetDatabase.LoadMainAssetAtPath(path);
        if ((existing != null && existing.GetType() != typeof(T)) || (existing == null && File.Exists(path))) r.Fail("다른 종류의 에셋이 존재합니다: " + path);
        var draft = ScriptableObject.CreateInstance<T>(); draft.name = Path.GetFileNameWithoutExtension(path);
        Set(draft, "_skillSchemaVersion", 1);
        drafts.Add(path, draft); targets.Add(path, existing as T ?? draft); return draft;
    }
    private T Owner<T>(Row r, string column) where T : ScriptableObject
    {
        string path = r.Required(column);
        if (!drafts.TryGetValue(path, out var value) || !(value is T)) throw r.Error("정의 시트에 없는 경로: " + path);
        return (T)value;
    }

    // 검증 후 호출. 실패하면 이 단계에서 수정한 기존 객체와 생성한 파일을 되돌립니다.
    public void Commit()
    {
        var backups = new Dictionary<ScriptableObject, ScriptableObject>();
        var created = new List<string>();
        try
        {
            foreach (var pair in drafts.OrderBy(x => x.Value is EffectData ? 0 : 1))
            {
                var target = targets[pair.Key];
                if (target == pair.Value) { AssetDatabase.CreateAsset(target, pair.Key); created.Add(pair.Key); }
                else { backups.Add(target, UnityEngine.Object.Instantiate(target)); EditorUtility.CopySerialized(pair.Value, target); }
                EditorUtility.SetDirty(target); AssetDatabase.SaveAssetIfDirty(target);
            }
        }
        catch
        {
            foreach (var backup in backups) { EditorUtility.CopySerialized(backup.Value, backup.Key); EditorUtility.SetDirty(backup.Key); AssetDatabase.SaveAssetIfDirty(backup.Key); }
            foreach (string path in created) AssetDatabase.DeleteAsset(path);
            throw;
        }
        finally { foreach (var backup in backups.Values) UnityEngine.Object.DestroyImmediate(backup); }
    }
    public void Dispose()
    {
        foreach (var draft in drafts.Values) if (draft != null && !EditorUtility.IsPersistent(draft)) UnityEngine.Object.DestroyImmediate(draft);
    }

    // 에디터 전용: 공개 setter가 없는 데이터 클래스의 직렬화 필드만 지정합니다.
    private static FieldInfo Field(object value, string name)
    {
        for (Type type = value.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }
        throw new InvalidOperationException(value.GetType().Name + ": 직렬화 필드가 없습니다: " + name);
    }
    private static void Set(object value, string name, object data) => Field(value, name).SetValue(value, data);
    private static List<T> List<T>(object value, string name) => (List<T>)Field(value, name).GetValue(value);
    private static void SetEnum(object value, string field, string name)
    {
        Type type = Field(value, field).FieldType;
        if (!type.IsEnum || !System.Enum.GetNames(type).Contains(name)) throw new FormatException(type.Name + ": 잘못된 enum 이름 " + name);
        Set(value, field, System.Enum.Parse(type, name));
    }
    private static IEnumerable<Row> Rows(string file, string sheet, string header)
    {
        if (!ExcelSheetReader.TryRead(file, sheet, out var rows, out var error)) throw new FormatException(error);
        string[] columns = header.Split(',');
        if (rows.Count == 0 || rows[0].Number != 1) throw new FormatException(sheet + ": 첫 행에 헤더가 필요합니다.");
        for (int i = 0; i < columns.Length; i++)
            if (!rows[0].Cells.TryGetValue(((char)('A' + i)).ToString(), out string name) || name != columns[i])
                throw new FormatException(sheet + ": 헤더 순서가 필요합니다: " + header);
        return rows.Skip(1).Select(r => new Row(sheet, r, columns));
    }
    private sealed class Row
    {
        private readonly string sheet;
        private readonly ExcelSheetReader.Row row;
        private readonly Dictionary<string, string> values = new();
        private readonly HashSet<string> used = new();
        public Row(string sheet, ExcelSheetReader.Row row, string[] columns)
        {
            this.sheet = sheet; this.row = row;
            for (int i = 0; i < columns.Length; i++) values[columns[i]] = row.Cells.TryGetValue(((char)('A' + i)).ToString(), out var v) ? v.Trim() : "";
            if (row.Cells.Keys.Any(x => x.Length != 1 || x[0] >= 'A' + columns.Length)) Fail("정의되지 않은 열에 값이 있습니다.");
        }
        public FormatException Error(string message) => new(sheet + " " + row.Number + "행: " + message);
        public void Fail(string message) => throw Error(message);
        public string Get(string key) { used.Add(key); return values[key]; }
        public string Required(string key) { string v = Get(key); if (v.Length == 0) Fail(key + " 값이 필요합니다."); return v; }
        public float Number(string key, float min, float max = 100000000)
        {
            if (!float.TryParse(Required(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float n) || float.IsNaN(n) || float.IsInfinity(n) || n < min || n > max) Fail(key + $": {min}~{max} 범위의 숫자를 입력하세요.");
            return n;
        }
        public int Integer(string key, int min)
        {
            if (!int.TryParse(Required(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) || n < min) Fail(key + ": 정수 " + min + " 이상이 필요합니다.");
            return n;
        }
        public void Enum(object value, string field, string column)
        {
            try { SetEnum(value, field, Required(column)); } catch (FormatException e) { throw Error(column + ": " + e.Message); }
        }
        public void Finish() { foreach (var p in values) if (p.Value.Length > 0 && !used.Contains(p.Key)) Fail(p.Key + ": 이 Kind에서 사용하지 않는 값입니다. 비워주세요."); }
    }
}
