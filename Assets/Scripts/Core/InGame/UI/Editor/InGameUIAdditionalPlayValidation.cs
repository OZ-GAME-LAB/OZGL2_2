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
using OZGL.KDH;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.InGame.Editor
{
    /// <summary>
    /// 별도 Play 실행에서 원본 데이터 업그레이드와 UI 재초기화를 검증한다.
    /// Event/Store/후보 없음은 UI 계약 fixture이며 실제 노드 추첨 검증과 구분해 기록한다.
    /// 씬, 프리팹, ScriptableObject 원본, 게임 저장 파일을 저장하지 않는다.
    /// </summary>
    public static class InGameUIAdditionalPlayValidation
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly StringBuilder Report = new StringBuilder();
        private static bool _running;
        private static int _checks;
        public static string LastResult { get; private set; } = "Not run";
        public static void Run() => RunAsync().Forget(Debug.LogException);

        private sealed class Context
        {
            public InGameUIManager UI;
            public BuildingUIConnection Building;
            public ContinueView Continue;
            public BootStrap Bootstrap;
            public BuildingCatalogView Catalog;
            public BuildingActionView Actions;
            public GameHudView Hud;
            public HudPresenter HudPresenter;

            public ArtifactRewardPresenter Reward;
            public ArtifactRewardView RewardView;
            public GameFlowController Flow;
            public WaveController Waves;
            public RunCurrencyManager Wallet;
            public ArtifactManager Artifacts;
            public TestWaitingScript Gate;
            public BuildingBuildController Controller;
            public BuildingCoreProgress Core;
            public BuildingSlot[] Slots;

            public Context(InGameUIManager ui)
            {
                UI = ui;
                Bootstrap = ui.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<BootStrap>(true)).Single();
                var startup = Read<InGameUIStartup>(Bootstrap, "_uiStartup");
                Building = Read<BuildingUIConnection>(startup, "_buildingUIConnection");
                var buildingView = Read<BuildingUIPresenter>(startup, "_buildingUI");
                Catalog = Read<BuildingCatalogView>(buildingView, "_catalog");
                Actions = Read<BuildingActionView>(buildingView, "_actions");
                Hud = Read<GameHudView>(startup, "_hudView");
                Continue = Read<ContinueView>(startup, "_continueUI");
                Reward = Read<ArtifactRewardPresenter>(startup, "_artifactSelectionUI");
                RewardView = Read<ArtifactRewardView>(Reward, "_panel");
                Flow = Read<GameFlowController>(Bootstrap, "_gameFlowController");
                Waves = Read<WaveController>(Bootstrap, "_waveController");
                Wallet = Read<RunCurrencyManager>(Bootstrap, "_runCurrencyManager");
                Artifacts = Read<ArtifactManager>(Bootstrap, "_artifactManager");
                Controller = Read<BuildingBuildController>(Bootstrap, "_buildController");
                Slots = ui.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<BuildingSlot>(true)).ToArray();
                HudPresenter = Read<HudPresenter>(startup, "_hudPresenter");
                Gate = Read<TestWaitingScript>(Bootstrap, "_testScript");
                Core = Read<BuildingCoreProgress>(Bootstrap, "_buildingCoreProgress");
            }

            public BuildingActionViewData Quote => Read<BuildingActionViewData>(Actions, "_data");
            public UIScreen Screen(UIId id)
            {
                if (!UI.TryGetScreen(id, out UIScreen screen)) throw new InvalidOperationException("Missing screen " + id);
                return screen;
            }
            public void InitializeUI() => Bootstrap.InitializeUI(Slots);
        }

        private static async UniTask RunAsync()
        {
            if (_running) throw new InvalidOperationException("Additional live validation is already running.");
            _running = true;
            _checks = 0;
            Report.Clear();
            Note("SETUP", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " Additional Play validation");
            try
            {
                if (!EditorApplication.isPlaying) throw new InvalidOperationException("Run in the playing Test scene.");
                InGameUIManager ui = FindManager();
                await WaitFor(() => ui != null && ui.IsReady, "Bootstrap initialization");
                var c = new Context(ui);
                await WaitFor(c.Flow.CanEnterBuildMode, "Preparation");
                await ValidatePointerAndEscape(c);
                await ValidateReinitialization(c);
                await ValidateBuildingCapacity(c);
                await ValidateOriginalUpgrades(c);
                await ValidateContentFixture(c, GamePhase.Event, PostBattleEventType.Event);
                await ValidateContentFixture(c, GamePhase.Store, PostBattleEventType.Shop);
                await ValidateEmptyRewardFixture(c);
                Check(c.Flow.CanEnterBuildMode() && !c.UI.HasBlockingPopup &&
                    Read<UnityEngine.UI.Button>(c.Hud, "_waveStartButton").IsInteractable(),
                    "all fixtures restore a usable Preparation HUD");
                Note("LIMIT", "Event/Store and empty candidate tests verify UI forwarding with temporary runtime fixtures, not generated node probabilities or real loot exhaustion.");
                Note("END", "Play remains running. Core/building upgrades are runtime changes; no scene, asset, or game-save write occurred.");
                LastResult = "PASS (" + _checks + " checks)\n" + Report;
                Debug.Log("[InGameUIAdditionalPlayValidation] " + LastResult);
            }
            catch (Exception exception)
            {
                LastResult = "FAIL after " + _checks + " checks\n" + Report + exception;
                Debug.LogError("[InGameUIAdditionalPlayValidation] " + LastResult);
            }
            finally
            {
                try
                {
                    string path = Path.GetFullPath(Path.Combine(Application.dataPath,
                        "../PersonalDocs/UIValidation/play-additional-tests.txt"));
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.AppendAllText(path, LastResult + Environment.NewLine + Environment.NewLine, Encoding.UTF8);
                }
                catch (Exception exception) { Debug.LogException(exception); }
                _running = false;
            }
        }

        private static async UniTask ValidatePointerAndEscape(Context c)
        {
            EventSystem events = EventSystem.current;
            Check(events != null, "playing Test has an active EventSystem");
            BuildingSlot empty = Array.Find(c.Slots, IsEmpty);
            Check(empty != null, "pointer validation has a real empty construction slot");
            Select(c, empty);
            UIScreen catalog = c.Screen(UIId.BuildingCatalog);
            UIScreen detail = c.Screen(UIId.Detail);
            Check(catalog.IsVisible && c.UI.OpenPopup(UIId.Detail), "detail stacks over the real building catalog");
            var detailView = detail.GetComponentInChildren<TextPopupView>(true);
            detailView.SetContent("입력 검증", "공용 상세", "EventSystem 경로 검사");
            await WaitForCanvasRender();
            var close = detailView.GetComponentInChildren<CloseUtility>(true);
            Button closeButton = Read<Button>(close, "_button");
            var pointer = PointerAt(events, closeButton);
            var hits = new List<RaycastResult>();
            events.RaycastAll(pointer, hits);
            Note("RAYCAST", DescribeRaycast(pointer, hits));
            Check(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(detail.Root.transform),
                "EventSystem raycast at the actual detail close button hits the top popup first");
            GameObject clicked = ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Check(clicked == closeButton.gameObject && !detail.IsVisible && catalog.IsVisible && c.UI.TopPopup == catalog,
                "ExecuteEvents pointerClick follows the saved CloseUtility and returns to the real parent");

            c.UI.ReplacePopup(UIId.ArtifactReward);
            UIScreen required = c.Screen(UIId.ArtifactReward);
            await WaitForCanvasRender();
            Button start = Read<Button>(c.Hud, "_waveStartButton");
            hits.Clear();
            pointer = PointerAt(events, start);
            events.RaycastAll(pointer, hits);
            Note("RAYCAST", DescribeRaycast(pointer, hits));
            Check(!start.IsInteractable() && !hits.Exists(hit => hit.gameObject == start.gameObject ||
                hit.gameObject.transform.IsChildOf(start.transform)),
                "mandatory popup prevents HUD start from receiving UI raycasts");
            Check(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(required.Root.transform),
                "the existing mandatory background still receives pointer hits over the HUD area");
            c.UI.OpenPopup(UIId.Detail);
            Check(c.UI.HasBlockingPopup && !start.IsInteractable(),
                "a normal detail above mandatory reward does not unlock the HUD");
            InGameUIValidation.SimulateEscape(c.UI);
            Check(!detail.IsVisible && required.IsVisible && c.UI.TopPopup == required,
                "synthetic InputSystem Escape reaches the manager Update branch in Play Mode");
            InGameUIValidation.SimulateEscape(c.UI);
            Check(required.IsVisible, "a second Escape preserves the mandatory reward");
            c.UI.ClosePopup(UIId.ArtifactReward, UICloseReason.Completed);
            c.Building.ClearSelection();
            Note("INPUT", "Pointer tests use EventSystem.RaycastAll plus ExecuteEvents.pointerClickHandler. Escape uses temporary Keyboard state plus manager Update, not physical hardware or natural pointer movement.");
        }

        private static async UniTask WaitForCanvasRender()
        {
            // Graphic.depth는 첫 Canvas 렌더 후 유효하다. Update 직후 한 프레임만 기다리면
            // 갓 열린 팝업이 아직 Raycaster에 등록되지 않은 시점을 검사할 수 있다.
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            Canvas.ForceUpdateCanvases();
        }

        private static string DescribeRaycast(PointerEventData pointer, List<RaycastResult> hits)
        {
            var report = new StringBuilder();
            report.Append("Screen ").Append(Screen.width).Append('x').Append(Screen.height)
                .Append(", point ").Append(pointer.position).Append(", hits ").Append(hits.Count);
            for (int i = 0; i < Math.Min(hits.Count, 5); i++)
                report.Append(" | ").Append(i).Append(':').Append(hits[i].gameObject.name)
                    .Append(" depth=").Append(hits[i].depth).Append(" order=").Append(hits[i].sortingOrder);
            return report.ToString();
        }

        private static PointerEventData PointerAt(EventSystem events, Button button)
        {
            RectTransform rect = (RectTransform)button.transform;
            Canvas canvas = button.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return new PointerEventData(events)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center))
            };
        }

        private static async UniTask ValidateBuildingCapacity(Context c)
        {
            int limit = c.Controller.BuildLimit;
            Check(limit > 0, "actual initial core has a configured positive construction limit: " + limit);
            int originalCount = c.Controller.BuiltCount;
            var built = new List<BuildingSlot>();
            BuildingSlot reserved = c.Slots.LastOrDefault(IsEmpty);
            Check(reserved != null, "an empty slot is reserved before filling construction capacity");
            BuildingInteractionState heldState = c.Controller.QueryInteraction(reserved);
            BuildingData heldCandidate = heldState.Candidates.First(candidate => candidate.HasValidCost).Data;
            BuildingInteractionTarget heldTarget = heldState.Target;
            try
            {
                while (c.Controller.HasBuildCapacity)
                {
                    BuildingSlot target = Array.Find(c.Slots, slot => slot != reserved && IsEmpty(slot));
                    Check(target != null, "a real empty slot remains while filling the unchanged core limit");
                    Select(c, target);
                    string id = CapacityCandidate(c);
                    CatalogClick(c, id);
                    Fund(c, c.Quote.Build);
                    int previousCount = c.Controller.BuiltCount;
                    Click(ActionButton(c, "_build"), "build capacity fixture");
                    Check(target.IsOccupied && c.Controller.BuiltCount == previousCount + 1,
                        "one actual build increments the ordinary-building census once");
                    built.Add(target);
                    await UniTask.NextFrame();
                }
                Check(c.Controller.BuiltCount == limit && !c.Controller.HasBuildCapacity,
                    "the configured core limit is reached without counting the core itself");
                BuildingSlot extra = reserved;
                Check(extra != null, "an empty slot exists for the over-limit rejection check");
                Select(c, extra);
                CatalogClick(c, heldCandidate.BuildingId);
                Fund(c, c.Quote.Build);
                var balance = Balance(c);
                ActionButton(c, "_build").onClick.Invoke();
                Check(!extra.IsOccupied && c.Controller.BuiltCount == limit && Balance(c) == balance && !c.Actions.IsRequestPending,
                    "over-limit UI request cannot occupy a slot, charge currency, or leave its request pending");
                BuildingInteractionResult capped = c.Controller.TryExecuteInteraction(heldTarget,
                    BuildingInteractionAction.Build, heldCandidate);
                Check(capped.Failure == BuildingInteractionFailure.BuildCapacity && !capped.MayHaveChangedState &&
                    !extra.IsOccupied && Balance(c) == balance && c.Controller.BuiltCount == limit,
                    "a target captured before capacity fills is rejected again by the execution API");
                if (built.Count > 0)
                {
                    BuildingSlot released = built[built.Count - 1];
                    SelectOccupied(c, released);
                    Click(ActionButton(c, "_dismantle"), "release capacity fixture");
                    await UniTask.NextFrame();
                    Check(!released.IsOccupied && c.Controller.BuiltCount == limit - 1 && c.Controller.HasBuildCapacity,
                        "demolition releases exactly one construction capacity");
                    Select(c, extra);
                    CatalogClick(c, CapacityCandidate(c));
                    Fund(c, c.Quote.Build);
                    Click(ActionButton(c, "_build"), "reuse released capacity");
                    Check(extra.IsOccupied && c.Controller.BuiltCount == limit,
                        "the freed capacity permits one new building through the UI");
                    built.Add(extra);
                }
            }
            finally
            {
                foreach (BuildingSlot slot in built)
                    if (slot != null && slot.IsOccupied) c.Controller.TryDemolish(slot);
                c.Building.ClearSelection();
            }
            await UniTask.NextFrame();
            Check(c.Controller.BuiltCount == originalCount,
                "capacity fixture restores the original occupied-slot census before upgrades");
        }

        private static string CapacityCandidate(Context c)
        {
            var data = Read<Dictionary<string, BuildingData>>(c.Building, "_byId");
            foreach (BuildingCatalogItem item in Read<List<BuildingCatalogItem>>(c.Catalog, "_items"))
                if (item.GoldCost.HasValue && data[item.Id].HasWorldVisual) return item.Id;
            throw new InvalidOperationException("No unchanged valid building candidate for the capacity check.");
        }

        private static async UniTask ValidateReinitialization(Context c)
        {
            object wallet = Read<object>(c.Wallet, "_wallet");
            object inventory = c.Artifacts.Instances;
            var balances = Balance(c);
            Node node = c.Flow.CurrentNode;
            GamePhase phase = c.Flow.CurPhase;
            string subscribers = SubscriberSnapshot(c);

            EventSystem.current.SetSelectedGameObject(Read<Button>(c.Hud, "_waveStartButton").gameObject);
            c.UI.gameObject.SetActive(false);
            await UniTask.NextFrame();
            Check(EventSystem.current.currentSelectedGameObject == null,
                "disabling the UI root clears the previous HUD keyboard selection");
            c.UI.gameObject.SetActive(true);
            await UniTask.NextFrame();
            AssertSameInitialization(c, wallet, inventory, balances, node, phase, subscribers, "root reactivation");

            c.InitializeUI();
            c.InitializeUI();
            await UniTask.NextFrame();
            AssertSameInitialization(c, wallet, inventory, balances, node, phase, subscribers, "two active Initialize calls");

            c.UI.gameObject.SetActive(false);
            try { c.InitializeUI(); }
            finally { c.UI.gameObject.SetActive(true); }
            await UniTask.NextFrame();
            AssertSameInitialization(c, wallet, inventory, balances, node, phase, subscribers, "inactive Initialize then activation");

            int notifications = 0;
            Action<CurrencyData, int, int> changed = (currency, oldValue, newValue) => notifications++;
            c.Wallet.BalanceChanged += changed;
            bool added = false;
            try
            {
                Check(c.Wallet.TryAdd(CurrencyType.Gold, 1), "temporary wallet notification probe adds one runtime Gold");
                added = true;
                Check(notifications == 1 && Read<TMP_Text>(c.Hud, "_goldText").text ==
                    c.Wallet.GetBalance(CurrencyType.Gold).ToString("N0"),
                    "a real balance event occurs once and updates the HUD after reinitialization");
            }
            finally
            {
                if (added && !c.Wallet.TrySpend(CurrencyType.Gold, 1))
                    throw new InvalidOperationException("Could not restore the one-Gold notification probe.");
                c.Wallet.BalanceChanged -= changed;
            }
            Check(notifications == 2 && Balance(c) == balances, "wallet notification probe restores the original balance exactly");

            BuildingSlot empty = Array.Find(c.Slots, IsEmpty);
            Check(empty != null, "reactivation probe has an actual empty slot");
            try
            {
                Select(c, empty);
                Check(c.UI.OpenPopupCount == 1 && c.UI.TopPopup == c.Catalog.Popup &&
                    c.Building.SelectedSlot == empty && c.Catalog.Popup.IsVisible,
                    "one world selection leaves exactly one catalog entry after every reinitialization path");
            }
            finally
            {
                c.Building.ClearSelection();
            }
        }

        private static void AssertSameInitialization(Context c, object wallet, object inventory,
            (int gold, int gems) balances, Node node, GamePhase phase, string subscribers, string label)
        {
            Check(c.UI.IsReady && c.Screen(UIId.Hud).IsVisible, label + " keeps the UI ready and HUD visible");
            Check(ReferenceEquals(wallet, Read<object>(c.Wallet, "_wallet")) &&
                ReferenceEquals(inventory, c.Artifacts.Instances) && Balance(c) == balances,
                label + " does not recreate the wallet/inventory or reset currency");
            Check(c.Flow.CurPhase == phase && ReferenceEquals(c.Flow.CurrentNode, node),
                label + " preserves the existing game phase and node");
            Check(SubscriberSnapshot(c) == subscribers,
                label + " preserves the exact event listener targets and counts");
        }

        private static string SubscriberSnapshot(Context c)
        {
            var lines = new List<string>();
            AddSubscribers(lines, c.Wallet, "BalanceChanged");
            AddSubscribers(lines, c.Flow, "PhaseChanged");
            AddSubscribers(lines, c.Flow, "QuarterDecisionRequested");
            AddSubscribers(lines, c.Waves, "WaveChanged");
            AddSubscribers(lines, c.Controller, "SlotSelected");
            AddSubscribers(lines, c.Controller, "SlotDeselected");
            AddSubscribers(lines, c.Hud, "WaveStartRequested");
            AddSubscribers(lines, c.RewardView, "_choiceRequested");
            AddSubscribers(lines, c.Actions, "_actionRequested");
            lines.Sort(StringComparer.Ordinal);
            return string.Join("\n", lines);
        }

        private static void AddSubscribers(List<string> lines, object source, string field)
        {
            FieldInfo info = source.GetType().GetField(field, Fields);
            if (info == null) throw new MissingFieldException(source.GetType().Name, field);
            var handlers = info.GetValue(source) as Delegate;
            if (handlers == null) return;
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                string target = handler.Target is UnityEngine.Object unityObject
                    ? unityObject.GetInstanceID().ToString()
                    : handler.Target == null ? "static" : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(handler.Target).ToString();
                lines.Add(source.GetType().Name + "." + field + "|" + target + "|" + handler.Method.DeclaringType + "." + handler.Method.Name);
            }
        }

        private static async UniTask ValidateOriginalUpgrades(Context c)
        {
            BuildingSlot core = Array.Find(c.Slots, slot => slot != null && c.Controller.IsCoreSlot(slot));
            Check(core != null && core.IsOccupied, "actual Test core is present");
            if (c.Core.CurrentLevel < 2)
            {
                SelectOccupied(c, core);
                await Upgrade(c, core, true);
                Check(c.Core.CurrentLevel >= 2, "original core upgrade unlocks core-level-2 requirements");
            }
            else Note("SETUP", "Core already meets level 2; no original data was altered to unlock upgrades.");

            var upgrades = new List<BuildingData>();
            BuildingSlot target = null;
            foreach (BuildingSlot slot in c.Slots)
            {
                if (slot == null || !slot.IsOccupied || c.Controller.IsCoreSlot(slot)) continue;
                slot.CurrentBuilding.Data.CollectUpgrades(upgrades, c.Core.CurrentLevel);
                if (upgrades.Count > 0) { target = slot; break; }
            }
            if (target == null)
            {
                target = Array.Find(c.Slots, IsEmpty);
                Check(target != null, "an empty slot is available for an original upgradable building");
                Select(c, target);
                var data = Read<Dictionary<string, BuildingData>>(c.Building, "_byId");
                string candidateId = null;
                foreach (BuildingCatalogItem item in Read<List<BuildingCatalogItem>>(c.Catalog, "_items"))
                {
                    if (!item.GoldCost.HasValue || !data[item.Id].HasWorldVisual) continue;
                    data[item.Id].CollectUpgrades(upgrades, c.Core.CurrentLevel);
                    if (upgrades.Count == 0) continue;
                    candidateId = item.Id;
                    break;
                }
                Check(candidateId != null, "actual catalog contains an unlocked configured upgrade path");
                CatalogClick(c, candidateId);
                Fund(c, c.Quote.Build);
                Click(ActionButton(c, "_build"), "build original upgrade candidate");
                Check(target.IsOccupied && target.CurrentBuilding.Data.BuildingId == candidateId,
                    "original candidate is built through its UI button");
                await UniTask.NextFrame();
            }
            SelectOccupied(c, target);
            await Upgrade(c, target, false);

            // 현재 Test 데이터의 core2 -> core3은 Gem 견적도 포함하므로 가능하면 함께 검증한다.
            SelectOccupied(c, core);
            if (c.Quote.Upgrade != null)
            {
                await Upgrade(c, core, true);
                Note("PASS", "Available next core tier was also tested with its unchanged configured currency cost.");
            }
            else Note("BRANCH", "Core has no further available tier; no synthetic upgrade data was created.");
            c.Building.ClearSelection();
        }

        private static async UniTask Upgrade(Context c, BuildingSlot slot, bool core)
        {
            BuildingData previous = slot.CurrentBuilding.Data;
            BuildingActionOffer quote = c.Quote.Upgrade;
            Check(quote != null, previous.BuildingId + " exposes its actual configured upgrade offer");
            Fund(c, quote);
            quote = c.Quote.Upgrade;
            BuildingData next = Read<BuildingData>(c.Building, "_candidate");
            BuildingResourceCost[] costs = c.Controller.GetUpgradeCost(slot, next);
            int gold = 0, gems = 0;
            foreach (BuildingResourceCost cost in costs)
                if (cost.type == BuildingResourceType.Gold) gold += cost.amount;
                else if (cost.type == BuildingResourceType.Gem) gems += cost.amount;
            Check(quote.CanExecute && quote.OptionId == next.BuildingId && quote.GoldAmount == gold && quote.GemAmount == gems,
                "upgrade UI quote matches the unchanged controller cost for " + previous.BuildingId + " -> " + next.BuildingId);
            var before = Balance(c);
            int requests = 0;
            BuildingInteractionTarget previousTarget = c.Controller.QueryInteraction(slot, next).Target;
            Action<BuildingActionRequest> requested = request => requests++;
            c.Actions.ActionRequested += requested;
            try { Click(ActionButton(c, "_upgrade"), "Upgrade " + previous.BuildingId); }
            finally { c.Actions.ActionRequested -= requested; }
            Check(requests == 1 && slot.IsOccupied && slot.CurrentBuilding.Data == next &&
                slot.CurrentBuilding.Data != previous, "one upgrade button click applies the next configured building once");
            Check(Balance(c) == (before.gold - gold, before.gems - gems),
                "upgrade deducts Gold " + gold + " and Gem " + gems + " exactly once");
            Check(c.Screen(UIId.BuildingInfo).IsVisible && c.Building.SelectedSlot == slot,
                "upgrade retains the original selected-slot and open-details behavior");
            Building replacement = slot.CurrentBuilding;
            var afterUpgrade = Balance(c);
            BuildingInteractionResult stale = c.Controller.TryExecuteInteraction(previousTarget,
                BuildingInteractionAction.Demolish);
            Check(stale.Failure == BuildingInteractionFailure.TargetChanged && !stale.MayHaveChangedState &&
                ReferenceEquals(slot.CurrentBuilding, replacement) && Balance(c) == afterUpgrade,
                "the old occupied target cannot demolish or refund the replacement building");
            if (core) Check(next.CoreLevel > previous.CoreLevel && c.Core.CurrentLevel == next.CoreLevel,
                "core progress observes the higher original tier");
            await UniTask.NextFrame();
            if (core) Check(c.Core.CurrentCore == slot.CurrentBuilding,
                "destroying the previous core does not unregister the replacement core");
        }

        private static async UniTask ValidateContentFixture(Context c, GamePhase phase, PostBattleEventType content)
        {
            var balance = Balance(c);
            Node node = c.Flow.CurrentNode;
            Note("UI FIXTURE", phase + " uses the explicit continue request; game phase/node are unchanged.");
            using (var cancellation = new CancellationTokenSource())
            {
                UniTask pending = c.Continue.ShowAsync(phase == GamePhase.Event ? "이벤트 진행 대기" : "상점 진행 대기", cancellation.Token);
                Check(c.Screen(UIId.WaveReward).IsVisible && !c.UI.CloseTopPopup(), phase + " request opens a required prompt");
                Click(Read<UnityEngine.UI.Button>(c.Continue, "_continueButton"), phase + " Continue");
                await pending;
                Check(!c.Screen(UIId.WaveReward).IsVisible, phase + " confirmation closes before returning");
            }
            Check(ReferenceEquals(c.Flow.CurrentNode, node) && Balance(c) == balance, phase + " fixture changes neither node nor currency");
        }

        private static async UniTask ValidateEmptyRewardFixture(Context c)
        {
            var balance = Balance(c);
            int count = c.Artifacts.Instances.Count;
            Check(await c.Artifacts.TestSelectAndApplyAsync(Array.Empty<ArtifactData>(), CancellationToken.None), "empty candidates complete without UI selection");
            Check(!c.RewardView.IsVisible && !c.Artifacts.IsSelectingReward && Balance(c) == balance && c.Artifacts.Instances.Count == count,
                "empty candidate contract grants no additional currency or artifact");
        }

        private static void Select(Context c, BuildingSlot slot) => Invoke(c.Controller, "NotifySlotSelected", slot);
        private static void SelectOccupied(Context c, BuildingSlot slot)
        {
            Select(c, slot);
            if (c.Catalog.Popup.IsVisible)
                CatalogClick(c, Read<List<BuildingCatalogItem>>(c.Catalog, "_items")[0].Id);
        }

        private static void CatalogClick(Context c, string id)
        {
            var items = Read<List<BuildingCatalogItem>>(c.Catalog, "_items");
            int index = items.FindIndex(item => item.Id == id);
            var cards = Read<Array>(c.Catalog, "_cards");
            if (index < 0 || cards.Length == 0) throw new InvalidOperationException("Missing actual catalog item " + id);
            while (c.Catalog.PageIndex < index / cards.Length)
                Click(Read<UnityEngine.UI.Button>(c.Catalog, "_next"), "catalog Next");
            var slot = Read<UIItemSlot>(cards.GetValue(index % cards.Length), "Slot");
            Click(Read<UnityEngine.UI.Button>(slot, "_button"), "candidate " + id);
        }

        private static UnityEngine.UI.Button ActionButton(Context c, string field) =>
            Read<UnityEngine.UI.Button>(Read<object>(c.Actions, field), "_button");

        private static void Fund(Context c, BuildingActionOffer offer)
        {
            if (offer == null) throw new InvalidOperationException("Missing configured action offer.");
            AddDeficit(c, CurrencyType.Gold, offer.GoldAmount);
            AddDeficit(c, CurrencyType.Gem, offer.GemAmount);
        }

        private static void AddDeficit(Context c, CurrencyType type, int cost)
        {
            int difference = Math.Max(0, cost - c.Wallet.GetBalance(type));
            if (difference == 0) return;
            if (!c.Wallet.TryAdd(type, difference)) throw new InvalidOperationException("Runtime test funding failed: " + type);
            Note("TEST CURRENCY", "Wallet.TryAdd(" + type + ", " + difference + ") funds only the actual upgrade/build deficit.");
        }

        private static (int gold, int gems) Balance(Context c) =>
            (c.Wallet.GetBalance(CurrencyType.Gold), c.Wallet.GetBalance(CurrencyType.Gem));

        private static bool IsEmpty(BuildingSlot slot) => slot != null && slot.isActiveAndEnabled &&
            !slot.IsOccupied && slot.TryGetComponent(out Collider2D collider) && collider.enabled;

        private static InGameUIManager FindManager()
        {
            InGameUIManager found = null;
            foreach (var ui in UnityEngine.Object.FindObjectsByType<InGameUIManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (ui.gameObject.scene.name != "Test") continue;
                if (found != null) throw new InvalidOperationException("Multiple active Test UI managers.");
                found = ui;
            }
            return found != null ? found : throw new InvalidOperationException("No active new UI manager in the playing Test scene.");
        }

        private static T Read<T>(object owner, string field)
        {
            FieldInfo info = owner?.GetType().GetField(field, Fields);
            if (info == null) throw new MissingFieldException(owner?.GetType().Name, field);
            object value = info.GetValue(owner);
            if (value == null) throw new InvalidOperationException(owner.GetType().Name + "." + field + " is missing.");
            return (T)value;
        }

        private static void Set(object owner, string field, object value)
        {
            FieldInfo info = owner.GetType().GetField(field, Fields);
            if (info == null) throw new MissingFieldException(owner.GetType().Name, field);
            info.SetValue(owner, value);
        }

        private static void Invoke(object owner, string method, params object[] arguments)
        {
            MethodInfo info = owner.GetType().GetMethod(method, Fields);
            if (info == null) throw new MissingMethodException(owner.GetType().Name, method);
            info.Invoke(owner, arguments);
        }

        private static void Click(UnityEngine.UI.Button button, string label)
        {
            if (button == null || !button.gameObject.activeInHierarchy || !button.IsInteractable())
                throw new InvalidOperationException(label + " button is inactive or blocked.");
            button.onClick.Invoke();
        }

        private static async UniTask WaitFor(Func<bool> condition, string label)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (!condition())
            {
                if (!EditorApplication.isPlaying) throw new OperationCanceledException("Play stopped during " + label);
                if (Time.realtimeSinceStartupAsDouble >= deadline) throw new TimeoutException(label + " exceeded 10 seconds.");
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            _checks++;
            Note("PASS", message);
        }

        private static void Note(string kind, string message)
        {
            Report.AppendLine(kind + ": " + message);
            LastResult = "RUNNING (" + _checks + " checks)\n" + Report;
        }
    }
}
#endif
