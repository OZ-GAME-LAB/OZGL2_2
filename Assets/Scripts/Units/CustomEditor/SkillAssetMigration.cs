#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Units.Skills;
using Units.Effects;
using UnityEditor;
using UnityEngine;

// 검증과 백업을 거쳐 스킬 스키마를 이전하고 항목별 결과를 남긴다.
namespace Units.Editor
{
    // 원본 백업을 만든 뒤 검증된 에셋만 이전한다. 실패 항목과 두 번째 실행 결과를 숨기지 않는다.
    public static class SkillAssetMigration
    {

        [Serializable]
        public sealed class Entry
        {

            // ============================================================
            // Data / Runtime State
            // ============================================================

            public string path;

            public string status;

            public List<string> errors = new();
        }

        [Serializable]
        public sealed class Report
        {

            // ============================================================
            // Data / Runtime State
            // ============================================================

            public bool applied;

            public string backupDirectory;

            public List<Entry> entries = new();
        }

        [MenuItem("Tools/Units/Skills/G7 Dry Run")]
        public static void DryRunMenu() => WriteReport(Run(false));

        [MenuItem("Tools/Units/Skills/G7 Apply Migration")]
        public static void ApplyMenu() => WriteReport(Run(true));

        public static Report Run(
            bool apply,
            string[] explicitPaths = null)
        {
            var paths = explicitPaths ?? AssetDatabase.FindAssets("t:ActiveSkillData t:PassiveSkillData t:EffectData").Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(value => value).ToArray();

            var duplicateIds = AssetDatabase.FindAssets("t:EffectData").Select(guid => AssetDatabase.LoadAssetAtPath<EffectData>(AssetDatabase.GUIDToAssetPath(guid))).Where(data => data != null && !string.IsNullOrWhiteSpace(data.EffectId)).GroupBy(data => data.EffectId).Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();

            var report = new Report
            {
                applied = apply
            };

            if (apply)
                report.backupDirectory = Path.GetFullPath(Path.Combine("SkillMigrationBackups", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")));

            int undoGroup = -1;

            if (apply)
            {
                Undo.IncrementCurrentGroup();

                undoGroup = Undo.GetCurrentGroup();

                Undo.SetCurrentGroupName("G7 Skill Schema Migration");
            }

            foreach (string path in paths)
            {
                var item = new Entry
                {
                    path = path
                };

                report.entries.Add(item);

                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

                try
                {
                    Validate(asset, item.errors, duplicateIds);
                }
                catch (Exception error)
                {
                    item.errors.Add("직렬화 데이터 검증 실패: " + error.Message);
                }

                if (item.errors.Count > 0)
                {
                    item.status = "Failed";

                    continue;
                }

                int version = Version(asset);

                if (version == 1)
                {
                    item.status = "Unchanged";

                    continue;
                }

                if (!apply)
                {
                    item.status = "WouldMigrate";

                    continue;
                }

                try
                {
                    string backup = Path.Combine(report.backupDirectory, path);

                    Directory.CreateDirectory(Path.GetDirectoryName(backup));

                    File.Copy(
                        path,
                        backup,
                        false
                    );

                    if (File.Exists(path + ".meta"))
                        File.Copy(
                            path + ".meta",
                            backup + ".meta",
                            false
                        );

                    Undo.RecordObject(asset, "Migrate Skill Schema");

                    if (asset is ActiveSkillData active)
                        active.UpgradeSkillSchema();

                    else if (asset is PassiveSkillData passive)
                        passive.UpgradeSkillSchema();

                    else if (asset is EffectData effect)
                        effect.UpgradeSkillSchema();

                    EditorUtility.SetDirty(asset);

                    AssetDatabase.SaveAssetIfDirty(asset);

                    item.status = "Migrated";
                }
                catch (Exception error)
                {
                    item.status = "Failed";

                    item.errors.Add(error.Message);

                    // 원본 파일을 복구하고 해당 에셋만 다시 읽는다.
                    string backup = Path.Combine(report.backupDirectory, path);

                    if (File.Exists(backup))
                    {
                        File.Copy(
                            backup,
                            path,
                            true
                        );

                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    }
                }
            }

            if (apply)
                Undo.CollapseUndoOperations(undoGroup);

            return report;
        }

        private static int Version(ScriptableObject asset) => asset switch
        {
            ActiveSkillData active => active.SkillSchemaVersion,
            PassiveSkillData passive => passive.SkillSchemaVersion,
            EffectData effect => effect.SkillSchemaVersion,
            _ => -1
        };

        // ============================================================
        // Validation
        // ============================================================

        public static void Validate(
            ScriptableObject asset,
            List<string> errors,
            HashSet<string> duplicateIds = null)
        {
            if (asset == null || Version(asset) < 0)
            {
                errors.Add("지원하는 스킬/효과 에셋이 아닙니다.");

                return;
            }

            if (Version(asset) > 1)
                errors.Add("현재 도구보다 높은 스키마 버전입니다.");

            if (SerializationUtility.HasManagedReferencesWithMissingTypes(asset))
                errors.Add("복원할 수 없는 SerializeReference 타입이 있습니다.");

            ValidateSerializedValues(asset, errors, duplicateIds);

            if (asset is ActiveSkillData active)
            {
                if (active.Actions.Count == 0)
                {
                    if (!Enum.IsDefined(typeof(ActiveSkillActionType), active.ActionType))
                        errors.Add("_actionType: 알 수 없는 레거시 값");

                    if (active.SkillFXType != ActiveSkillFXType.None || active.HitFXType != ActiveSkillFXType.None)
                        errors.Add("_skillFXType/_hitFXType: 자동으로 매핑할 수 없는 FX 값");
                }

                if (!Finite(active.SkillRange) || active.SkillRange < 0 || !Finite(active.SkillCooldown) || active.SkillCooldown < 0)
                    errors.Add("_skillRange/_skillCooldown: 유효하지 않은 값");

                var actions = SkillActionPlan.Create(active);

                if (actions.Count == 0)
                    errors.Add("_actions: 비어 있는 실행 계획");

                for (int i = 0; i < actions.Count; i++)
                    ValidateAction(
                        actions[i],
                        "_actions[" + i + "]",
                        errors
                    );
            }
            else if (asset is PassiveSkillData passive)
            {
                if (!Enum.IsDefined(typeof(PassiveSkillTriggerType), passive.TriggerType))
                    errors.Add("_triggerType: 알 수 없는 값");

                if (!Enum.IsDefined(typeof(PassiveSkillEffectMode), passive.EffectMode))
                    errors.Add("_effectMode: 알 수 없는 값");

                for (int i = 0; i < passive.Actions.Count; i++)
                {
                    if (passive.Actions[i] == null)
                        errors.Add("_actions[" + i + "]: 빈 액션");

                    else if (passive.Actions[i] is PassiveAdditionalAttackActionData extra)
                        ValidateAction(
                            extra.Attack,
                            "_actions[" + i + "]._attack",
                            errors
                        );

                    else if (passive.Actions[i] is PassiveEffectActionData effect)
                        ValidateEffects(
                            effect.Effects,
                            "_actions[" + i + "]._effects",
                            errors
                        );
                }
            }
            else if (asset is EffectData effect)
            {
                if (string.IsNullOrWhiteSpace(effect.EffectId))
                    errors.Add("_effectId: 빈 ID");

                else if (duplicateIds != null && duplicateIds.Contains(effect.EffectId))
                    errors.Add("_effectId: 중복 ID " + effect.EffectId);

                if (!Finite(effect.Duration) || effect.Duration < 0 || effect.MaxStack < 1)
                    errors.Add("_duration/_maxStack: 유효하지 않은 값");

                if (effect.Actions.Any(action => action == null))
                    errors.Add("_actions: 빈 효과 액션");
            }
        }

        public static void ValidateAction(
            SkillActionData action,
            string path,
            List<string> errors)
        {
            if (action == null || !action.IsConfigured)
            {
                errors.Add(path + ": 유효하지 않은 Action");

                return;
            }

            foreach (var entry in action.BaseEffects)
                ValidateEntry(
                    entry,
                    action,
                    path + ".BaseEffects",
                    errors
                );

            foreach (var entry in action.ConditionalEffects)
                ValidateEntry(
                    entry,
                    action,
                    path + ".ConditionalEffects",
                    errors
                );
        }

        private static void ValidateEntry(
            SkillEffectEntry entry,
            SkillActionData action,
            string path,
            List<string> errors)
        {
            if (entry == null)
            {
                errors.Add(path + ": 빈 엔트리");

                return;
            }

            if (action is not SkillAttackActionData && entry.Timing == SkillEffectTiming.OnHit)
                errors.Add(path + ": Cast/Dash의 OnHit은 지원하지 않습니다.");

            if (entry.Frequency == SkillEffectFrequency.OncePerExecution && entry.Subject != SkillEffectSubject.Self)
                errors.Add(path + ": OncePerExecution은 Self에만 지원합니다.");

            if (entry is SkillConditionalEffectEntry extra && extra.ConsumeStacks && (extra.StackQuery == null || !extra.StackQuery.IsValid || extra.StackCount < 1))
                errors.Add(path + ": 스택 소비 설정이 유효하지 않습니다.");

            ValidateEffects(
                entry.Effects,
                path + ".Effects",
                errors
            );
        }

        private static void ValidateEffects(
            IReadOnlyList<SkillEffectData> effects,
            string path,
            List<string> errors)
        {
            foreach (var effect in effects)
            {
                if (effect == null)
                    errors.Add(path + ": 빈 효과");

                if (effect is SkillRuntimeEffectData runtime && (runtime.EffectData == null || string.IsNullOrWhiteSpace(runtime.EffectData.EffectId)))
                    errors.Add(path + ": RuntimeEffect 참조 또는 ID가 비어 있습니다.");
            }
        }

        // ============================================================
        // Execution
        // ============================================================

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        // 어댑터의 값 보정 전에 원본 필드를 검사해 잘못된 값이 정상값으로 저장되는 것을 막는다.
        private static void ValidateSerializedValues(
            ScriptableObject asset,
            List<string> errors,
            HashSet<string> duplicateIds)
        {
            using var serialized = new SerializedObject(asset);
            var property = serialized.GetIterator();

            while (property.Next(true))
            {
                if (property.propertyType == SerializedPropertyType.Enum && property.enumValueIndex < 0)
                    errors.Add(property.propertyPath + ": 알 수 없는 enum 값");

                if (property.propertyType == SerializedPropertyType.Float && !Finite(property.floatValue))
                    errors.Add(property.propertyPath + ": 유한하지 않은 수치");

                if (property.propertyType == SerializedPropertyType.ObjectReference &&
                    property.objectReferenceValue is EffectData effect &&
                    duplicateIds != null && duplicateIds.Contains(effect.EffectId))
                    errors.Add(property.propertyPath + ": 중복 EffectId 참조 " + effect.EffectId);

                if (property.name is "_maxTargetCount" or "_maxEffectTargetCount" or "_maxEffectTargets" &&
                    property.propertyType == SerializedPropertyType.Integer && property.intValue < 1)
                    errors.Add(property.propertyPath + ": 대상 수는 1 이상이어야 합니다.");
            }
        }

        private static void WriteReport(Report report)
        {
            Directory.CreateDirectory("SkillMigrationReports");

            string path = Path.GetFullPath(Path.Combine("SkillMigrationReports", report.applied ? "apply.json" : "dry_run.json"));

            File.WriteAllText(path, JsonUtility.ToJson(report, true));

            Debug.Log("[Skill Migration] " + path + " — " + report.entries.Count + " assets, " + report.entries.Count(item => item.status == "Failed") + " failed");
        }
    }
}
#endif
