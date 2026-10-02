#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Cysharp.Threading.Tasks;
using Game.Cameras;
using Game.Core;
using OZGL.KDH;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI.InGame.Editor
{
    /// <summary>
    /// Test 씬의 실제 Play 상태에서 버튼부터 게임 매니저까지 연결을 검증한다.
    /// 실행 중인 런의 재화/건물/진행은 변경하지만 씬, 프리팹, 게임 저장 파일은 저장하지 않는다.
    /// 테스트가 끝나도 Play Mode를 유지한다. 실패 시 현재 화면을 그대로 조사할 수 있다.
    /// </summary>
    public static class InGameUIPlayValidation
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const double TimeoutSeconds = 10;
        private static readonly StringBuilder Report = new StringBuilder();
        private static bool _running;
        private static int _checks;
        private static int _limits;
        public static string LastResult { get; private set; } = "Not run";

        public static void Run() => Execute("Full integration", RunFull).Forget(Debug.LogException);
        public static void RunDefeatTest() => Execute("Defeat integration", RunDefeat).Forget(Debug.LogException);
        public static void RunRestartTest() => Execute("Restart integration", RunRestart).Forget(Debug.LogException);
        public static void RunContinueTest() => Execute("In-memory Continue contract", RunContinue).Forget(Debug.LogException);

        private sealed class Context
        {
            public InGameUIManager Manager;
            public BuildingUIConnection Building;
            public ContinueView Continue;
            public ShopView Shop;
            public BootStrap Bootstrap;
            public InGameUIStartup Startup;
            public BuildingCatalogView Catalog;
            public BuildingActionView Actions;
            public GameHudView Hud;
            public RunDecisionView Decision;
            public ArtifactRewardPresenter Reward;
            public ArtifactRewardView RewardView;
            public GameFlowController Flow;
            public WaveController Waves;
            public RunCurrencyManager Wallet;
            public ArtifactManager Artifacts;
            public BuildingBuildController Controller;
            public BuildingSlot[] Slots;
            public InGameCameraController Camera;

            public Context(InGameUIManager manager)
            {
                Manager = manager;
                Bootstrap = manager.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<BootStrap>(true)).Single();
                Startup = Read<InGameUIStartup>(Bootstrap, "_uiStartup");
                Building = Read<BuildingUIConnection>(Startup, "_buildingUIConnection");
                var buildingView = Read<BuildingUIPresenter>(Startup, "_buildingUI");
                Catalog = Read<BuildingCatalogView>(buildingView, "_catalog");
                Actions = Read<BuildingActionView>(buildingView, "_actions");
                Hud = Read<GameHudView>(Startup, "_hudView");
                Continue = Read<ContinueView>(Startup, "_continueUI");
                Shop = Read<ShopView>(Startup, "_shopView");
                Reward = Read<ArtifactRewardPresenter>(Startup, "_artifactSelectionUI");
                RewardView = Read<ArtifactRewardView>(Reward, "_panel");
                Flow = Read<GameFlowController>(Bootstrap, "_gameFlowController");
                Waves = Read<WaveController>(Bootstrap, "_waveController");
                Wallet = Read<RunCurrencyManager>(Bootstrap, "_runCurrencyManager");
                Artifacts = Read<ArtifactManager>(Bootstrap, "_artifactManager");
                Controller = Read<BuildingBuildController>(Bootstrap, "_buildController");
                Slots = manager.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<BuildingSlot>(true)).ToArray();
                Decision = Read<RunDecisionView>(Startup, "_runDecisionUI");
                Camera = Read<InGameCameraController>(Flow, "_cameraController");
            }

            public UIScreen Screen(UIId id)
            {
                if (!Manager.TryGetScreen(id, out UIScreen result))
                    throw new InvalidOperationException("Missing screen: " + id);
                return result;
            }

            public BuildingActionViewData Quote => Read<BuildingActionViewData>(Actions, "_data");
        }

        private static async UniTask Execute(string name, Func<Context, UniTask> test)
        {
            if (_running) throw new InvalidOperationException("A live UI validation is already running.");
            _running = true;
            _checks = 0;
            _limits = 0;
            Report.Clear();
            Report.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + name);
            Note("SETUP", "Live runtime changes are intentional. No scene/prefab/game save API is called.");
            try
            {
                if (!EditorApplication.isPlaying)
                    throw new InvalidOperationException("Enter Play Mode in the saved Test scene before running this helper.");
                InGameUIManager manager = FindManager();
                await WaitFor(() => manager != null && manager.IsReady, "Bootstrap UI initialization");
                var context = new Context(manager);
                Check(context.Wallet.IsInitialized && context.Artifacts.IsInitialized,
                    "Bootstrap initialized the injected currency and artifact managers");
                await test(context);
                LastResult = "PASS (" + _checks + " checks, " + _limits + " limits)\n" + Report;
                Debug.Log("[InGameUIPlayValidation] " + LastResult);
            }
            catch (Exception exception)
            {
                Report.AppendLine(exception.ToString());
                LastResult = "FAIL after " + _checks + " checks\n" + Report;
                Debug.LogError("[InGameUIPlayValidation] " + LastResult);
            }
            finally
            {
                // 테스트 기록만 저장한다. Application.persistentDataPath나 게임 저장 서비스는 사용하지 않는다.
                try
                {
                    string path = Path.GetFullPath(Path.Combine(Application.dataPath,
                        "../PersonalDocs/UIValidation/play-tests.txt"));
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.AppendAllText(path, LastResult + Environment.NewLine + Environment.NewLine, Encoding.UTF8);
                }
                catch (Exception exception) { Debug.LogException(exception); }
                _running = false;
            }
        }

        private static async UniTask RunFull(Context c)
        {
            await WaitFor(c.Flow.CanEnterBuildMode, "initial Preparation");
            await UniTask.NextFrame(); // HUD 바로가기의 LateUpdate 상태까지 반영한다.
            await ValidateBuildings(c);

            bool autoContinue = c.Flow.AutoContinue;
            c.Flow.AutoContinue = false;
            Note("SETUP", "AutoContinue is temporarily false so actual decision buttons can be exercised.");
            try
            {
                await WinBattleAndResolveReward(c, "ordinary wave");
                // 이미 마지막 노드에서 실행했으면 등장한 결정을 실제 Continue 버튼으로 처리한다.
                if (c.Flow.CanChooseRunDecision) await ContinueDecision(c);
                await WaitFor(c.Flow.CanEnterBuildMode, "Preparation after ordinary reward");

                if (c.Flow.CanJumpToLastQuarter)
                {
                    c.Waves.JumpToLastQuarterForTest();
                    Check(c.Flow.CurrentQuarter == WaveController.MAIN_QUARTERS,
                        "existing debug API moves to the final main-game quarter");
                }
                if (c.Flow.CanJumpToLastWave) c.Waves.JumpToLastWaveForTest();
                Check(c.Flow.IsLastNode && c.Waves.CurrentPreset != null,
                    "final-quarter battle has an existing final-node preset");
                await WinBattleAndResolveReward(c, "final main-game quarter");
                await ContinueDecision(c);

                if (c.Flow.CanJumpToLastWave) c.Waves.JumpToLastWaveForTest();
                Check(c.Flow.IsLastNode && c.Waves.CurrentPreset != null,
                    "continued quarter provides a final-node preset");
                await WinBattleAndResolveReward(c, "continued quarter");
                await WaitFor(() => c.Flow.CanChooseRunDecision && c.Decision.IsVisible,
                    "second quarter decision");
                AssertRequiredModal(c, UIId.RunDecision);
                Click(Read<UnityEngine.UI.Button>(c.Decision, "_finishButton"), "decision Finish");
                await WaitFor(() => c.Flow.CurPhase == GamePhase.Finished, "Finish phase");
                Check(c.Flow.HasClearedMainGame && !c.Decision.IsVisible, "Finish preserves clear state and closes its decision");
                Limit("Actual settlement payout, result request from FinishRun and scene navigation are outside this change; verified separately as a UI-only request.");

            }
            finally { if (c.Flow != null) c.Flow.AutoContinue = autoContinue; }
        }

        private static async UniTask ValidateBuildings(Context c)
        {
            BuildingSlot expected = null;
            foreach (BuildingSlot slot in c.Slots)
                if (IsLiveEmpty(slot)) { expected = slot; break; }
            Check(expected != null, "the initialized Test scene has a live empty construction slot");

            BuildingShortcut shortcut = null;
            foreach (BuildingShortcut candidate in Read<BuildingShortcut[]>(c.Startup, "_buildingShortcuts"))
                if (candidate != null && Read<bool>(candidate, "_selectFirstEmptySlot")) { shortcut = candidate; break; }
            Check(shortcut != null, "HUD has its configured first-empty-slot shortcut");
            Click(Read<UnityEngine.UI.Button>(shortcut, "_button"), "HUD construction shortcut");
            Check(c.Building.SelectedSlot == expected && c.Catalog.Popup.IsVisible && c.Catalog.ItemCount > 0,
                "HUD shortcut selects the first valid empty slot and opens its catalog");

            string buildingId = PickBuildCandidate(c);
            ClickCatalog(c, buildingId);
            Check(c.Screen(UIId.BuildingInfo).IsVisible && !c.Catalog.Popup.IsVisible &&
                c.Building.SelectedSlot == expected && c.Quote.Build?.OptionId == buildingId,
                "candidate slot button opens building details without losing the selected world slot");
            ValidateInsufficientFunds(c, expected);
            ValidateDirectBuildingRejections(c, expected);

            int selectedEvents = 0;
            int deselectedEvents = 0;
            Action<BuildingSlot> selected = slot => selectedEvents++;
            Action deselected = () => deselectedEvents++;
            c.Controller.SlotSelected += selected;
            c.Controller.SlotDeselected += deselected;
            try
            {
                c.Camera.ShowBase();
                SelectWorld(c, expected);
                var focus = Read<CinemachineCamera>(c.Camera, "_focusCamera");
                var target = Read<Transform>(c.Camera, "_focusTarget");
                var position = new Vector3(expected.BuildPosition.x, expected.BuildPosition.y, 0);
                Check(selectedEvents == 1 && (int)focus.Priority == 20 && target.position == position,
                    "world selection event reaches both the catalog and camera close-up");
                ClickCatalog(c, buildingId);
                int beforeClose = deselectedEvents;
                ClickClose(c, UIId.BuildingInfo);
                Check(c.Building.SelectedSlot == null && !c.Screen(UIId.BuildingInfo).IsVisible &&
                    deselectedEvents == beforeClose && (int)focus.Priority == 20,
                    "close button preserves the existing behavior: UI clears, camera does not zoom out");

                SelectWorld(c, expected);
                ClickCatalog(c, buildingId);
                Fund(c, c.Quote.Build);
                BuildingActionOffer build = c.Quote.Build;
                var before = Balance(c);
                int beforeBuild = deselectedEvents;
                var buildButton = ActionButton(c, "_build");
                Click(buildButton, "Build");
                Check(expected.IsOccupied && expected.CurrentBuilding.Data.BuildingId == buildingId,
                    "Build button creates the selected building through the original controller");
                AssertBalance(c, before, -build.GoldAmount, -build.GemAmount, "build cost");
                Check(c.Screen(UIId.BuildingInfo).IsVisible && c.Building.SelectedSlot == expected &&
                    deselectedEvents == beforeBuild + 1 && (int)focus.Priority == -1,
                    "build success keeps details open and uses the existing controller camera reset");
                Building built = expected.CurrentBuilding;
                var afterBuild = Balance(c);
                buildButton.onClick.Invoke();
                Check(expected.CurrentBuilding == built && Balance(c) == afterBuild,
                    "a repeated build callback cannot spend or create again after success");

                SelectOccupied(c, expected);
                Check(c.Screen(UIId.BuildingInfo).IsVisible && c.Quote.Build == null && c.Quote.Dismantle != null,
                    "occupied world selection shows current building actions");
                if (c.Quote.Upgrade != null)
                {
                    Fund(c, c.Quote.Upgrade);
                    BuildingActionOffer upgrade = c.Quote.Upgrade;
                    BuildingData oldData = expected.CurrentBuilding.Data;
                    before = Balance(c);
                    Click(ActionButton(c, "_upgrade"), "Upgrade");
                    Check(expected.IsOccupied && expected.CurrentBuilding.Data != oldData &&
                        expected.CurrentBuilding.Data.BuildingId == upgrade.OptionId,
                        "Upgrade button replaces the building with its configured next data");
                    AssertBalance(c, before, -upgrade.GoldAmount, -upgrade.GemAmount, "upgrade cost");
                    Check(c.Screen(UIId.BuildingInfo).IsVisible,
                        "upgrade success retains the existing details-screen behavior");
                    await UniTask.NextFrame(); // 기존 건물 Destroy 및 census 해제를 완료한다.
                }
                else Limit("This candidate has no upgrade available at the current core level; no unlock rule was changed.");

                SelectOccupied(c, expected);
                BuildingActionOffer refund = c.Quote.Dismantle;
                Check(refund != null && refund.CanExecute, "non-core building offers demolition");
                before = Balance(c);
                var dismantle = ActionButton(c, "_dismantle");
                Click(dismantle, "Dismantle");
                Check(!expected.IsOccupied && c.Building.SelectedSlot == null &&
                    !c.Screen(UIId.BuildingInfo).IsVisible, "demolition empties the slot and closes its details");
                AssertBalance(c, before, refund.GoldAmount, refund.GemAmount, "demolition refund");
                var refunded = Balance(c);
                dismantle.onClick.Invoke();
                Check(Balance(c) == refunded && !expected.IsOccupied, "demolition refund is applied only once");
                await UniTask.NextFrame();

                ValidateCoreProtection(c);
                // 다음 실제 전투에도 아군을 생성할 수 있도록 검증에 썼던 건물을 버튼으로 다시 세운다.
                SelectWorld(c, expected);
                ClickCatalog(c, buildingId);
                Fund(c, c.Quote.Build);
                Click(ActionButton(c, "_build"), "rebuild for battle validation");
                Check(expected.IsOccupied, "construction remains usable after demolition and core rejection");
                ClickClose(c, UIId.BuildingInfo);
                await UniTask.NextFrame();
            }
            finally
            {
                if (c.Controller != null)
                {
                    c.Controller.SlotSelected -= selected;
                    c.Controller.SlotDeselected -= deselected;
                }
            }
        }

        private static void ValidateDirectBuildingRejections(Context c, BuildingSlot slot)
        {
            BuildingData option = Read<BuildingData>(c.Building, "_candidate");
            var originalBalance = Balance(c);
            Fund(c, c.Quote.Build);
            BuildingInteractionTarget target = c.Controller.QueryInteraction(slot, option).Target;
            var before = Balance(c);
            Building original = slot.CurrentBuilding;
            BuildingData outside = UnityEngine.Object.Instantiate(option);
            Collider2D collider = slot.GetComponent<Collider2D>();
            bool colliderEnabled = collider.enabled;
            bool slotEnabled = slot.enabled;
            bool active = slot.gameObject.activeSelf;
            FieldInfo phaseField = typeof(GameFlowController).GetField("_curPhase", Fields);
            object phase = phaseField.GetValue(c.Flow);
            try
            {
                // Clone은 후보 목록의 원본 참조가 아니다. SO 원본은 수정하지 않는다.
                AssertDirectBuildingRejection(c, target, BuildingInteractionAction.Build, outside,
                    BuildingInteractionFailure.CandidateUnavailable, "out-of-catalog clone");
                collider.enabled = false;
                AssertDirectBuildingRejection(c, target, BuildingInteractionAction.Build, option,
                    BuildingInteractionFailure.InvalidTarget, "disabled Collider");
                collider.enabled = colliderEnabled;
                slot.enabled = false;
                AssertDirectBuildingRejection(c, target, BuildingInteractionAction.Build, option,
                    BuildingInteractionFailure.InvalidTarget, "disabled slot component");
                slot.enabled = slotEnabled;
                slot.gameObject.SetActive(false);
                AssertDirectBuildingRejection(c, target, BuildingInteractionAction.Build, option,
                    BuildingInteractionFailure.InvalidTarget, "inactive slot object");
                slot.gameObject.SetActive(active);
                // 동기 검증 범위에서 조건 필드만 바꾼다. 페이즈 이벤트/노드 진행은 실행하지 않는다.
                phaseField.SetValue(c.Flow, GamePhase.Battle);
                AssertDirectBuildingRejection(c, target, BuildingInteractionAction.Build, option,
                    BuildingInteractionFailure.PhaseBlocked, "phase changed after query");
                Check(Balance(c) == before && ReferenceEquals(slot.CurrentBuilding, original),
                    "all direct rejection probes preserve wallet and occupation");
            }
            finally
            {
                phaseField.SetValue(c.Flow, phase);
                collider.enabled = colliderEnabled;
                slot.enabled = slotEnabled;
                slot.gameObject.SetActive(active);
                UnityEngine.Object.Destroy(outside);
                if (before.gold > originalBalance.gold && !c.Wallet.TrySpend(CurrencyType.Gold, before.gold - originalBalance.gold))
                    throw new InvalidOperationException("Could not restore direct-probe Gold funding.");
                if (before.gems > originalBalance.gems && !c.Wallet.TrySpend(CurrencyType.Gem, before.gems - originalBalance.gems))
                    throw new InvalidOperationException("Could not restore direct-probe Gem funding.");
            }
            Check(Balance(c) == originalBalance, "direct rejection probes restore their original runtime funding");
            c.Building.Refresh();
        }

        private static void AssertDirectBuildingRejection(Context c, BuildingInteractionTarget target,
            BuildingInteractionAction action, BuildingData option, BuildingInteractionFailure failure, string label)
        {
            var balance = Balance(c);
            Building occupant = target.Slot.CurrentBuilding;
            BuildingInteractionResult result = c.Controller.TryExecuteInteraction(target, action, option);
            Check(result.Failure == failure && !result.MayHaveChangedState && Balance(c) == balance &&
                ReferenceEquals(target.Slot.CurrentBuilding, occupant), label + " is rejected by the new execution API without mutation");
        }

        private static void ValidateInsufficientFunds(Context c, BuildingSlot slot)
        {
            BuildingActionOffer offer = c.Quote.Build;
            if (offer.GoldAmount == 0 && offer.GemAmount == 0)
            {
                Limit("The selected construction is free, so it cannot exercise insufficient funds.");
                return;
            }
            var before = Balance(c);
            BuildingData option = Read<BuildingData>(c.Building, "_candidate");
            BuildingInteractionTarget target = c.Controller.QueryInteraction(slot, option).Target;
            int removedGold = 0;
            int removedGems = 0;
            try
            {
                if (before.gold > 0)
                {
                    if (!c.Wallet.TrySpend(CurrencyType.Gold, before.gold)) throw new InvalidOperationException("Cannot temporarily empty test gold.");
                    removedGold = before.gold;
                }
                if (before.gems > 0)
                {
                    if (!c.Wallet.TrySpend(CurrencyType.Gem, before.gems)) throw new InvalidOperationException("Cannot temporarily empty test gems.");
                    removedGems = before.gems;
                }
                Note("SETUP", "Temporarily emptied runtime Gold/Gem through TrySpend; exact removed amounts are restored in finally.");
                var button = ActionButton(c, "_build");
                Check(!button.interactable && !c.Quote.Build.CanExecute,
                    "live balance change disables an unaffordable construction");
                button.onClick.Invoke(); // 비활성 버튼 콜백을 우회 호출해도 실제 명령을 보내지 않아야 한다.
                Check(!slot.IsOccupied && Balance(c) == (0, 0) && !c.Actions.IsRequestPending,
                    "an unaffordable callback cannot occupy a slot or change currency");
                AssertDirectBuildingRejection(c, target, BuildingInteractionAction.Build, option,
                    BuildingInteractionFailure.InsufficientFunds, "balance changed after query");
            }
            finally
            {
                if (removedGold > 0 && !c.Wallet.TryAdd(CurrencyType.Gold, removedGold))
                    throw new InvalidOperationException("Failed to restore temporarily removed gold.");
                if (removedGems > 0 && !c.Wallet.TryAdd(CurrencyType.Gem, removedGems))
                    throw new InvalidOperationException("Failed to restore temporarily removed gems.");
            }
            Check(Balance(c) == before, "insufficient-funds setup restores the exact original wallet");
        }

        private static void ValidateCoreProtection(Context c)
        {
            BuildingSlot core = Array.Find(c.Slots, slot => slot != null && c.Controller.IsCoreSlot(slot));
            Check(core != null && core.IsOccupied, "initialized scene has its occupied core slot");
            SelectOccupied(c, core);
            Building original = core.CurrentBuilding;
            var before = Balance(c);
            Check(c.Quote.Dismantle == null && !ActionButton(c, "_dismantle").interactable,
                "core details do not offer demolition");
            ActionButton(c, "_dismantle").onClick.Invoke();
            AssertDirectBuildingRejection(c, c.Controller.QueryInteraction(core).Target,
                BuildingInteractionAction.Demolish, null, BuildingInteractionFailure.CoreProtected, "core demolition");
            Note("EXPECTED WARNING", "The following original controller rejection says the core cannot be demolished.");
            bool accepted = c.Controller.TryDemolish(core);
            Check(!accepted && core.CurrentBuilding == original && Balance(c) == before,
                "both the UI callback and original controller preserve the core and wallet");
            ClickClose(c, UIId.BuildingInfo);
        }

        private static async UniTask WinBattleAndResolveReward(Context c, string label)
        {
            await StartBattle(c, label);
            c.Waves.SetSuccess();
            Note("INPUT", label + ": existing WaveController.SetSuccess test input.");
            await WaitFor(() => c.RewardView.IsVisible || c.Flow.CurPhase == GamePhase.Event ||
                c.Flow.CurPhase == GamePhase.Store || c.Flow.CanEnterBuildMode() || c.Flow.CanChooseRunDecision,
                label + " direct artifact selection or empty-pool completion");
            Check(c.Flow.CurPhase != GamePhase.Reward || !c.Screen(UIId.WaveReward).IsVisible,
                label + ": reward confirmation is skipped before artifact selection");
            var rewardBalance = Balance(c);
            if (c.Flow.CurPhase == GamePhase.Reward)
            {
                Check(c.Wallet.TryGetAppliedWaveReward(c.Flow.CurrentQuarter, c.Flow.CurrentWave, out _, out _),
                    label + ": currency remains paid at the existing Reward phase timing");
                Check(!c.Wallet.TryApplyWaveReward() && Balance(c) == rewardBalance,
                    label + ": current wave currency cannot be granted twice");
            }
            if (c.RewardView.IsVisible)
            {
                Check(c.Artifacts.IsSelectingReward, label + ": artifact UI is using the real manager selection");
                AssertRequiredModal(c, UIId.ArtifactReward);
                var confirm = Read<UnityEngine.UI.Button>(c.RewardView, "_confirmButton");
                Check(!confirm.interactable, label + ": required artifact cannot be forfeited without selecting a card");
                var cards = Read<Array>(c.RewardView, "_cards");
                Click(Read<UnityEngine.UI.Button>(cards.GetValue(0), "Button"), "artifact candidate");
                string id = c.RewardView.SelectedArtifactId;
                int stacks = StackCount(c.Artifacts, id);
                Click(confirm, "artifact Confirm");
                confirm.onClick.Invoke();
                Check(StackCount(c.Artifacts, id) == stacks + 1 && !c.RewardView.IsVisible,
                    label + ": confirm grants the chosen real artifact once and closes before flow resumes");
            }
            else Limit(label + ": current artifact pool has no candidates; existing empty-pool completion was used.");

            await WaitFor(() => c.Flow.CurPhase == GamePhase.Event || c.Flow.CurPhase == GamePhase.Store ||
                c.Flow.CanEnterBuildMode() || c.Flow.CanChooseRunDecision, label + " post-reward phase");
            if (c.Flow.CurPhase == GamePhase.Event || c.Flow.CurPhase == GamePhase.Store)
            {
                GamePhase phase = c.Flow.CurPhase;
                if (phase == GamePhase.Store)
                {
                    await WaitFor(() => c.Shop.IsPending && c.Screen(UIId.Shop).IsVisible,
                        label + " shop request");
                    AssertRequiredModal(c, UIId.Shop);
                    Click(Read<UnityEngine.UI.Button>(c.Shop, "_leaveButton"), "Shop Leave");
                }
                else
                {
                    AssertRequiredModal(c, UIId.WaveReward);
                    Click(Read<UnityEngine.UI.Button>(c.Continue, "_continueButton"), phase + " Continue");
                }
                Note("PASS", label + ": " + phase + " screen forwards its real content completion.");
                _checks++;
                await WaitFor(() => c.Flow.CanEnterBuildMode() || c.Flow.CanChooseRunDecision,
                    label + " after attached content");
            }
            else Note("BRANCH", label + ": this generated node has no attached Event/Store prompt.");
            Check(!c.RewardView.IsVisible && !c.Screen(UIId.WaveReward).IsVisible,
                label + ": reward screens close before the next preparation/decision");
        }

        private static async UniTask StartBattle(Context c, string label)
        {
            await WaitFor(c.Flow.CanEnterBuildMode, label + " Preparation");
            await UniTask.NextFrame();
            int preparing = 0;
            Action<GamePhase> count = phase => { if (phase == GamePhase.BattlePreparing) preparing++; };
            c.Flow.PhaseChanged += count;
            try
            {
                var start = Read<UnityEngine.UI.Button>(c.Hud, "_waveStartButton");
                Click(start, "HUD start wave");
                start.onClick.Invoke();
                await WaitFor(c.Flow.CanBattle, label + " Battle (real unit preparation)");
                Check(preparing == 1 && c.Flow.CurPhase == GamePhase.Battle,
                    label + ": HUD starts real preparation and Battle exactly once");
                Check(!c.Screen(UIId.BuildingInfo).IsVisible && !c.Catalog.Popup.IsVisible && !start.interactable,
                    label + ": entering battle closes construction and locks start input");
            }
            finally { c.Flow.PhaseChanged -= count; }
        }

        private static async UniTask ContinueDecision(Context c)
        {
            await WaitFor(() => c.Flow.CanChooseRunDecision && c.Decision.IsVisible, "quarter decision");
            AssertRequiredModal(c, UIId.RunDecision);
            int quarter = c.Flow.CurrentQuarter;
            Click(Read<UnityEngine.UI.Button>(c.Decision, "_continueButton"), "decision Continue");
            await WaitFor(c.Flow.CanEnterBuildMode, "Preparation after decision Continue");
            Check(c.Flow.CurrentQuarter == quarter + 1 && c.Flow.HasClearedMainGame && !c.Decision.IsVisible,
                "decision Continue advances a quarter, retains clear state, and closes the modal");
        }

        private static async UniTask RunDefeat(Context c)
        {
            await StartBattle(c, "defeat branch");
            c.Waves.SetFail();
            Note("INPUT", "Existing WaveController.SetFail test input.");
            SettlementView settlement = Read<SettlementView>(c.Startup, "_settlementUI");
            await WaitFor(() => settlement.IsVisible && settlement.IsPending,
                "defeat settlement request");
            Check(c.Flow.CurPhase != GamePhase.Finished, "settlement waits before Finished clears run effects");
            Check(!c.RewardView.IsVisible && !c.Screen(UIId.WaveReward).IsVisible && !c.Decision.IsVisible,
                "defeat bypasses artifact and quarter selection");
            Check(c.Manager.TopPopup == c.Screen(UIId.RunResult) && c.Manager.HasBlockingPopup,
                "defeat opens the registered mandatory settlement screen");
            Note("SCOPE", "Scene return and isolated bloodstone persistence are checked by RunSettlementPlayValidation.");
        }

        private static UniTask RunRestart(Context c)
        {
            Limit("Result restart/navigation is outside the request-only settlement contract.");
            return UniTask.CompletedTask;
        }

        private static async UniTask RunContinue(Context c)
        {
            Check(c.Flow.CurPhase == GamePhase.Finished,
                "in-memory Continue starts from the actual finished result");
            await WaitFor(() => !Read<bool>(c.Flow, "_isTransitioning"), "finished flow transition unlock");
            GameFlowSaveData snapshot = c.Flow.CaptureSaveData();
            int quarter = c.Flow.CurrentQuarter;
            int wave = c.Flow.CurrentWave;
            Note("INPUT", "CaptureSaveData and RestoreSaveData use one in-memory snapshot only; no save coordinator or file is used.");
            c.Flow.RestoreSaveData(snapshot);
            c.Flow.Continue();
            await WaitFor(c.Flow.CanEnterBuildMode, "existing Continue Preparation");
            await UniTask.NextFrame();
            Check(c.Screen(UIId.Hud).IsVisible && !c.Screen(UIId.RunResult).IsVisible && !c.Manager.HasBlockingPopup,
                "existing Continue phase notification restores the HUD and closes the result");
            Check(c.Flow.CurrentQuarter == quarter && c.Flow.CurrentWave == wave,
                "in-memory Continue preserves the restored node instead of starting a new run");
            Check(Read<UnityEngine.UI.Button>(c.Hud, "_waveStartButton").IsInteractable(),
                "existing Continue restores preparation start input");
            Limit("Core Continue currently ignores individual resume steps and always enters Preparation. This checks UI notification only, not complete save-resume correctness.");
            Note("END", "Play Mode remains in Preparation. Restart requires another finished result; it is not invoked automatically.");
        }

        private static void AssertRequiredModal(Context c, UIId id)
        {
            UIScreen screen = c.Screen(id);
            Check(screen.IsVisible && c.Manager.HasBlockingPopup && !c.Manager.CloseTopPopup() && screen.IsVisible,
                id + " blocks the same CloseTop path used by ESC");
            Check(!Read<UnityEngine.UI.Button>(c.Hud, "_waveStartButton").IsInteractable(),
                id + " blocks HUD interaction while its mandatory choice is open");
        }

        private static string PickBuildCandidate(Context c)
        {
            var items = Read<List<BuildingCatalogItem>>(c.Catalog, "_items");
            var data = Read<Dictionary<string, BuildingData>>(c.Building, "_byId");
            // 가능한 경우 다음 전투에서 아군도 생성하는 후보를 고른다. 후보/해금 규칙은 변경하지 않는다.
            foreach (BuildingCatalogItem item in items)
                if (item.GoldCost.HasValue && data[item.Id].HasWorldVisual && data[item.Id].HasSpawn) return item.Id;
            foreach (BuildingCatalogItem item in items)
                if (item.GoldCost.HasValue && data[item.Id].HasWorldVisual) return item.Id;
            throw new InvalidOperationException("Current catalog has no candidate with valid cost and world visual.");
        }

        private static void ClickCatalog(Context c, string id)
        {
            var items = Read<List<BuildingCatalogItem>>(c.Catalog, "_items");
            int index = items.FindIndex(item => item.Id == id);
            if (index < 0) throw new InvalidOperationException("Candidate left the actual catalog: " + id);
            var cards = Read<Array>(c.Catalog, "_cards");
            if (cards.Length == 0) throw new InvalidOperationException("The catalog has no cards.");
            int page = index / cards.Length;
            while (c.Catalog.PageIndex < page) Click(Read<UnityEngine.UI.Button>(c.Catalog, "_next"), "catalog Next");
            while (c.Catalog.PageIndex > page) Click(Read<UnityEngine.UI.Button>(c.Catalog, "_previous"), "catalog Previous");
            UIItemSlot slot = Read<UIItemSlot>(cards.GetValue(index % cards.Length), "Slot");
            Click(Read<UnityEngine.UI.Button>(slot, "_button"), "catalog candidate " + id);
        }

        private static void SelectWorld(Context c, BuildingSlot slot)
        {
            MethodInfo notify = typeof(BuildingBuildController).GetMethod("NotifySlotSelected", Fields);
            if (notify == null) throw new MissingMethodException("BuildingBuildController.NotifySlotSelected");
            notify.Invoke(c.Controller, new object[] { slot });
        }

        private static void SelectOccupied(Context c, BuildingSlot slot)
        {
            SelectWorld(c, slot);
            if (c.Catalog.Popup.IsVisible)
            {
                var items = Read<List<BuildingCatalogItem>>(c.Catalog, "_items");
                if (items.Count == 0) throw new InvalidOperationException("Occupied selection opened an empty upgrade catalog.");
                ClickCatalog(c, items[0].Id);
            }
        }

        private static void ClickClose(Context c, UIId id)
        {
            UIScreen screen = c.Screen(id);
            foreach (CloseUtility utility in c.Manager.GetComponentsInChildren<CloseUtility>(true))
                if (Read<UIScreen>(utility, "_screen") == screen && utility.gameObject.activeInHierarchy)
                {
                    Click(Read<UnityEngine.UI.Button>(utility, "_button"), id + " Close");
                    return;
                }
            throw new InvalidOperationException("No active owner CloseUtility button for " + id);
        }

        private static UnityEngine.UI.Button ActionButton(Context c, string field) =>
            Read<UnityEngine.UI.Button>(Read<object>(c.Actions, field), "_button");

        private static void Fund(Context c, BuildingActionOffer offer)
        {
            if (offer == null) throw new InvalidOperationException("The expected action has no real quote.");
            AddDeficit(c, CurrencyType.Gold, offer.GoldAmount);
            AddDeficit(c, CurrencyType.Gem, offer.GemAmount);
        }

        private static void AddDeficit(Context c, CurrencyType type, int required)
        {
            int deficit = Math.Max(0, required - c.Wallet.GetBalance(type));
            if (deficit == 0) return;
            if (!c.Wallet.TryAdd(type, deficit)) throw new InvalidOperationException("Could not add runtime test currency: " + type);
            Note("TEST CURRENCY", "Wallet.TryAdd(" + type + ", " + deficit + ") supplied the exact test deficit; no saved balance was edited.");
        }

        private static (int gold, int gems) Balance(Context c) =>
            (c.Wallet.GetBalance(CurrencyType.Gold), c.Wallet.GetBalance(CurrencyType.Gem));

        private static void AssertBalance(Context c, (int gold, int gems) before, int gold, int gems, string message) =>
            Check(Balance(c) == (before.gold + gold, before.gems + gems),
                message + " matches the displayed quote for Gold and Gem");

        private static int StackCount(ArtifactManager manager, string id) =>
            manager.TryGetById(id, out ArtifactInstance instance) ? instance.StackCount : 0;

        private static bool IsLiveEmpty(BuildingSlot slot)
        {
            if (slot == null || !slot.isActiveAndEnabled || slot.IsOccupied) return false;
            var collider = slot.GetComponent<Collider2D>();
            return collider != null && collider.enabled;
        }

        private static InGameUIManager FindManager()
        {
            InGameUIManager result = null;
            foreach (InGameUIManager manager in UnityEngine.Object.FindObjectsByType<InGameUIManager>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!manager.gameObject.scene.IsValid() || manager.gameObject.scene.name != "Test") continue;
                if (result != null) throw new InvalidOperationException("Multiple active InGameUIManagers exist in Test.");
                result = manager;
            }
            if (result == null) throw new InvalidOperationException("No active InGameUIManager is installed in the playing Test scene.");
            return result;
        }

        private static T Read<T>(object owner, string field)
        {
            if (owner == null) throw new InvalidOperationException("Cannot read " + field + " on a missing component.");
            FieldInfo info = owner.GetType().GetField(field, Fields);
            if (info == null) throw new MissingFieldException(owner.GetType().Name, field);
            object value = info.GetValue(owner);
            if (value == null) throw new InvalidOperationException(owner.GetType().Name + "." + field + " is not connected.");
            return (T)value;
        }

        private static void Click(UnityEngine.UI.Button button, string label)
        {
            if (button == null || !button.gameObject.activeInHierarchy || !button.IsInteractable())
                throw new InvalidOperationException(label + " button is not active/interactable.");
            button.onClick.Invoke();
        }

        private static async UniTask WaitFor(Func<bool> condition, string label)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + TimeoutSeconds;
            while (!condition())
            {
                if (!EditorApplication.isPlaying) throw new OperationCanceledException("Play Mode stopped during " + label);
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                    throw new TimeoutException(label + " did not complete within " + TimeoutSeconds + " seconds.");
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }

        private static void Check(bool success, string message)
        {
            if (!success) throw new InvalidOperationException(message);
            _checks++;
            Note("PASS", message);
        }

        private static void Limit(string message)
        {
            _limits++;
            Note("LIMIT", message);
        }

        private static void Note(string kind, string message)
        {
            Report.AppendLine(kind + ": " + message);
            LastResult = "RUNNING (" + _checks + " checks)\n" + Report;
        }
    }
}
#endif
