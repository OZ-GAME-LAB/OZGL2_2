using System;
using System.IO;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using Game.Core;
using OZGL.KDH;
using TMPro;
using Units;
using Units.UnitDatas;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.UI.Editor
{
    [InitializeOnLoad]
    public static class BalanceAuthoringPlayValidation
    {
        private const string Key = "Balance.PlayValidation";
        private const string Folder = "Logs/BalanceAuthoring";
        private static readonly StringBuilder Report = new StringBuilder();
        static BalanceAuthoringPlayValidation() => EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false)) Run().Forget();
        };

        public static void RunBatch()
        {
            if (!Application.isBatchMode || !Application.dataPath.Replace('\\', '/').Contains("/UnityUIValidation/")) throw new InvalidOperationException("Isolated validation project only.");
            var workspace = JsonUtility.FromJson<BalanceAuthoringWindow.Workspace>(File.ReadAllText(File.ReadAllText(Folder + "/manifest-path.txt")));
            var buildingEntry = workspace.entries.Single(e => e.source.EndsWith("/Test_Building_00.asset", StringComparison.Ordinal));
            var building = AssetDatabase.LoadAssetAtPath<ScriptableObject>(buildingEntry.working);
            var serialized = new SerializedObject(building);
            serialized.FindProperty("buildCost.Array.data[0].amount").intValue = 17;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(building);
            foreach (var entry in workspace.entries.Where(e => e.type == "UnitData" && e.connected))
            {
                var unit = AssetDatabase.LoadAssetAtPath<UnitData>(entry.working);
                if (unit.Team != UnitTeam.Ally) continue;
                serialized = new SerializedObject(unit);
                var stats = serialized.FindProperty("_stats");
                for (int i = 0; i < stats.arraySize; i++)
                {
                    var stat = stats.GetArrayElementAtIndex(i);
                    if (stat.FindPropertyRelative("_statType").intValue == (int)UnitStatType.MaxHp) stat.FindPropertyRelative("_value").floatValue = 12345;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(unit);
            }
            var scene = EditorSceneManager.OpenScene(workspace.scene);
            if (!scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SaveManager>(true)).Any())
                new GameObject("BalanceSaveIsolationProbe").AddComponent<SaveManager>();
            var wallet = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RunCurrencyManager>(true)).Single();
            serialized = new SerializedObject(wallet);
            var currencies = serialized.FindProperty("_baseStartingCurrencies");
            for (int i = 0; i < currencies.arraySize; i++)
            {
                var item = currencies.GetArrayElementAtIndex(i);
                if ((item.FindPropertyRelative("_currency").objectReferenceValue as CurrencyData)?.Type == CurrencyType.Gold) item.FindPropertyRelative("_amount").intValue = 500;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.SaveScene(scene);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static async UniTaskVoid Run()
        {
            Report.Clear(); bool success = false;
            try
            {
                var startup = Object.FindFirstObjectByType<TeamBuildingUiStartup>();
                var flow = Object.FindFirstObjectByType<GameFlowController>();
                await Wait(() => startup.IsReady && flow.CanEnterBuildMode(), 30);
                var wallet = Object.FindFirstObjectByType<RunCurrencyManager>();
                Check(wallet.GetBalance(CurrencyType.Gold) == 500, "test scene starting gold = 500");
                foreach (var save in Object.FindObjectsByType<SaveManager>(FindObjectsSortMode.None))
                    Check(save.DirectoryPath.Replace('\\', '/').Contains("/Library/BalanceSaves/"), "save directory isolated");
                var binding = Object.FindFirstObjectByType<RuntimeBuildingUiBinding>();
                var catalog = Object.FindFirstObjectByType<BuildingCatalogPanel>();
                var actions = Object.FindFirstObjectByType<BuildingActionPanel>();
                var slot = Object.FindObjectsByType<BuildingSlot>(FindObjectsSortMode.None).First(s => !s.IsOccupied && s.GetComponent<Collider2D>() != null && s.GetComponent<Collider2D>().enabled);
                binding.SelectSlot(slot); await UniTask.NextFrame();
                var cards = new SerializedObject(catalog).FindProperty("_cards");
                Button choice = null;
                for (int i = 0; i < cards.arraySize; i++)
                {
                    var card = cards.GetArrayElementAtIndex(i);
                    if (((TMP_Text)card.FindPropertyRelative("Name").objectReferenceValue).text == "melee1") choice = card.FindPropertyRelative("Button").objectReferenceValue as Button;
                }
                Check(choice != null && choice.interactable, "working building present in real catalog");
                choice.onClick.Invoke(); await UniTask.NextFrame();
                Ref<Button>(actions, "_build._button").onClick.Invoke(); await UniTask.NextFrame();
                Check(slot.IsOccupied, "real building created");
                Check(wallet.GetBalance(CurrencyType.Gold) == 483, "working cost 17 deducted (500 -> 483)");
                await Capture("building-cost");
                binding.ClearSelection(); await UniTask.NextFrame();
                Ref<Button>(Object.FindFirstObjectByType<GameUIController>(), "_waveStartButton").onClick.Invoke();
                await Wait(() => Object.FindObjectsByType<Unit_Gateway>(FindObjectsSortMode.None).Any(u => u.Team == UnitTeam.Ally && u.IsAlive), 60);
                var allies = Object.FindObjectsByType<Unit_Gateway>(FindObjectsSortMode.None).Where(u => u.Team == UnitTeam.Ally && u.IsAlive).ToArray();
                Check(allies.Any(u => Math.Abs(u.CurrentHp - 12345) < 0.1f), "spawned ally uses copied MaxHp = 12345");
                Report.AppendLine("Ally HP: " + string.Join(", ", allies.Select(u => u.CurrentHp.ToString())));
                await UniTask.Delay(300);
                await Capture("spawned-units");
                await UniTask.Delay(500);
                success = true;
            }
            catch (Exception exception) { Report.AppendLine("FAIL " + exception); Debug.LogException(exception); }
            finally
            {
                SessionState.SetBool(Key, false);
                File.WriteAllText(Folder + "/play-results.txt", Report.ToString());
                Debug.Log("[BalancePlayValidation] " + (success ? "PASS" : "FAIL"));
                EditorApplication.Exit(success ? 0 : 1);
            }
        }

        private static T Ref<T>(Object owner, string path) where T : Object => new SerializedObject(owner).FindProperty(path).objectReferenceValue as T;
        private static async UniTask Capture(string name)
        {
            await UniTask.DelayFrame(2);
            var camera = Camera.main;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.isActiveAndEnabled).ToArray();
            var modes = canvases.Select(c => c.renderMode).ToArray();
            var cameras = canvases.Select(c => c.worldCamera).ToArray();
            var planes = canvases.Select(c => c.planeDistance).ToArray();
            var scales = canvases.Select(c => c.scaleFactor).ToArray();
            var scalers = canvases.Select(c => c.GetComponent<CanvasScaler>()).ToArray();
            var enabled = scalers.Select(s => s != null && s.enabled).ToArray();
            var target = new RenderTexture(1280, 720, 24); target.Create();
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (scalers[i] != null) scalers[i].enabled = false;
                    canvases[i].renderMode = RenderMode.ScreenSpaceCamera; canvases[i].worldCamera = camera;
                    canvases[i].planeDistance = 1; canvases[i].scaleFactor = 2f / 3f;
                }
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                File.WriteAllBytes(Folder + "/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = oldActive; camera.targetTexture = oldTarget;
                for (int i = 0; i < canvases.Length; i++)
                {
                    canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; canvases[i].planeDistance = planes[i]; canvases[i].scaleFactor = scales[i];
                    if (scalers[i] != null) scalers[i].enabled = enabled[i];
                }
                Object.Destroy(image); target.Release(); Object.Destroy(target);
            }
        }
        private static void Check(bool condition, string text) { if (!condition) throw new InvalidOperationException(text); Report.AppendLine("PASS " + text); }
        private static async UniTask Wait(Func<bool> condition, float timeout)
        {
            float start = Time.realtimeSinceStartup;
            while (!condition()) { if (Time.realtimeSinceStartup - start > timeout) throw new TimeoutException("Balance Play Mode wait"); await UniTask.NextFrame(); }
        }
    }
}
