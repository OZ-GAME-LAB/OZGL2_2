using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using Game.Core;
using OZGL.KDH;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.Editor
{
    /// <summary>격리된 팀 씬 복사본에서만 실행한다. 테스트 조작/캡처는 런타임 상태이며 에셋은 저장하지 않는다.</summary>
    [InitializeOnLoad]
    public static class TeamBuildingUiValidation
    {
        private const string Key = "Game.UI.TeamBuildingValidation";
        private static readonly StringBuilder _report = new StringBuilder();
        private static int _checks;
        private static int _errors;
        private static int _searchErrors;

        static TeamBuildingUiValidation() => EditorApplication.playModeStateChanged += HandleState;

        public static void RunBatch()
        {
            if (!Application.isBatchMode || !Path.GetFullPath(Application.dataPath).Replace('\\', '/').Contains("/UnityUIValidation/"))
                throw new InvalidOperationException("Use an isolated UnityUIValidation project.");
            TeamBuildingUiSetup.CreateScene();
            var scene = EditorSceneManager.OpenScene(TeamBuildingUiSetup.ScenePath);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                        throw new InvalidOperationException("Missing script: " + transform.name);
                    foreach (var component in transform.GetComponents<MonoBehaviour>())
                    {
                        if (component == null) continue;
                        var property = new SerializedObject(component).GetIterator();
                        while (property.Next(true))
                        {
                            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                            var reference = property.objectReferenceValue;
                            if (reference == null && property.objectReferenceInstanceIDValue != 0)
                                throw new InvalidOperationException("Missing reference: " + component.GetType().Name + "." + property.propertyPath);
                            var referencedObject = reference is Component c ? c.gameObject : reference as GameObject;
                            if (referencedObject != null && !EditorUtility.IsPersistent(referencedObject) && referencedObject.scene != scene)
                                throw new InvalidOperationException("Cross-scene reference: " + component.GetType().Name + "." + property.propertyPath);
                        }
                    }
                }
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void HandleState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false)) RunAsync().Forget();
        }

        private static async UniTaskVoid RunAsync()
        {
            _checks = _errors = _searchErrors = 0; _report.Clear();
            Application.logMessageReceived += HandleLog;
            try
            {
                var startup = UnityEngine.Object.FindFirstObjectByType<TeamBuildingUiStartup>();
                var flow = UnityEngine.Object.FindFirstObjectByType<GameFlowController>();
                await Wait(() => startup != null && startup.IsReady && flow.CanEnterBuildMode());
                await UniTask.NextFrame();
                var binding = UnityEngine.Object.FindFirstObjectByType<RuntimeBuildingUiBinding>();
                var wallet = UnityEngine.Object.FindFirstObjectByType<RunCurrencyManager>();
                var controller = UnityEngine.Object.FindFirstObjectByType<BuildingBuildController>();
                var catalog = UnityEngine.Object.FindFirstObjectByType<BuildingCatalogPanel>();
                var info = UnityEngine.Object.FindFirstObjectByType<BuildingInfoPanel>();
                var actions = UnityEngine.Object.FindFirstObjectByType<BuildingActionPanel>();
                var hud = UnityEngine.Object.FindFirstObjectByType<GameUIController>();
                var slots = UnityEngine.Object.FindObjectsByType<BuildingSlot>(FindObjectsSortMode.None).OrderBy(s => s.name).ToArray();
                var data = AssetDatabase.LoadAssetAtPath<BuildingDatabase>("Assets/Tests/KDH/Test_BuildingDatabase.asset");
                var candidates = new List<BuildingData>(); slots[0].CollectCandidates(candidates, data, 1);
                var build = Ref<Button>(actions, "_build._button");
                var dismantle = Ref<Button>(actions, "_dismantle._button");
                var goldText = Ref<TMP_Text>(hud, "_goldText");
                Check(slots.Length > 0 && slots.All(s => s.GetComponent<RuntimeBuildingSelectionTarget>() != null), "all original slots have UI input: " + slots.Length);
                Check(candidates.Count == 3, "three original team building candidates, no UI fixture database");
                Check(Ref<BuildingDatabase>(binding, "_database") == data, "binding uses team database identity");
                Check(Ref<RunCurrencyManager>(binding, "_wallet") == wallet && Ref<BuildingBuildController>(binding, "_controller") == controller, "UI and controller share team authority");
                Check(Mathf.Approximately(controller.RefundRate, .7f), "original team 70 percent refund retained");
                Check(wallet.GetBalance(CurrencyType.Gold) == 100 && goldText.text == "100", "original starting gold initializes automatically once");
                Check(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "one original event system");
                Check(!UnityEngine.Object.FindObjectsByType<CurrencyTestPanel>(FindObjectsSortMode.None).Any(p => p.enabled), "manual currency debug panel hidden in copy");
                Check(!UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Any(c => c.name == "TestCanvas" && c.enabled), "team debug canvas hidden, logic preserved");
                Check(UnityEngine.Object.FindFirstObjectByType<Samples.MvpRuntimeHudSample>() == null && UnityEngine.Object.FindFirstObjectByType<Samples.PlayerUiPreviewBindings>() == null, "no preview initialization or mock units");
                Check(!Ref<Button>(hud, "_waveStartButton").gameObject.activeSelf, "test combat start not exposed as production UI");
                await Capture("01-team-slots", 1280, 720);

                foreach (var slot in slots)
                {
                    binding.ClearSelection(); await UniTask.NextFrame();
                    Check(ClickTarget(Point(slot)) == slot.gameObject, "slot frontmost raycast: " + slot.name);
                    Click(Point(slot));
                    Check(binding.SelectedSlot == slot && catalog.Popup.IsVisible && catalog.ItemCount == candidates.Count, "world click opens exact team slot catalog: " + slot.name);
                }
                binding.ClearSelection(); await UniTask.NextFrame();
                Click(Point(slots[0])); await UniTask.NextFrame();
                await Capture("02-team-catalog", 1280, 720);
                Check(ClickTarget(Point(slots[1])) != slots[1].gameObject, "catalog backdrop blocks world slot raycast");
                Click(Point(slots[1]));
                Check(binding.SelectedSlot != slots[1], "closing backdrop does not click through to another slot");

                for (int i = 0; i < candidates.Count; i++)
                {
                    binding.ClearSelection(); await UniTask.NextFrame();
                    Click(Point(slots[0])); await UniTask.NextFrame();
                    var item = candidates[i];
                    var cards = new SerializedObject(catalog).FindProperty("_cards");
                    var card = cards.GetArrayElementAtIndex(i);
                    Check(((TMP_Text)card.FindPropertyRelative("Name").objectReferenceValue).text == item.DisplayName, "catalog name comes from original data: " + item.BuildingId);
                    int price = item.BuildCost.Single(c => c.type == BuildingResourceType.Gold).amount;
                    Check(((TMP_Text)card.FindPropertyRelative("Price").objectReferenceValue).text == $"{price:N0} 골드", "catalog price comes from original data: " + price);
                    Click(Point((Button)card.FindPropertyRelative("Button").objectReferenceValue)); await UniTask.NextFrame();
                    Check(info.HasSelection && build.interactable, "original building offer executable: " + item.BuildingId);
                    Check(!Ref<GameObject>(actions, "_upgrade._rowRoot").activeSelf, "unimplemented upgrade not offered");
                    Check(!Ref<TMP_Text>(info, "_levelText").gameObject.activeSelf, "no invented building level without team API");
                    Check(Ref<TMP_Text>(info, "_nameText").text == item.DisplayName && Ref<TMP_Text>(info, "_descriptionText").text == item.Description, "building info reads original name and description");
                    int before = wallet.GetBalance(CurrencyType.Gold);
                    Click(Point(build)); await UniTask.NextFrame();
                    Check(slots[0].IsOccupied && slots[0].CurrentBuilding.Data == item, "actual original building instantiated: " + item.BuildingId);
                    Check(wallet.GetBalance(CurrencyType.Gold) == before - price && goldText.text == (before - price).ToString("N0"), "exactly one original cost deducted and HUD refreshed");
                    Check(slots[0].CurrentBuilding.GetComponent<SpriteRenderer>().sprite == item.WorldSprite, "original world sprite retained");
                    binding.ClearSelection(); await UniTask.NextFrame();
                    Click(Point(slots[0])); await UniTask.NextFrame();
                    Check(info.HasSelection && dismantle.interactable && !catalog.Popup.IsVisible, "click on original built prefab opens demolition info");
                    int refund = Mathf.FloorToInt(price * controller.RefundRate);
                    Check(Ref<TMP_Text>(actions, "_dismantle._quote").text == $"{refund:N0} 골드", "original refund quote including rounding");
                    if (i == 0) await Capture("03-team-building-info", 1280, 720);
                    if (i == 1) { await Capture("04-team-info-16x10", 1280, 800); await Capture("05-team-info-fullhd", 1920, 1080); }
                    Click(Point(dismantle)); await UniTask.NextFrame();
                    Check(!slots[0].IsOccupied && wallet.GetBalance(CurrencyType.Gold) == before - price + refund, "demolition invokes real refund once");
                    Check(goldText.text == wallet.GetBalance(CurrencyType.Gold).ToString("N0"), "HUD shows refunded balance");
                }

                int balance = wallet.GetBalance(CurrencyType.Gold);
                Check(wallet.TrySpend(CurrencyType.Gold, balance), "test-only setup: empty wallet");
                binding.ClearSelection(); await UniTask.NextFrame(); Click(Point(slots[0])); await UniTask.NextFrame();
                var firstCard = new SerializedObject(catalog).FindProperty("_cards").GetArrayElementAtIndex(0);
                Click(Point((Button)firstCard.FindPropertyRelative("Button").objectReferenceValue)); await UniTask.NextFrame();
                Check(!build.interactable, "insufficient gold disables build");
                Click(Point(build));
                Check(!slots[0].IsOccupied && wallet.GetBalance(CurrencyType.Gold) == 0, "disabled click creates no building or charge");
                Check(wallet.TryAdd(CurrencyType.Gold, balance), "test-only balance restore");
                Check(build.interactable, "live balance update re-enables offer");
                Check(wallet.TryAdd(CurrencyType.Gem, 2), "test-only gem change");
                Check(Ref<TMP_Text>(startup, "_gemText").text == "보석 2", "gem HUD observes original wallet event");
                for (int i = 0; i < 3; i++)
                {
                    startup.enabled = false; startup.enabled = true;
                    hud.gameObject.SetActive(false); hud.gameObject.SetActive(true);
                    startup.Refresh();
                    Check(wallet.GetBalance(CurrencyType.Gold) == balance && wallet.GetBalance(CurrencyType.Gem) == 2, "re-enable does not reset or grant currency: " + i);
                }
                binding.ClearSelection(); await UniTask.NextFrame();
                var launch = hud.transform.Find("BottomBar/Build").GetComponent<Button>();
                Click(Point(launch));
                Check(binding.SelectedSlot != null && catalog.Popup.IsVisible, "build shortcut selects a real empty team slot");
                binding.ClearSelection();
                Check(await flow.TrySpawnUnits(), "original core phase transition available (TestSpawner, not full combat)");
                Click(Point(slots[0]));
                Check(binding.SelectedSlot == null && !catalog.Popup.IsVisible, "battle phase blocks original slot construction UI");
                await ValidateAutomaticRewards(startup, flow, wallet, hud);
                Check(_errors == 0, "no runtime UI/game errors");
                _report.Insert(0, $"PASS: {_checks} team scene/UI assertions. Runtime errors: {_errors}; Search startup exceptions: {_searchErrors}.\nUnity 6000.3.23f1, isolated Play Mode. Real team assets/API, not full combat or Player build.\n");
                File.WriteAllText(Output("results.txt"), _report.ToString());
                Debug.Log("[UI/TeamBuildingValidation] " + _checks + " assertions passed.");
                SessionState.SetBool(Key, false); EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                File.WriteAllText(Output("results.txt"), "FAIL\n" + _report + "\n" + exception);
                SessionState.SetBool(Key, false); EditorApplication.Exit(1);
            }
            finally { Application.logMessageReceived -= HandleLog; }
        }

        private static async UniTask ValidateAutomaticRewards(TeamBuildingUiStartup startup,
            GameFlowController flow, RunCurrencyManager wallet, GameUIController hud)
        {
            var waves = UnityEngine.Object.FindFirstObjectByType<WaveController>();
            var gate = UnityEngine.Object.FindFirstObjectByType<TestWaitingScript>();
            var goldText = Ref<TMP_Text>(hud, "_goldText");
            var gemText = Ref<TMP_Text>(startup, "_gemText");
            int events = 0;
            bool duplicateRejected = true;
            bool atomicSnapshot = true;
            int expectedGold = 0;
            int expectedGems = 0;
            Action<CurrencyData, int, int> changed = (currency, before, after) =>
            {
                events++;
                duplicateRejected &= !wallet.TryApplyWaveReward();
                atomicSnapshot &= wallet.GetBalance(CurrencyType.Gold) == expectedGold &&
                    wallet.GetBalance(CurrencyType.Gem) == expectedGems;
            };
            float spawn = flow.SpawnTime;
            float staging = flow.StagingTime;
            wallet.BalanceChanged += changed;
            try
            {
                flow.SpawnTime = flow.StagingTime = .02f;
                for (int round = 0; round < 2; round++)
                {
                    if (round == 1)
                    {
                        waves.JumpToLastWaveForTest();
                        Check(await flow.TrySpawnUnits(), "boss reward test starts through Core");
                    }
                    int gold = MvpEconomyUiValidation.GetExpectedReward(waves, CurrencyType.Gold);
                    int gems = MvpEconomyUiValidation.GetExpectedReward(waves, CurrencyType.Gem);
                    Check(wallet.CurrentGoldReward == gold && wallet.CurrentGemReward == gems,
                        "BattlePreparing freezes the team reward amounts: " + round);
                    expectedGold = wallet.GetBalance(CurrencyType.Gold) + gold;
                    expectedGems = wallet.GetBalance(CurrencyType.Gem) + gems;
                    int beforeEvents = events;
                    flow.ResolveBattleAsync(ResultType.Victory).Forget();
                    await Wait(() => flow.CurPhase == GamePhase.Reward);
                    await UniTask.NextFrame();
                    Check(wallet.GetBalance(CurrencyType.Gold) == expectedGold &&
                        wallet.GetBalance(CurrencyType.Gem) == expectedGems,
                        "Reward phase grants currency without any UI payout: " + round);
                    Check(goldText.text == expectedGold.ToString("N0") && gemText.text == $"보석 {expectedGems:N0}",
                        "Gold and Gem HUD show the automatic wallet transaction: " + round);
                    Check(events == beforeEvents + (gold > 0 ? 1 : 0) + (gems > 0 ? 1 : 0) &&
                        duplicateRejected && atomicSnapshot, "one atomic automatic payout rejects event reentry: " + round);
                    Check(wallet.TryPrepareWaveReward() && !wallet.TryApplyWaveReward(),
                        "same-wave preparation and manual retry cannot repay: " + round);
                    int stableEvents = events;
                    startup.enabled = false; startup.enabled = true; startup.Refresh();
                    Check(events == stableEvents && wallet.GetBalance(CurrencyType.Gold) == expectedGold,
                        "UI re-enable cannot reinitialize or repay the wallet: " + round);
                    gate.ChooseResultBtn();
                    await Wait(() => flow.CurPhase == GamePhase.Preparation);
                    Check(waves.CurQuarter == (round == 0 ? 1 : 2) && waves.CurWave == (round == 0 ? 2 : 1),
                        "explicit test gate progresses the original Core once: " + round);
                }

                int beforeLoss = events;
                Check(await flow.TrySpawnUnits(), "defeat scenario starts through Core");
                await flow.ResolveBattleAsync(ResultType.Defeat);
                Check(flow.CurPhase == GamePhase.Finished && events == beforeLoss &&
                    wallet.GetBalance(CurrencyType.Gold) == expectedGold &&
                    wallet.GetBalance(CurrencyType.Gem) == expectedGems, "defeat does not award victory currency");
                Check(wallet.CurrentGoldReward == 0 && wallet.CurrentGemReward == 0 && !wallet.TryApplyWaveReward(),
                    "Finished clears prepared reward and rejects late payout");

                Check(wallet.TryEndRun(), "test owner ends wallet run");
                wallet.Initialize(waves, flow, null);
                flow.ResetRun();
                startup.Refresh();
                Check(wallet.GetBalance(CurrencyType.Gold) == 100 && wallet.GetBalance(CurrencyType.Gem) == 0 &&
                    goldText.text == "100" && gemText.text == "보석 0", "explicit new run refreshes both HUD currencies");
                Check(await flow.TrySpawnUnits(), "new run starts normally");
                expectedGold = 100 + wallet.CurrentGoldReward;
                expectedGems = wallet.CurrentGemReward;
                int newRunEvents = events;
                flow.ResolveBattleAsync(ResultType.Victory).Forget();
                await Wait(() => flow.CurPhase == GamePhase.Reward);
                await UniTask.NextFrame();
                Check(events == newRunEvents + (wallet.CurrentGoldReward > 0 ? 1 : 0) +
                    (wallet.CurrentGemReward > 0 ? 1 : 0) && duplicateRejected && atomicSnapshot,
                    "end/reinitialize removes old subscriptions and pays only once");
                Check(goldText.text == expectedGold.ToString("N0") && gemText.text == $"보석 {expectedGems:N0}",
                    "new-run reward is reflected in the visible HUD");
                gate.ChooseResultBtn();
                await Wait(() => flow.CurPhase == GamePhase.Preparation);
            }
            finally
            {
                wallet.BalanceChanged -= changed;
                flow.SpawnTime = spawn;
                flow.StagingTime = staging;
            }
        }

        private static T Ref<T>(UnityEngine.Object obj, string field) where T : UnityEngine.Object =>
            new SerializedObject(obj).FindProperty(field).objectReferenceValue as T;
        private static Vector2 Point(Component component)
        {
            Canvas.ForceUpdateCanvases();
            if (component.transform is RectTransform rect) return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            return Camera.main.WorldToScreenPoint(component.transform.position);
        }
        private static RaycastResult Hit(Vector2 point)
        {
            Canvas.ForceUpdateCanvases(); Physics2D.SyncTransforms();
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) {position = point}, hits);
            if (hits.Count == 0) throw new InvalidOperationException("No hit at " + point);
            return hits[0];
        }
        private static GameObject ClickTarget(Vector2 point) => ExecuteEvents.GetEventHandler<IPointerClickHandler>(Hit(point).gameObject);
        private static void Click(Vector2 point)
        {
            var hit = Hit(point);
            var data = new PointerEventData(EventSystem.current) {position = point, pressPosition = point,
                button = PointerEventData.InputButton.Left, pointerId = -1, pointerCurrentRaycast = hit,
                pointerPressRaycast = hit, eligibleForClick = true};
            data.pointerPress = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
            ExecuteEvents.ExecuteHierarchy(hit.gameObject, data, ExecuteEvents.pointerDownHandler);
            if (data.pointerPress != null)
            {
                ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerClickHandler);
            }
        }
        private static async UniTask Wait(Func<bool> condition)
        {
            float start = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (Time.realtimeSinceStartup - start > 30) throw new TimeoutException("Team startup");
                await UniTask.NextFrame();
            }
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            _checks++; _report.AppendLine("PASS " + message);
        }
        private static string Output(string name)
        {
            string directory = Path.GetFullPath("Logs/TeamBuildingUi20260915");
            Directory.CreateDirectory(directory); return Path.Combine(directory, name);
        }
        private static void HandleLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception && stack.Contains("UnityEditor.Search.SearchDatabase")) { _searchErrors++; return; }
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors++;
        }
        private static async UniTask Capture(string name, int width, int height)
        {
            await UniTask.DelayFrame(2);
            var camera = Camera.main;
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.isActiveAndEnabled).ToArray();
            var modes = canvases.Select(c => c.renderMode).ToArray();
            var scales = canvases.Select(c => c.scaleFactor).ToArray();
            var scalers = canvases.Select(c => c.GetComponent<CanvasScaler>()).ToArray();
            var enabled = scalers.Select(s => s != null && s.enabled).ToArray();
            var target = new RenderTexture(width, height, 24); target.Create();
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (scalers[i] != null) scalers[i].enabled = false;
                    canvases[i].renderMode = RenderMode.ScreenSpaceCamera; canvases[i].worldCamera = camera;
                    canvases[i].planeDistance = 1; canvases[i].scaleFactor = Mathf.Sqrt(width / 1920f * height / 1080f);
                }
                Canvas.ForceUpdateCanvases();
                foreach (var label in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                {
                    var parent = label.GetComponentInParent<Canvas>();
                    if (parent == null || !parent.enabled || string.IsNullOrEmpty(label.text) || label.GetComponentInParent<ScrollRect>() != null) continue;
                    label.ForceMeshUpdate(); Check(!label.isTextOverflowing, name + " text fits: " + label.name);
                }
                camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                File.WriteAllBytes(Output(name + ".png"), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = oldActive; camera.targetTexture = oldTarget;
                for (int i = 0; i < canvases.Length; i++)
                {
                    canvases[i].renderMode = modes[i]; canvases[i].worldCamera = null; canvases[i].scaleFactor = scales[i];
                    if (scalers[i] != null) scalers[i].enabled = enabled[i];
                }
                UnityEngine.Object.Destroy(image); target.Release(); UnityEngine.Object.Destroy(target);
            }
            await UniTask.NextFrame();
        }
    }
}
