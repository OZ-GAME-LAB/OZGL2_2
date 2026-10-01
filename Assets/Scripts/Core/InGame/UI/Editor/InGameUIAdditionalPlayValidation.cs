#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using OZGL.KDH;
using TMPro;
using UnityEditor;
using UnityEngine;

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
            public BuildingUIPresenter Building;
            public BuildingCatalogView Catalog;
            public BuildingActionView Actions;
            public GameHudView Hud;
            public HudPresenter HudPresenter;
            public RunFlowPresenter RunFlow;
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
                Building = Read<BuildingUIPresenter>(ui, "_building");
                Catalog = Read<BuildingCatalogView>(Building, "_catalog");
                Actions = Read<BuildingActionView>(Building, "_actions");
                Hud = Read<GameHudView>(ui, "_hud");
                HudPresenter = Read<HudPresenter>(ui, "_hudPresenter");
                RunFlow = Read<RunFlowPresenter>(ui, "_flow");
                Reward = Read<ArtifactRewardPresenter>(ui, "_artifact");
                RewardView = Read<ArtifactRewardView>(Reward, "_panel");
                Flow = Read<GameFlowController>(RunFlow, "_flow");
                Waves = Read<WaveController>(RunFlow, "_waves");
                Wallet = Read<RunCurrencyManager>(RunFlow, "_wallet");
                Artifacts = Read<ArtifactManager>(RunFlow, "_artifacts");
                Gate = Read<TestWaitingScript>(RunFlow, "_contentGate");
                Controller = Read<BuildingBuildController>(Building, "_controller");
                Core = Read<BuildingCoreProgress>(Building, "_coreProgress");
                Slots = Read<BuildingSlot[]>(Building, "_slots");
            }

            public BuildingActionViewData Quote => Read<BuildingActionViewData>(Actions, "_data");
            public UIScreen Screen(UIId id)
            {
                if (!UI.TryGetScreen(id, out UIScreen screen)) throw new InvalidOperationException("Missing screen " + id);
                return screen;
            }
            public void InitializeUI() => UI.Initialize(Wallet, Waves, Flow, Artifacts, Core, Gate, Controller, Slots);
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
                await ValidateReinitialization(c);
                await ValidateOriginalUpgrades(c);
                await ValidateContentFixture(c, GamePhase.Event, PostBattleEventType.Event);
                await ValidateContentFixture(c, GamePhase.Store, PostBattleEventType.Shop);
                await ValidateEmptyRewardFixture(c);
                Check(c.Flow.CanEnterBuildMode() && !c.UI.HasModalOpen &&
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

        private static async UniTask ValidateReinitialization(Context c)
        {
            object wallet = Read<object>(c.Wallet, "_wallet");
            object inventory = c.Artifacts.Instances;
            var balances = Balance(c);
            Node node = c.Flow.CurrentNode;
            GamePhase phase = c.Flow.CurPhase;
            string subscribers = SubscriberSnapshot(c);

            c.UI.gameObject.SetActive(false);
            await UniTask.NextFrame();
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
            int shown = 0;
            Action<UIScreen> onShown = screen => shown++;
            c.Catalog.Popup.Shown += onShown;
            try
            {
                Select(c, empty);
                Check(shown == 1 && c.Building.SelectedSlot == empty && c.Catalog.Popup.IsVisible,
                    "one world selection opens the catalog once after every reinitialization path");
            }
            finally
            {
                c.Catalog.Popup.Shown -= onShown;
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
            AddSubscribers(lines, c.Hud, "ContinueRequested");
            AddSubscribers(lines, c.Hud, "RestartRequested");
            AddSubscribers(lines, c.Reward, "Completed");
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
            if (core) Check(next.CoreLevel > previous.CoreLevel && c.Core.CurrentLevel == next.CoreLevel,
                "core progress observes the higher original tier");
            await UniTask.NextFrame();
            if (core) Check(c.Core.CurrentCore == slot.CurrentBuilding,
                "destroying the previous core does not unregister the replacement core");
        }

        private static async UniTask ValidateContentFixture(Context c, GamePhase phase, PostBattleEventType content)
        {
            Check(c.Flow.CanEnterBuildMode() && !c.Gate.IsWaitingForPostBattleContent && !c.Artifacts.IsSelectingReward,
                phase + " UI fixture starts without a pending game operation");
            GamePhase previous = c.Flow.CurPhase;
            Node node = c.Flow.CurrentNode;
            var balance = Balance(c);
            using (var cancellation = new CancellationTokenSource())
            {
                UniTask pending = c.Gate.WaitPostBattleContentAsync(new Node(node.WaveNumber, node.Preset, content), cancellation.Token);
                bool observed = false;
                try
                {
                    Note("UI FIXTURE", phase + " uses a temporary CurPhase and RunFlowPresenter notification only; the real node and global PhaseChanged event are not modified/published.");
                    Set(c.Flow, "_curPhase", phase);
                    Invoke(c.RunFlow, "HandlePhaseChanged", phase);
                    Check(c.Gate.IsWaitingForPostBattleContent && c.Screen(UIId.WaveReward).IsVisible,
                        phase + " displays a continuation prompt for the real content gate");
                    Check(!c.UI.CloseTop() && c.Screen(UIId.WaveReward).IsVisible,
                        phase + " mandatory prompt rejects ESC CloseTop");
                    Click(Read<UnityEngine.UI.Button>(c.Hud, "_continueButton"), phase + " Continue");
                    await WaitFor(() => pending.Status != UniTaskStatus.Pending, phase + " content completion");
                    observed = true;
                    await pending;
                    Check(!c.Gate.IsWaitingForPostBattleContent && !c.Screen(UIId.WaveReward).IsVisible,
                        phase + " button completes the current gate and closes its prompt");
                }
                finally
                {
                    cancellation.Cancel();
                    if (!observed) await pending.SuppressCancellationThrow();
                    RestoreFixturePhase(c, previous);
                }
            }
            Check(ReferenceEquals(c.Flow.CurrentNode, node) && Balance(c) == balance,
                phase + " fixture preserves actual node progression and currency");
        }

        private static async UniTask ValidateEmptyRewardFixture(Context c)
        {
            Check(c.Flow.CanEnterBuildMode() && !c.Artifacts.IsSelectingReward,
                "empty reward fixture starts outside an actual selection");
            var balance = Balance(c);
            object inventory = c.Artifacts.Instances;
            int inventoryCount = c.Artifacts.Instances.Count;
            GamePhase phase = c.Flow.CurPhase;
            bool toggle = Read<bool>(c.Gate, "_toggle");
            UniTask<bool> selection = c.Artifacts.TestSelectAndApplyAsync(Array.Empty<ArtifactData>(), CancellationToken.None);
            Check(selection.Status != UniTaskStatus.Pending && await selection && !c.Artifacts.IsSelectingReward,
                "existing ArtifactManager empty-candidate API completes immediately without opening selection");

            using (var cancellation = new CancellationTokenSource())
            {
                UniTask gate = c.Gate.WaitToggle(cancellation.Token);
                bool observed = false;
                try
                {
                    Note("UI FIXTURE", "Reward uses the existing empty-candidate API and a temporary presenter-only phase; global Reward is not published, so no fake wave currency is granted.");
                    Set(c.Flow, "_curPhase", GamePhase.Reward);
                    Invoke(c.RunFlow, "HandlePhaseChanged", GamePhase.Reward);
                    Check(c.Screen(UIId.WaveReward).IsVisible && !c.RewardView.IsVisible,
                        "empty candidate reward still shows its initial continuation prompt");
                    int frame = Time.frameCount;
                    Click(Read<UnityEngine.UI.Button>(c.Hud, "_continueButton"), "empty reward Continue");
                    Check(gate.Status == UniTaskStatus.Pending && !c.RewardView.IsVisible,
                        "empty reward Continue preserves the required next-frame handoff");
                    Check(Read<bool>(c.RunFlow, "_rewardAcknowledged"),
                        "Continue marks the reward prompt acknowledged before reinitialization");
                    c.InitializeUI();
                    Check(c.UI.IsReady && c.Screen(UIId.WaveReward).IsVisible &&
                        !c.RewardView.IsVisible && gate.Status == UniTaskStatus.Pending,
                        "Initialize during an acknowledged reward restores its prompt without completing the gate");
                    Check(!Read<bool>(c.RunFlow, "_rewardAcknowledged") &&
                        !Read<bool>(c.RunFlow, "_pendingArtifactOpen"),
                        "reward reinitialization resets both presenter handoff flags");
                    frame = Time.frameCount;
                    Click(Read<UnityEngine.UI.Button>(c.Hud, "_continueButton"), "restored empty reward Continue");
                    await WaitFor(() => gate.Status != UniTaskStatus.Pending, "empty reward gate completion");
                    observed = true;
                    await gate;
                    Check(Time.frameCount > frame && !c.RewardView.IsVisible && !c.Screen(UIId.WaveReward).IsVisible,
                        "next-frame empty candidate handling completes the real gate without an artifact popup");
                }
                finally
                {
                    cancellation.Cancel();
                    if (!observed) await gate.SuppressCancellationThrow();
                    Set(c.Gate, "_toggle", toggle);
                    RestoreFixturePhase(c, phase);
                }
            }
            Check(Balance(c) == balance && ReferenceEquals(c.Artifacts.Instances, inventory) &&
                c.Artifacts.Instances.Count == inventoryCount,
                "empty reward fixture grants neither extra currency nor an artifact");
        }

        private static void RestoreFixturePhase(Context c, GamePhase phase)
        {
            if (c.Flow == null) return;
            Set(c.Flow, "_curPhase", phase);
            Invoke(c.RunFlow, "HandlePhaseChanged", phase);
            c.HudPresenter.Refresh();
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
