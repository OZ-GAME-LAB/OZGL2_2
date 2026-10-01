using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.UI.Editor
{
    public static class BalanceAuthoringValidation
    {
        public static void RunBatch()
        {
            if (!Application.isBatchMode || !Application.dataPath.Replace('\\', '/').Contains("/UnityUIValidation/")) throw new InvalidOperationException("격리된 UnityUIValidation 프로젝트에서만 실행하세요.");
            int checks = 0;
            var report = new StringBuilder();
            void Check(bool condition, string name)
            {
                if (!condition) throw new InvalidOperationException(name);
                checks++; report.AppendLine("PASS " + name);
            }
            void Reject(Action action, string name)
            {
                bool rejected = false;
                try { action(); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected, name);
            }
            string manifest = BalanceAuthoringWindow.CreateWorkspace();
            var workspace = JsonUtility.FromJson<BalanceAuthoringWindow.Workspace>(File.ReadAllText(manifest));
            Directory.CreateDirectory("Logs/BalanceAuthoring");
            File.WriteAllText("Logs/BalanceAuthoring/manifest-path.txt", manifest);
            Check(workspace.entries.Count > 20, "dependency workspace created");
            Check(workspace.entries.All(e => BalanceAuthoringWindow.Hash(e.source) == e.hash), "all original files unchanged by creation");
            Check(workspace.entries.Where(e => !string.IsNullOrEmpty(e.group)).All(e => BalanceAuthoringWindow.GetChanges(e).Count == 0), "new workspace values equal source");
            foreach (string group in new[] { "유닛", "건물", "웨이브", "재화", "아티팩트", "상점" })
                Check(workspace.entries.Any(e => e.group == group && BalanceAuthoringWindow.NumericPaths(AssetDatabase.LoadAssetAtPath<ScriptableObject>(e.working)).Count > 0), "editable category " + group);
            var issues = BalanceAuthoringWindow.Validate(workspace);
            File.WriteAllLines("Logs/BalanceAuthoring/validation-issues.txt", issues);
            Check(!issues.Any(s => s.Contains("원본 참조 잔존") || s.Contains("데이터 타입 불일치") || s.Contains("목록 구조")), "no original graph references or structural mismatch");
            var scene = EditorSceneManager.OpenScene(workspace.scene);
            Check(PlayerTeamUiConnectionCheck.Inspect(scene).Count == 0, "copied UI scene connection check");
            Check(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "no missing scripts in copied scene");
            var entry = workspace.entries.First(e => e.type == "BuildingData" && e.source.EndsWith("Test_Building_00.asset", StringComparison.Ordinal));
            var original = AssetDatabase.LoadAssetAtPath<ScriptableObject>(entry.source);
            var copy = AssetDatabase.LoadAssetAtPath<ScriptableObject>(entry.working);
            string sourceText = File.ReadAllText(entry.source);
            string copyText = File.ReadAllText(entry.working);
            var values = new SerializedObject(copy);
            const string costPath = "buildCost.Array.data[0].amount";
            int previous = values.FindProperty(costPath).intValue;
            values.FindProperty(costPath).intValue = previous + 7;
            values.ApplyModifiedProperties();
            Check(BalanceAuthoringWindow.Hash(entry.source) == entry.hash, "working edit does not modify source");
            Check(BalanceAuthoringWindow.GetChanges(entry).Single().Contains("→ " + (previous + 7)), "numeric diff preview");
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo(); values.Update();
            Check(values.FindProperty(costPath).intValue == previous, "working edit Undo");
            Undo.PerformRedo(); values.Update();
            Check(values.FindProperty(costPath).intValue == previous + 7, "working edit Redo");
            AssetDatabase.SaveAssetIfDirty(copy);
            AssetDatabase.ImportAsset(entry.working, ImportAssetOptions.ForceUpdate);
            Check(BalanceAuthoringWindow.GetChanges(entry).Count == 1, "saved copy survives reimport");
            values = new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>(entry.working));
            string id = values.FindProperty("buildingId").stringValue;
            values.FindProperty("buildingId").stringValue = id + "_unsafe"; values.ApplyModifiedPropertiesWithoutUndo();
            Reject(() => BalanceAuthoringWindow.GetChanges(entry), "identity mutation blocked");
            values.Update(); values.FindProperty("buildingId").stringValue = id; values.ApplyModifiedPropertiesWithoutUndo();
            string hash = entry.hash; entry.hash = "stale";
            Reject(() => BalanceAuthoringWindow.Publish(entry), "teammate/source change blocked"); entry.hash = hash;
            try
            {
                BalanceAuthoringWindow.Publish(entry);
                Check(new SerializedObject(original).FindProperty(costPath).intValue == previous + 7, "explicit numeric publish");
                Check(new SerializedObject(original).FindProperty("buildingId").stringValue == id, "publish preserves identity");
                Check(Directory.GetFiles("Library/BalanceBackups", "*.asset", SearchOption.AllDirectories).Length > 0, "publish backup created");
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
                Check(new SerializedObject(original).FindProperty(costPath).intValue == previous, "publish Undo");
            }
            finally
            {
                // Only this isolated validation project is restored, never the user's checkout.
                File.WriteAllText(entry.source, sourceText, new UTF8Encoding(false));
                File.WriteAllText(entry.working, copyText, new UTF8Encoding(false));
                EditorUtility.ClearDirty(original);
                EditorUtility.ClearDirty(AssetDatabase.LoadAssetAtPath<ScriptableObject>(entry.working));
                AssetDatabase.ImportAsset(entry.source, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(entry.working, ImportAssetOptions.ForceUpdate);
            }
            Check(BalanceAuthoringWindow.Hash(entry.source) == hash, "source restored after isolated publish test");
            Check(!BalanceAuthoringWindow.IsWorkspacePath("Assets/BalanceWorkspace/Editor/../../bad"), "path traversal blocked");
            report.AppendLine("Checks passed: " + checks);
            report.AppendLine("Existing data validation findings: " + issues.Count);
            File.WriteAllText("Logs/BalanceAuthoring/results.txt", report.ToString());
            Debug.Log("[BalanceValidation] PASS " + checks + " checks. Existing data findings: " + issues.Count);
        }
    }
}
