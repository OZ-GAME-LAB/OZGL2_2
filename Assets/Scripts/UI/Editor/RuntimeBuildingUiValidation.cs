using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.UI.Samples;
using OZGL.KDH;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Editor
{
    /// <summary>별도 배치 Editor의 실제 Play Mode에서 원본 건물/재화의 상태 변화를 검사한다.</summary>
    [InitializeOnLoad]
    public static class RuntimeBuildingUiValidation
    {
        private const string SessionKey = "Game.UI.BuildingIntegration.Running";
        private static int _checks;
        private static int _errors;
        private static int _searchErrors;
        static RuntimeBuildingUiValidation() => EditorApplication.playModeStateChanged += HandleState;

        public static void RunBatch()
        {
            if (!Application.isBatchMode || !Path.GetFullPath(Application.dataPath).Replace('\\', '/').Contains("/UnityUIValidation/"))
                throw new InvalidOperationException("Use an isolated UnityUIValidation batch project.");
            RuntimeBuildingUiSetup.CreateScene();
            EditorSceneManager.OpenScene(RuntimeBuildingUiSetup.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(SessionKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static void HandleState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(SessionKey, false)) RunAsync().Forget();
        }

        private static async UniTaskVoid RunAsync()
        {
            _checks = _errors = _searchErrors = 0;
            Application.logMessageReceived += HandleLog;
            try
            {
                var binding = UnityEngine.Object.FindFirstObjectByType<RuntimeBuildingUiBinding>();
                var flow = UnityEngine.Object.FindFirstObjectByType<GameFlowController>();
                var wallet = UnityEngine.Object.FindFirstObjectByType<RunCurrencyManager>();
                var sample = UnityEngine.Object.FindFirstObjectByType<MvpRuntimeHudSample>();
                var catalog = UnityEngine.Object.FindFirstObjectByType<BuildingCatalogPanel>();
                var actions = UnityEngine.Object.FindFirstObjectByType<BuildingActionPanel>();
                var info = UnityEngine.Object.FindFirstObjectByType<BuildingInfoPanel>();
                var slots = UnityEngine.Object.FindObjectsByType<BuildingSlot>(FindObjectsSortMode.None).OrderBy(s => s.name).ToArray();
                await Wait(() => sample.IsReady && flow.CanEnterBuildMode(), "scene initialization");
                Check(binding != null && slots.Length == 4, "four real BuildingSlot instances");
                var build = Ref<Button>(actions, "_build._button");
                var dismantle = Ref<Button>(actions, "_dismantle._button");
                var quote = Ref<TMP_Text>(actions, "_build._quote");
                int initialGold = wallet.GetBalance(CurrencyType.Gold);
                int initialGem = wallet.GetBalance(CurrencyType.Gem);
                binding.SelectSlot(slots[0]);
                Check(catalog.Popup.IsVisible && catalog.ItemCount == 2, "real database candidates open catalog");
                Select(catalog, 0);
                Check(info.HasSelection && build.interactable && quote.text == "30 골드", "gold quote and valid build");
                Check(!Ref<GameObject>(actions, "_upgrade._rowRoot").activeSelf, "missing upgrade API stays hidden");
                await Capture("01-build-quote");
                build.onClick.Invoke(); build.onClick.Invoke();
                Check(slots[0].IsOccupied && wallet.GetBalance(CurrencyType.Gold) == initialGold - 30,
                    "real build occupies slot and deducts once on double click");
                Check(slots[0].CurrentBuilding.GetComponent<BuildingProducer>() != null, "original producer module installed");
                Check(dismantle.interactable && !build.gameObject.activeInHierarchy, "occupied slot offers demolition, not replacement");
                await Capture("02-built");
                dismantle.onClick.Invoke(); dismantle.onClick.Invoke();
                Check(!slots[0].IsOccupied && wallet.GetBalance(CurrencyType.Gold) == initialGold,
                    "original default refund restores gold exactly once");
                Check(!info.HasSelection && binding.SelectedSlot == null, "demolition clears stale selection");
                await UniTask.NextFrame();
                var slotCaption = GameObject.Find("BuildPlot0").transform.Find("Caption").GetComponent<TMP_Text>();
                Check(slotCaption.text == "건설", "demolition restores empty slot label after Destroy");

                Check(wallet.TryAdd(CurrencyType.Gem, 10), "fund test gem balance through real wallet");
                binding.SelectSlot(slots[1]); Select(catalog, 1);
                Check(quote.text.Contains("20 골드") && quote.text.Contains("2 보석"), "both currencies shown");
                await Capture("03-two-currency-quote");
                build.onClick.Invoke();
                Check(slots[1].IsOccupied && wallet.GetBalance(CurrencyType.Gold) == initialGold - 20 &&
                    wallet.GetBalance(CurrencyType.Gem) == initialGem + 8, "real build charges both currencies");
                dismantle.onClick.Invoke();
                Check(wallet.GetBalance(CurrencyType.Gold) == initialGold && wallet.GetBalance(CurrencyType.Gem) == initialGem + 10,
                    "real demolition returns both currencies using controller rate");

                Check(wallet.TrySpend(CurrencyType.Gem, wallet.GetBalance(CurrencyType.Gem)), "empty gem balance");
                binding.SelectSlot(slots[1]); Select(catalog, 1);
                Check(!build.interactable, "gem shortage disables build despite sufficient gold");
                build.onClick.Invoke();
                Check(!slots[1].IsOccupied && wallet.GetBalance(CurrencyType.Gold) == initialGold, "shortage leaves no partial gold charge");
                binding.SelectSlot(slots[0]); Select(catalog, 0);
                Check(wallet.TrySpend(CurrencyType.Gold, initialGold), "external gold spending");
                Check(!build.interactable, "balance event refreshes affordability");
                Check(wallet.TryAdd(CurrencyType.Gold, initialGold), "restore test gold");
                Check(build.interactable, "balance event restores valid offer");

                slots[0].GetComponent<Collider2D>().enabled = false;
                build.onClick.Invoke();
                Check(!slots[0].IsOccupied && wallet.GetBalance(CurrencyType.Gold) == initialGold, "disabled slot rechecked at click time");
                slots[0].GetComponent<Collider2D>().enabled = true;
                binding.SelectSlot(slots[0]); Select(catalog, 0);
                var savedData = Ref<BuildingDatabase>(binding, "_database");
                var databaseFields = new SerializedObject(savedData);
                var candidateList = databaseFields.FindProperty("buildings");
                var firstCandidate = candidateList.GetArrayElementAtIndex(0).objectReferenceValue;
                candidateList.GetArrayElementAtIndex(0).objectReferenceValue = null;
                databaseFields.ApplyModifiedPropertiesWithoutUndo();
                build.onClick.Invoke();
                Check(!slots[0].IsOccupied && wallet.GetBalance(CurrencyType.Gold) == initialGold, "removed candidate cannot execute stale offer");
                candidateList.GetArrayElementAtIndex(0).objectReferenceValue = firstCandidate;
                databaseFields.ApplyModifiedPropertiesWithoutUndo();

                for (int i = 0; i < 3; i++) { binding.enabled = false; binding.enabled = true; }
                binding.SelectSlot(slots[0]); Select(catalog, 0); build.onClick.Invoke();
                Check(wallet.GetBalance(CurrencyType.Gold) == initialGold - 30, "binding re-enable does not duplicate receiver");
                dismantle.onClick.Invoke();
                binding.SelectSlot(slots[0]); Select(catalog, 0);
                var starting = flow.TryStartWave();
                build.onClick.Invoke();
                Check(!slots[0].IsOccupied && wallet.GetBalance(CurrencyType.Gold) == initialGold, "phase transition blocks stale click immediately");
                Check(await starting, "real Core starts battle");
                binding.SelectSlot(slots[0]);
                Check(binding.SelectedSlot == null, "battle blocks opening construction");
                await flow.ResolveBattleAsync(ResultType.Defeat);
                Check(flow.CurPhase == GamePhase.Finished, "original defeat completes without invented final result");
                flow.ResetRun(); await Wait(() => flow.CanEnterBuildMode(), "reset");
                binding.SelectSlot(slots[0]); Select(catalog, 0);
                binding.enabled = false;
                build.onClick.Invoke();
                Check(!slots[0].IsOccupied && !info.HasSelection, "disabled binding clears UI and detaches input");
                binding.enabled = true;
                Check(new BuildingCatalogItem("free", "무료", "", 0).GemCost == 0, "legacy gold constructor unchanged");
                Check(new BuildingCatalogItem("unknown", "미정", "", null).GemCost == null, "unknown price is not free");
                actions.ShowActions(new BuildingActionViewData("quote-only", "표시 검사",
                    new BuildingActionOffer(BuildingUiAction.Build, "표시 검사", 0, 3, false, "표시 검사", "quote-only")));
                Check(quote.text == "3 보석", "gem-only display");
                actions.HideActions();
                Expect(() => new BuildingCatalogItem("x", "x", "", 1, null), "partially unknown price rejected");
                Expect(() => new BuildingActionOffer(BuildingUiAction.Dismantle, "x", 0, -1, true), "negative gem refund rejected");
                await MvpRuntimeUnitInfoValidation.RunChecksAsync();
                Check(_errors == 0, "no UI/game errors during runtime checks");
                File.WriteAllText(Output("results.txt"), $"PASS: {_checks} building/quote/visual assertions; runtime unit-info regression executed.\nEditor Search startup exceptions: {_searchErrors}.\nReal building and wallet APIs; fixture prices, not the team gameplay map.\n");
                SessionState.SetBool(SessionKey, false); EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception); File.WriteAllText(Output("results.txt"), "FAIL\n" + exception);
                SessionState.SetBool(SessionKey, false); EditorApplication.Exit(1);
            }
            finally { Application.logMessageReceived -= HandleLog; }
        }

        private static T Ref<T>(UnityEngine.Object target, string field) where T : UnityEngine.Object =>
            new SerializedObject(target).FindProperty(field).objectReferenceValue as T;
        private static void Select(BuildingCatalogPanel catalog, int index) =>
            ((Button)new SerializedObject(catalog).FindProperty("_cards").GetArrayElementAtIndex(index)
                .FindPropertyRelative("Button").objectReferenceValue).onClick.Invoke();
        private static void Check(bool passed, string name)
        {
            if (!passed) throw new InvalidOperationException(name);
            _checks++; Debug.Log("[UI/RuntimeBuildingUiValidation] PASS " + name);
        }
        private static void Expect(Action action, string name)
        {
            try { action(); } catch (ArgumentException) { Check(true, name); return; }
            throw new InvalidOperationException(name);
        }
        private static async UniTask Wait(Func<bool> condition, string name)
        {
            float start = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (Time.realtimeSinceStartup - start > 30) throw new TimeoutException(name);
                await UniTask.NextFrame();
            }
        }
        private static string Output(string name)
        {
            var dir = Path.GetFullPath("Logs/BuildingIntegration20260915"); Directory.CreateDirectory(dir); return Path.Combine(dir, name);
        }
        private static void HandleLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception && stack.Contains("UnityEditor.Search.SearchDatabase") &&
                stack.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup")) { _searchErrors++; return; }
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors++;
        }
        private static async UniTask Capture(string name)
        {
            await UniTask.DelayFrame(2);
            var camera = Camera.main;
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas).ToArray();
            var modes = canvases.Select(c => c.renderMode).ToArray();
            var scales = canvases.Select(c => c.scaleFactor).ToArray();
            var scalers = canvases.Select(c => c.GetComponent<CanvasScaler>()).ToArray();
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
                Canvas.ForceUpdateCanvases();
                foreach (var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                {
                    if (string.IsNullOrEmpty(text.text) || text.GetComponentInParent<ScrollRect>() != null) continue;
                    text.ForceMeshUpdate(); Check(!text.isTextOverflowing, "text fits: " + text.name);
                }
                camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                File.WriteAllBytes(Output(name + ".png"), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = oldActive; camera.targetTexture = oldTarget;
                for (int i = 0; i < canvases.Length; i++)
                {
                    canvases[i].renderMode = modes[i]; canvases[i].worldCamera = null; canvases[i].scaleFactor = scales[i];
                    if (scalers[i] != null) scalers[i].enabled = true;
                }
                UnityEngine.Object.Destroy(image); target.Release(); UnityEngine.Object.Destroy(target);
            }
        }
    }
}
