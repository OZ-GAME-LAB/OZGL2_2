#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI.InGame.Editor
{
    /// <summary>저장 경로를 격리한 새 Play 세션에서 상점 진입·구매·종료를 검사한다. 원본 에셋은 저장하지 않는다.</summary>
    [InitializeOnLoad]
    public static class ShopUIValidation
    {
        private const string Key = "Shop.UI.Validation.";
        private const string Output = "Logs/ShopValidation.txt";
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly List<string> Checks = new List<string>();
        public static string LastResult => SessionState.GetString(Key + "Result", "Not run");

        static ShopUIValidation()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key + "Armed", false)) return;
                SessionState.SetBool(Key + "Armed", false);
                InGameUITwoCanvasValidation.ArmPlayFontIsolation(false);
            };
        }

        public static string ValidatePrefab(string path = "Assets/Prefabs/UI/Test/InGameUIRoot.prefab")
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) throw new InvalidOperationException("UI prefab is missing: " + path);
            var view = root.GetComponentInChildren<ShopView>(true);
            if (view == null) throw new InvalidOperationException("ShopView is missing from the UI prefab.");
            UIScreen screen = Read<UIScreen>(view, "_screen");
            if (screen.Id != UIId.Shop || screen.IsHud || screen.CanCloseByUser || !screen.BlocksHudInput ||
                screen.Root == screen.gameObject || !screen.Root.transform.IsChildOf(screen.transform) ||
                !screen.gameObject.activeSelf)
                throw new InvalidOperationException("Shop requires an active host and a separate mandatory popup root.");
            var ui = root.GetComponentInChildren<InGameUIManager>(true);
            if (!Read<UIScreen[]>(ui, "_screenInstances").Contains(screen))
                throw new InvalidOperationException("Shop is not registered in InGameUIManager.");
            if (root.GetComponentsInChildren<Canvas>(true).Length != 2 ||
                screen.GetComponentInParent<Canvas>(true).name != "PopupCanvas")
                throw new InvalidOperationException("Shop must use the existing PopupCanvas.");
            Read<TMP_Text>(view, "_goldText");
            Read<TMP_Text>(view, "_feedbackText");
            Read<Button>(view, "_leaveButton");
            var artifacts = Read<ShopItemCardView[]>(view, "_artifactCards");
            var consumables = Read<ShopItemCardView[]>(view, "_consumableCards");
            if (artifacts.Length != 5 || consumables.Length != 1)
                throw new InvalidOperationException("Expected five artifact cards and one consumable card.");
            foreach (ShopItemCardView card in artifacts.Concat(consumables))
            {
                Read<Image>(card, "_icon");
                Read<TMP_Text>(card, "_nameText");
                Read<TMP_Text>(card, "_descriptionText");
                Read<TMP_Text>(card, "_priceText");
                Read<Button>(card, "_buyButton");
                Read<TMP_Text>(card, "_buyButtonText");
            }
            var exchange = screen.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.name.IndexOf("Exchange", StringComparison.OrdinalIgnoreCase) >= 0);
            if (exchange == null || exchange.interactable)
                throw new InvalidOperationException("The exchange placeholder button must be disabled.");
            return "PASS: prefab has five artifact cards, one consumable card, disabled exchange, required references and two Canvas registration.";
        }

        [MenuItem("Tools/InGame UI/Validate Shop in Fresh Play Session")]
        public static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Begin in Edit Mode before a fresh Play session.");
            if (EditorSettings.enterPlayModeOptionsEnabled &&
                (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableSceneReload) != 0)
                throw new InvalidOperationException("Scene reload is required for pre-Start save isolation.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "Test") throw new InvalidOperationException("Open Test before running shop validation.");
            string prefabResult = ValidatePrefab();
            BootStrap bootstrap = InScene<BootStrap>(scene).Single();
            var startup = Read<InGameUIStartup>(bootstrap, "_uiStartup");
            ShopManager shop = Read<ShopManager>(bootstrap, "_shopManager");
            ConsumableItemManager items = Read<ConsumableItemManager>(bootstrap, "_consumableItemManager");
            Read<ShopView>(startup, "_shopView");
            Read<ShopTable>(shop, "_shopTable");
            Read<ConsumableItemCatalog>(items, "_itemCatalog");
            Read<ConsumableItemCaster>(items, "_itemCasterPrefab");
            if (!items.gameObject.activeInHierarchy) throw new InvalidOperationException("Consumable manager must stay active.");
            string saves = Path.GetFullPath(".utmp/ShopValidation/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saves);
            SessionState.SetString(Key + "Saves", saves);
            SessionState.SetString(Key + "Result", "ARMED\n" + prefabResult);
            SessionState.SetString(Key + "SafetyError", "");
            SessionState.SetBool(Key + "Armed", true);
            SessionState.SetBool(Key + "Started", false);
            InGameUITwoCanvasValidation.ArmPlayFontIsolation(true);
            EditorApplication.isPlaying = true;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Application.isPlaying || scene.name != "Test" || !SessionState.GetBool(Key + "Armed", false)) return;
            try
            {
                SaveManager[] saves = InScene<SaveManager>(scene).ToArray();
                if (saves.Length == 0) throw new InvalidOperationException("No SaveManager exists for isolation.");
                foreach (SaveManager save in saves) save.ConfigureDirectory(SessionState.GetString(Key + "Saves", ""));
            }
            catch (Exception exception)
            {
                foreach (BootStrap bootstrap in InScene<BootStrap>(scene)) bootstrap.enabled = false;
                SessionState.SetString(Key + "SafetyError", exception.Message);
            }
            if (SessionState.GetBool(Key + "Started", false)) return;
            SessionState.SetBool(Key + "Started", true);
            RunAsync(scene).Forget(Debug.LogException);
        }

        private static async UniTask RunAsync(Scene scene)
        {
            Checks.Clear();
            try
            {
                Check(string.IsNullOrEmpty(SessionState.GetString(Key + "SafetyError", "")), "save isolation configured before Bootstrap.Start");
                BootStrap bootstrap = InScene<BootStrap>(scene).Single();
                var startup = Read<InGameUIStartup>(bootstrap, "_uiStartup");
                var flow = Read<GameFlowController>(bootstrap, "_gameFlowController");
                var waves = Read<WaveController>(bootstrap, "_waveController");
                var shop = Read<ShopManager>(bootstrap, "_shopManager");
                var items = Read<ConsumableItemManager>(bootstrap, "_consumableItemManager");
                var wallet = Read<RunCurrencyManager>(bootstrap, "_runCurrencyManager");
                var artifacts = Read<ArtifactManager>(bootstrap, "_artifactManager");
                var view = Read<ShopView>(startup, "_shopView");
                var ui = startup.UIManager;
                var screen = Read<UIScreen>(view, "_screen");
                var leave = Read<Button>(view, "_leaveButton");
                await WaitFor(() => bootstrap.IsUIConnected && flow.CanEnterBuildMode(), "Bootstrap initialization");
                Check(shop.IsInitialized && items.IsInitialized && items.isActiveAndEnabled &&
                    InScene<SaveManager>(scene).All(save => save.DirectoryPath == SessionState.GetString(Key + "Saves", "")),
                    "Bootstrap connects initialized shop, consumable manager and isolated saves");

                // 생성된 상점 노드로 런타임 위치만 옮긴다. 전투 결과를 주입하고 실제 승리 처리 경로를 실행한다.
                GameFlowSaveData position = flow.CaptureSaveData();
                int shopIndex = position.Node.nodes.FindIndex(node => node.postBattleEvent == PostBattleEventType.Shop);
                Check(shopIndex >= 0 && shopIndex < position.Node.nodes.Count - 1, "generated quarter contains a non-final shop node");
                position.Node.currentWave = shopIndex;
                flow.RestoreSaveData(position);
                Invoke(flow, "NotifyNodeChanged");
                flow.StagingTime = 0;
                Invoke(flow, "ChangePhase", GamePhase.Battle);
                int wave = flow.CurrentWave;
                UniTask resolution = flow.ResolveBattleAsync(ResultType.Victory);
                var reward = Read<ArtifactRewardPresenter>(startup, "_artifactSelectionUI");
                var rewardView = Read<ArtifactRewardView>(reward, "_panel");
                await WaitFor(() => rewardView.IsVisible || view.IsPending, "reward or shop popup");
                if (rewardView.IsVisible)
                {
                    Array cards = Read<Array>(rewardView, "_cards");
                    Click(Read<Button>(cards.GetValue(0), "Button"));
                    Click(Read<Button>(rewardView, "_confirmButton"));
                }
                await WaitFor(() => view.IsPending && screen.IsVisible, "shop event entry");
                Check(flow.CurPhase == GamePhase.Store && shop.HasStock && shop.PurchaseSlots.Count > 0 &&
                    shop.ConsumableSlots.Count > 0 && ui.TopPopup == screen && ui.HasBlockingPopup,
                    "real victory event opens registered shop with generated artifact and consumable stock");
                await UniTask.NextFrame();
                await UniTask.NextFrame();
                Check(flow.CurrentWave == wave && resolution.Status == UniTaskStatus.Pending &&
                    !Read<ContinueView>(startup, "_continueUI").IsPending && !ui.CloseTopPopup(),
                    "shop waits on its own exit, preserves the node and rejects Escape close");
                Check(!Read<Button>(Read<GameHudView>(startup, "_hudView"), "_waveStartButton").IsInteractable(),
                    "shop blocks HUD start input");

                await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
                Directory.CreateDirectory("Logs");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("Logs/ShopScreen.png"));
                await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
                await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
                await ValidatePurchases(shop, artifacts, items, wallet, view);
                Click(leave);
                await WaitFor(() => resolution.Status != UniTaskStatus.Pending, "shop exit resolution");
                await resolution;
                Check(!view.IsPending && !screen.IsVisible && flow.CanEnterBuildMode() && flow.CurrentWave == wave + 1,
                    "Leave closes shop and real Flow advances exactly one node into Preparation");

                using (var cancellation = new CancellationTokenSource())
                {
                    UniTask pending = shop.OpenShopAsync(cancellation.Token);
                    Check(view.IsPending && screen.IsVisible, "shop can reopen with its current stock");
                    bool rejected = false;
                    try { await shop.OpenShopAsync(CancellationToken.None); }
                    catch (InvalidOperationException) { rejected = true; }
                    Check(rejected && view.IsPending, "overlapping open is rejected without replacing the first request");
                    cancellation.Cancel();
                    Check(await WasCanceled(pending) && !view.IsPending && !screen.IsVisible,
                        "token cancellation clears the popup and wait state");
                }
                UniTask external = shop.OpenShopAsync(CancellationToken.None);
                Check(ui.ClosePopup(UIId.Shop, UICloseReason.Replaced) && await WasCanceled(external) && !view.IsPending,
                    "external popup close cancels the shop request");
                UniTask reopened = shop.OpenShopAsync(CancellationToken.None);
                Click(leave);
                await reopened;
                Check(!view.IsPending && !screen.IsVisible, "reopen after external close completes normally");
                UniTask ended = shop.OpenShopAsync(CancellationToken.None);
                Check(shop.TryEndRun() && await WasCanceled(ended) && !view.IsPending && !screen.IsVisible && !shop.IsInitialized,
                    "TryEndRun cancels pending UI before removing shop state");
                SetResult("PASS (" + Checks.Count + " checks)\n" + string.Join("\n", Checks) +
                    "\nFixture: generated shop node, runtime position change and injected victory result; combat simulation is outside this test. " +
                    "Purchases use catalog items and a runtime-only stock snapshot; original assets, source data and player saves are untouched. " +
                    "Play Mode remains open for inspection.");
            }
            catch (Exception exception)
            {
                SetResult("FAIL after " + Checks.Count + " checks\n" + string.Join("\n", Checks) + "\n" + exception);
                Debug.LogError("[ShopUIValidation] " + LastResult);
            }
        }

        private static async UniTask ValidatePurchases(ShopManager shop, ArtifactManager artifacts,
            ConsumableItemManager items, RunCurrencyManager wallet, ShopView view)
        {
            ArtifactData[] choices = artifacts.ArtifactCatalog.Artifacts
                .Where(item => StackCount(artifacts, item.Id) < item.MaxStacks && item.ConsumableSlotEffects.Count == 0)
                .Take(2).ToArray();
            Check(choices.Length == 2 && items.Catalog.Items.Count > 0, "catalog contains suitable purchase fixture items");
            ConsumableItemData consumable = items.Catalog.Items[0];
            var stock = new ShopSaveData { HasStock = true };
            foreach (ArtifactData artifact in choices)
                stock.Artifacts.Add(new ShopPurchaseSaveEntry { ItemId = artifact.Id, Currency = CurrencyType.Gold, Price = 100 });
            stock.Consumables.Add(new ShopPurchaseSaveEntry { ItemId = consumable.Id, Currency = CurrencyType.Gold, Price = 70 });
            shop.RestoreSaveData(stock);
            var cards = Read<ShopItemCardView[]>(view, "_artifactCards");
            var itemCard = Read<ShopItemCardView[]>(view, "_consumableCards")[0];
            Check(Read<TMP_Text>(cards[0], "_nameText").text == choices[0].DisplayName &&
                Read<TMP_Text>(itemCard, "_nameText").text == consumable.DisplayName &&
                !Read<Button>(cards[2], "_buyButton").gameObject.activeSelf,
                "current slot objects populate both product kinds and missing slots remain empty");

            int funds = wallet.GetBalance(CurrencyType.Gold);
            if (funds > 0) Check(wallet.TrySpend(CurrencyType.Gold, funds), "temporarily empty runtime gold");
            int stacks = StackCount(artifacts, choices[0].Id);
            Click(Read<Button>(cards[0], "_buyButton"));
            Check(wallet.GetBalance(CurrencyType.Gold) == 0 && StackCount(artifacts, choices[0].Id) == stacks &&
                !shop.PurchaseSlots[0].IsPurchased && Read<TMP_Text>(view, "_feedbackText").text.Contains("부족"),
                "insufficient funds shows feedback without payment or artifact grant");
            Check(wallet.TryAdd(CurrencyType.Gold, funds + 1000), "fund runtime purchase fixture through the existing wallet");
            int gold = wallet.GetBalance(CurrencyType.Gold);
            int gem = wallet.GetBalance(CurrencyType.Gem);
            await PointerClick(Read<Button>(cards[0], "_buyButton"));
            Check(shop.PurchaseSlots[0].IsPurchased && StackCount(artifacts, choices[0].Id) == stacks + 1 &&
                wallet.GetBalance(CurrencyType.Gold) == gold - 100 && wallet.GetBalance(CurrencyType.Gem) == gem &&
                !Read<Button>(cards[0], "_buyButton").interactable &&
                Read<TMP_Text>(view, "_goldText").text.Contains((gold - 100).ToString("N0")),
                "real artifact purchase pays exactly the quote, grants one stack and updates balance/sold state");
            gold -= 100;
            Read<Button>(cards[0], "_buyButton").onClick.Invoke();
            Check(!shop.TryPurchase(shop.PurchaseSlots[0]) && wallet.GetBalance(CurrencyType.Gold) == gold &&
                StackCount(artifacts, choices[0].Id) == stacks + 1, "repeat callback and manager purchase cannot charge or grant twice");

            ConsumableItemSaveData inventory = items.CaptureSaveData();
            Check(items.TrySetCapacity(0) && !items.HasEmptySlot, "prepare a runtime inventory with no purchase space");
            int count = items.ItemCount;
            Click(Read<Button>(itemCard, "_buyButton"));
            Check(!shop.ConsumableSlots[0].IsPurchased && items.ItemCount == count &&
                wallet.GetBalance(CurrencyType.Gold) == gold && !string.IsNullOrEmpty(Read<TMP_Text>(view, "_feedbackText").text),
                "full consumable inventory reports failure without charging");
            items.RestoreSaveData(inventory);
            Check(items.HasEmptySlot, "restore runtime inventory capacity for consumable purchase");
            count = items.ItemCount;
            await PointerClick(Read<Button>(itemCard, "_buyButton"));
            Check(shop.ConsumableSlots[0].IsPurchased && items.ItemCount == count + 1 &&
                items.Slots.Any(slot => slot.Item == consumable) && wallet.GetBalance(CurrencyType.Gold) == gold - 70 &&
                !Read<Button>(itemCard, "_buyButton").interactable,
                "real consumable purchase grants the catalog item and charges its quote once");
            Read<Button>(itemCard, "_buyButton").onClick.Invoke();
            Check(!shop.TryPurchaseConsumable(shop.ConsumableSlots[0]) && items.ItemCount == count + 1 &&
                wallet.GetBalance(CurrencyType.Gold) == gold - 70, "repeat consumable purchase cannot charge or grant twice");
        }

        private static async UniTask PointerClick(Button button)
        {
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            Canvas.ForceUpdateCanvases();
            var events = EventSystem.current;
            var rect = (RectTransform)button.transform;
            Canvas canvas = button.GetComponentInParent<Canvas>();
            var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                    rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>();
            events.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject,
                "product purchase button is the top pointer raycast target");
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static async UniTask<bool> WasCanceled(UniTask task)
        {
            await WaitFor(() => task.Status != UniTaskStatus.Pending, "request cancellation");
            try { await task; return false; }
            catch (OperationCanceledException) { return true; }
        }

        private static async UniTask WaitFor(Func<bool> condition, string label)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (!condition())
            {
                if (!EditorApplication.isPlaying) throw new OperationCanceledException("Play stopped during " + label);
                if (Time.realtimeSinceStartupAsDouble >= deadline) throw new TimeoutException(label + " timed out.");
                await UniTask.Yield();
            }
        }

        private static int StackCount(ArtifactManager manager, string id) =>
            manager.TryGetById(id, out ArtifactInstance instance) ? instance.StackCount : 0;
        private static IEnumerable<T> InScene<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
        private static T Read<T>(object owner, string name)
        {
            object result = owner?.GetType().GetField(name, Fields)?.GetValue(owner);
            if (result == null) throw new InvalidOperationException(owner?.GetType().Name + "." + name + " is missing.");
            return (T)result;
        }
        private static object Invoke(object owner, string name, params object[] arguments) =>
            owner.GetType().GetMethod(name, Fields).Invoke(owner, arguments);
        private static void Click(Button button)
        {
            if (!button.gameObject.activeInHierarchy || !button.IsInteractable())
                throw new InvalidOperationException("Expected button is not interactable: " + button.name);
            button.onClick.Invoke();
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Checks.Add("PASS: " + message);
        }
        private static void SetResult(string result)
        {
            SessionState.SetString(Key + "Result", result);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(Output)));
            File.WriteAllText(Output, result, Encoding.UTF8);
        }
    }
}
#endif
