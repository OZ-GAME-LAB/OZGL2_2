#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using Game.Core;
using OZGL.KDH;
using TMPro;
using Units;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.UI.InGame.Editor
{
    /// <summary>
    /// 실제 Test 전멸 → 결산 → OutGame 복귀를 검사한다. 저장은 GUID별 격리 폴더만 사용한다.
    /// Arm은 Edit Mode, Run은 새 Play 세션에서 호출한다. Play 종료는 호출자가 결정한다.
    /// </summary>
    [InitializeOnLoad]
    public static class RunSettlementPlayValidation
    {
        private const string Key = "RunSettlement.Validation.";
        private const int SeedBalance = 1234;
        private const double TimeoutSeconds = 30;
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly StringBuilder Report = new StringBuilder();
        private static int _checks;

        public static string LastResult => SessionState.GetString(Key + "Result", "Not run");
        public static string OutputDirectory => SessionState.GetString(Key + "Output", "");
        public static string SaveDirectory => SessionState.GetString(Key + "Saves", "");
        private static bool Armed => SessionState.GetBool(Key + "Armed", false);

        [Serializable] private sealed class FileStamp { public string Path; public string Hash; }
        [Serializable] private sealed class FileStamps { public List<FileStamp> Items = new List<FileStamp>(); }

        static RunSettlementPlayValidation()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            EditorApplication.playModeStateChanged += OnPlayStateChanged;
        }

        /// <summary>mode: zero(정상 전멸), positive(1회 클리어 후 전멸), forced(SetFail), victory(최종 분기 종료), retry(저장 실패 후 재시도).</summary>
        public static string Arm(string mode)
        {
            mode = NormalizeMode(mode);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Arm in Edit Mode before entering a fresh Test Play session.");
            if (EditorSettings.enterPlayModeOptionsEnabled &&
                (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableSceneReload) != 0)
                throw new InvalidOperationException("This validation requires scene reload so isolation runs before scene Start methods.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "Test" || scene.isDirty)
                throw new InvalidOperationException("Open the saved Test scene before arming validation.");
            SessionState.SetBool(Key + "Armed", false);

            string output = Path.GetFullPath("PersonalDocs/UIValidation/SettlementIntegration/" +
                DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + mode + "-" + Guid.NewGuid().ToString("N"));
            string saves = Path.GetFullPath(".utmp/SettlementIntegration/Saves-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(output);
            Directory.CreateDirectory(saves);
            SessionState.SetString(Key + "Mode", mode);
            SessionState.SetString(Key + "Output", output);
            SessionState.SetString(Key + "Saves", saves);
            SessionState.SetString(Key + "SafetyError", "");
            SessionState.SetBool(Key + "Running", false);
            SessionState.SetBool(Key + "HasRun", false);

            var stamps = new FileStamps();
            foreach (string path in AssetDatabase.FindAssets("t:TMP_FontAsset")
                .Select(AssetDatabase.GUIDToAssetPath).Where(path => path.StartsWith("Assets/", StringComparison.Ordinal)))
                stamps.Items.Add(new FileStamp { Path = Path.GetFullPath(path), Hash = Hash(path) });
            string defaultSave = Path.Combine(Application.persistentDataPath, "Saves", "outgame-test-persistent.json");
            stamps.Items.Add(new FileStamp { Path = defaultSave, Hash = Hash(defaultSave) });
            SessionState.SetString(Key + "ProtectedFiles", JsonUtility.ToJson(stamps));

            var bootstrap = InScene<BootStrap>(scene).Single();
            var traits = Read<OutGameTraitController>(bootstrap, "_persistentTraits");
            var catalog = Read<List<TraitData>>(traits, "_traitDatas");
            SeedProfile(catalog, saves);
            SessionState.SetBool(Key + "Armed", true);
            InGameUITwoCanvasValidation.ArmPlayFontIsolation(true);
            SessionState.SetString(Key + "Result", "Armed " + mode + "; saves: " + saves);
            return LastResult;
        }

        private static void SeedProfile(IReadOnlyList<TraitData> catalog, string directory)
        {
            if (catalog == null || catalog.Count == 0 || catalog.Any(item => item == null))
                throw new InvalidOperationException("The Test persistent trait catalog is missing.");
            TraitData leveled = catalog.FirstOrDefault(item => item.Prerequisite == null && item.MaxLevel >= 1 &&
                item.CurrencyEffects.Count == 0);
            if (leveled == null) throw new InvalidOperationException("No independent non-currency trait is available for the preservation fixture.");
            var levels = catalog.Select(item => new TraitLevelEntry { Id = item.Id, Level = item == leveled ? 1 : 0 }).ToList();
            var seed = new PersistentSaveData { Traits = new TraitSaveData(levels), Wallet = new PersistentWalletSaveData(SeedBalance) };
            var host = new GameObject("Settlement isolated save fixture") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var wallet = host.AddComponent<PersistentCurrencyManager>();
                var writer = host.AddComponent<PersistentSaveCoordinator>();
                var provider = host.AddComponent<OutGameTraitController>();
                var save = host.AddComponent<SaveManager>();
                wallet.Initialize();
                typeof(OutGameTraitController).GetField("_traitDatas", Fields).SetValue(provider, new List<TraitData>(catalog));
                provider.Initialize(wallet, writer);
                if (!provider.TryValidateSaveData(seed.Traits, out string invalid))
                    throw new InvalidOperationException("Invalid seed traits: " + invalid);
                save.ConfigureDirectory(directory);
                if (!save.TrySave(PersistentSaveCoordinator.SaveKey, seed, out string error))
                    throw new IOException("Could not write the isolated seed: " + error);
                SessionState.SetString(Key + "SeedTraits", JsonUtility.ToJson(seed.Traits));
            }
            finally { Object.DestroyImmediate(host); }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Application.isPlaying || !Armed || (scene.name != "Test" && scene.name != "OutGameSetupTest")) return;
            try
            {
                SaveManager[] saves = InScene<SaveManager>(scene).ToArray();
                if (saves.Length == 0 || string.IsNullOrWhiteSpace(SaveDirectory))
                    throw new InvalidOperationException("The armed scene has no isolated SaveManager.");
                foreach (SaveManager save in saves) save.ConfigureDirectory(SaveDirectory);
            }
            catch (Exception exception)
            {
                // Start가 기본 플레이어 경로에 접근하기 전에 초기화를 중단한다.
                foreach (BootStrap bootstrap in InScene<BootStrap>(scene)) bootstrap.enabled = false;
                foreach (OutGameBootstrap bootstrap in InScene<OutGameBootstrap>(scene)) bootstrap.enabled = false;
                SessionState.SetString(Key + "SafetyError", exception.Message);
                Debug.LogException(exception);
            }
        }

        private static void OnPlayStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !Armed) return;
            try { VerifyProtectedFiles(); }
            catch (Exception exception)
            {
                SessionState.SetString(Key + "Result", LastResult + "\nPROTECTION FAIL: " + exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                SessionState.SetBool(Key + "Armed", false);
                SessionState.SetBool(Key + "Running", false);
                InGameUITwoCanvasValidation.ArmPlayFontIsolation(false);
                if (!string.IsNullOrWhiteSpace(OutputDirectory))
                    File.WriteAllText(Path.Combine(OutputDirectory, "after-stop.txt"), LastResult, Encoding.UTF8);
            }
        }

        public static void Run(string mode) => RunAsync(mode).Forget(Debug.LogException);

        public static async UniTask<string> RunAsync(string mode)
        {
            mode = NormalizeMode(mode);
            if (!Armed || !EditorApplication.isPlaying || SessionState.GetString(Key + "Mode", "") != mode ||
                SessionState.GetBool(Key + "Running", false) || SessionState.GetBool(Key + "HasRun", false))
                throw new InvalidOperationException("Arm this mode, enter a new Test Play session, then run it once.");
            SessionState.SetBool(Key + "Running", true);
            SessionState.SetBool(Key + "HasRun", true);
            SessionState.SetString(Key + "Result", "Running " + mode);
            Report.Clear();
            _checks = 0;
            Context c = null;
            int finishedEvents = 0;
            RunSummary failedSummary = null;
            Action<GamePhase> trackFinished = phase => { if (phase == GamePhase.Finished) finishedEvents++; };
            Note("MODE", mode + "; saved scene/prefab assets remain unchanged; isolated profile: " + SaveDirectory);
            try
            {
                Check(string.IsNullOrWhiteSpace(SessionState.GetString(Key + "SafetyError", "")), "pre-Start save isolation succeeded");
                c = new Context(SceneManager.GetActiveScene());
                c.Flow.PhaseChanged += trackFinished;
                await WaitFor(() => c.Bootstrap.IsUIConnected && c.Flow.CanEnterBuildMode(), "Test initialization");
                Check(c.Save.DirectoryPath == SaveDirectory && c.Persistent.IsInitialized && c.Persistent.Balance == SeedBalance,
                    "Test loaded the seeded balance from the isolated profile");
                Check(TraitsEqual(c.Traits.CaptureSaveData(), SeedTraits()), "Test loaded the seeded trait levels");
                Check(c.UI.GetComponentsInChildren<Canvas>(true).Length == 2, "existing HUD and Popup Canvas count remains two");
                Check(c.Archive.CaptureSaveData().ClearWaveCount == 0, "a fresh run begins with no cleared-wave record");
                await EnsureArmyBuilding(c);
                if (mode == "victory")
                {
                    c.Flow.AutoContinue = false;
                    Check(c.Waves.CanJumpToLastQuarter, "the public final-quarter test jump is available");
                    c.Waves.JumpToLastQuarterForTest();
                    Check(c.Waves.CanJumpToLastWave, "the public final-wave test jump is available");
                    c.Waves.JumpToLastWaveForTest();
                    Note("TEST PROGRESSION", "JumpToLastQuarterForTest/JumpToLastWaveForTest move to the final main-game node. Earlier nodes are not played or counted as cleared. AutoContinue=false exposes the real Finish choice.");
                    Check(c.Flow.CurrentQuarter == WaveController.MAIN_QUARTERS && c.Flow.IsLastNode,
                        "public test inputs selected the final main-game battle");
                    await StartBattle(c);
                    KillTeam(c, UnitTeam.Enemy);
                    await ResolveVictoryContents(c);
                    await WaitFor(() => c.Decision.IsPending && c.Decision.IsVisible && c.Flow.CanChooseRunDecision,
                        "quarter Finish/Continue decision");
                    await PointerClick(Read<Button>(c.Decision, "_finishButton"), "quarter Finish decision");
                }
                else
                {
                    if (mode == "positive") await ClearOneWave(c);
                    await StartBattle(c);
                    if (mode == "retry")
                    {
                        string blocker = Path.Combine(SaveDirectory, "save-directory-blocker");
                        File.WriteAllText(blocker, "Intentional isolated save failure fixture.", Encoding.UTF8);
                        c.Save.ConfigureDirectory(blocker);
                        Note("EXPECTED ERROR", "A regular file temporarily replaces the isolated save directory; the first settlement write must fail without changing the wallet.");
                    }
                    if (mode == "forced")
                    {
                        Note("INPUT", "Existing WaveController.SetFail injects a result; it does not verify unit death/annihilation.");
                        c.Waves.SetFail();
                    }
                    else
                    {
                        Note("INPUT", "Public lethal damage is injected into live allies. Died → UnitDied → WaveController result detection is real; combat AI/damage calculation is outside this test.");
                        KillTeam(c, UnitTeam.Ally);
                    }
                    if (mode == "retry")
                    {
                        await WaitFor(() => c.Continue.IsPending, "settlement save-failure retry prompt");
                        Check(!c.Settlement.IsPending && !c.Settlement.IsVisible && c.Persistent.Balance == SeedBalance &&
                            finishedEvents == 0 && c.Archive.TryGetRunSummary(out failedSummary),
                            "failed save preserves balance, completed summary and unfinished scene transition");
                        Check(Read<TMP_Text>(c.Continue, "_messageText").text.Contains("정산"),
                            "the pending Continue prompt reports the settlement failure");
                        c.Save.ConfigureDirectory(SaveDirectory);
                        VerifySaved(c.Save, SeedBalance);
                        await PointerClick(Read<Button>(c.Continue, "_continueButton"), "settlement retry confirmation");
                    }
                }
                await WaitFor(() => c.Settlement.IsPending && c.Settlement.IsVisible, "requested settlement popup");
                Check(c.Flow.CurPhase != GamePhase.Finished && finishedEvents == 0 &&
                    c.Archive.TryGetRunSummary(out RunSummary summary),
                    "the archive is completed while settlement keeps effects active before Finished");
                c.Archive.TryGetRunSummary(out summary);
                if (mode == "retry") Check(ReferenceEquals(summary, failedSummary), "retry reuses the exact completed RunSummary");
                int expected = CalculateReward(c, summary);
                Check(mode == "positive" || mode == "victory" ? summary.ClearWaveCount >= 1 && expected > 0 :
                    summary.ClearWaveCount == 0 && expected == 0,
                    "the scenario has the expected cleared-wave count and bloodstone amount: " + expected);
                if (mode == "victory")
                    Check(c.Flow.HasClearedMainGame && summary.ClearWaveCount == 1 &&
                        summary.MaxQuarter == WaveController.MAIN_QUARTERS && summary.MaxWave == WaveController.MAX_WAVE,
                        "Finish records only the actual cleared final wave and its real quarter/wave position");
                Check(c.Persistent.Balance == SeedBalance + expected, "settlement grants the computed amount exactly once");
                Check(Read<TMP_Text>(c.Settlement, "_bloodstone").text == expected.ToString("N0"),
                    "settlement displays the granted bloodstone amount");
                Check(Read<TMP_Text>(c.Settlement, "_progress").text == (summary.MaxQuarter > 0 && summary.MaxWave > 0
                    ? $"{summary.MaxQuarter}분기 · {summary.MaxWave}웨이브" : "클리어 기록 없음"),
                    "settlement displays progress from the completed RunSummary");
                summary.Artifacts.TryGetValue(ArtifactRarity.Common, out int common);
                summary.Artifacts.TryGetValue(ArtifactRarity.Rare, out int rare);
                summary.Artifacts.TryGetValue(ArtifactRarity.Legendary, out int legendary);
                summary.Artifacts.TryGetValue(ArtifactRarity.Mythic, out int mythic);
                Check(Read<TMP_Text>(c.Settlement, "_artifacts").text ==
                    $"일반 {common:N0}개\n희귀 {rare:N0}개\n전설 {legendary:N0}개\n신화 {mythic:N0}개",
                    "settlement displays every artifact rarity from the completed RunSummary");
                Check(c.UI.TopPopup == c.Screen(UIId.RunResult) && c.UI.HasBlockingPopup, "registered RunResult is the blocking top popup");
                InGameUIValidation.SimulateEscape(c.UI);
                Check(c.Settlement.IsPending && c.Settlement.IsVisible, "Escape cannot dismiss the required settlement");
                Button start = Read<Button>(c.Hud, "_waveStartButton");
                Check(!start.IsInteractable(), "settlement blocks HUD start input");
                await AssertHudRaycastBlocked(c, start);
                string savedBeforeRepeat = File.ReadAllText(c.Coordinator.SavePath);
                Note("EXPECTED ERROR", "The next deliberate duplicate request logs 이미 정산을 진행하고 있습니다.");
                Check(!await c.Rewards.TryApplyReward(summary), "overlapping manager settlement request is rejected");
                c.Waves.SetFail();
                await c.Flow.ResolveBattleAsync(ResultType.Defeat);
                await UniTask.NextFrame();
                Check(c.Persistent.Balance == SeedBalance + expected && c.Settlement.IsPending && c.Settlement.IsVisible &&
                    File.ReadAllText(c.Coordinator.SavePath) == savedBeforeRepeat, "duplicate settlement and defeat requests neither repay nor replace the popup");
                VerifySaved(c.Save, SeedBalance + expected);
                await CaptureAsync(mode + "-settlement", 1920, 1080);
                await CaptureAsync(mode + "-settlement", 1280, 720);
                Button main = Read<Button>(c.Settlement, "_mainButton");
                await PointerClick(main, "settlement confirmation");
                // 같은 실버튼 경로로 중복 입력을 보낸다. 씬이 이미 파괴됐다면 보내지 않는다.
                if (main != null) ExecuteEvents.Execute(main.gameObject, new PointerEventData(EventSystem.current)
                    { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                await WaitFor(() => SceneManager.GetActiveScene().name == "OutGameSetupTest", "OutGame scene return");
                Check(finishedEvents == 1, "confirmation emits Finished exactly once before scene return");
                var outBootstrap = InScene<OutGameBootstrap>(SceneManager.GetActiveScene()).Single();
                await WaitFor(() => outBootstrap.IsInitialized, "OutGame initialization");
                var outSave = Read<SaveManager>(outBootstrap, "_saveManager");
                var outWallet = Read<PersistentCurrencyManager>(outBootstrap, "_wallet");
                var outTraits = Read<OutGameTraitController>(outBootstrap, "traitController");
                Check(outSave.DirectoryPath == SaveDirectory && outWallet.Balance == SeedBalance + expected,
                    "OutGame loaded the same isolated, settled balance");
                Check(TraitsEqual(outTraits.CaptureSaveData(), SeedTraits()), "OutGame preserved every seeded trait level");
                VerifySaved(outSave, SeedBalance + expected);
                await CaptureAsync(mode + "-outgame", 1920, 1080);
                await CaptureAsync(mode + "-outgame", 1280, 720);
                VerifyProtectedFiles();
                SetResult("PASS (" + _checks + ")\n" + Report);
            }
            catch (Exception exception)
            {
                SetResult("FAIL after " + _checks + " checks\n" + Report + "\n" + exception);
                throw;
            }
            finally
            {
                if (c != null && c.Flow != null) c.Flow.PhaseChanged -= trackFinished;
                if (c != null && c.Save != null) c.Save.ConfigureDirectory(SaveDirectory);
                SessionState.SetBool(Key + "Running", false);
            }
            return LastResult;
        }

        private sealed class Context
        {
            public readonly BootStrap Bootstrap;
            public readonly InGameUIStartup Startup;
            public readonly InGameUIManager UI;
            public readonly GameFlowController Flow;
            public readonly WaveController Waves;
            public readonly RuntimeUnitManager Runtime;
            public readonly ArchiveManager Archive;
            public readonly RunSettlementManager Rewards;
            public readonly SettlementView Settlement;
            public readonly PersistentCurrencyManager Persistent;
            public readonly PersistentSaveCoordinator Coordinator;
            public readonly SaveManager Save;
            public readonly OutGameTraitController Traits;
            public readonly GameHudView Hud;
            public readonly BuildingUIConnection Buildings;
            public readonly BuildingBuildController Controller;
            public readonly BuildingCatalogView Catalog;
            public readonly BuildingActionView Actions;
            public readonly RunCurrencyManager Wallet;
            public readonly ArtifactRewardView ArtifactView;
            public readonly ContinueView Continue;
            public readonly RunDecisionView Decision;
            public readonly BuildingSlot[] Slots;
            public Context(Scene scene)
            {
                if (scene.name != "Test") throw new InvalidOperationException("Run in the Test scene.");
                Bootstrap = InScene<BootStrap>(scene).Single();
                Startup = Read<InGameUIStartup>(Bootstrap, "_uiStartup");
                UI = Startup.UIManager;
                Flow = Read<GameFlowController>(Bootstrap, "_gameFlowController");
                Waves = Read<WaveController>(Bootstrap, "_waveController");
                Runtime = Read<RuntimeUnitManager>(Bootstrap, "_runtimeUnitManager");
                Archive = Read<ArchiveManager>(Bootstrap, "_archiveManager");
                Rewards = Read<RunSettlementManager>(Bootstrap, "_runSettlementManager");
                Settlement = Read<SettlementView>(Startup, "_settlementUI");
                Persistent = Read<PersistentCurrencyManager>(Bootstrap, "_persistentCurrencyManager");
                Coordinator = Read<PersistentSaveCoordinator>(Bootstrap, "_persistentSaveCoordinator");
                Save = Read<SaveManager>(Bootstrap, "_saveManager");
                Traits = Read<OutGameTraitController>(Bootstrap, "_persistentTraits");
                Hud = Read<GameHudView>(Startup, "_hudView");
                Buildings = Read<BuildingUIConnection>(Startup, "_buildingUIConnection");
                Controller = Read<BuildingBuildController>(Bootstrap, "_buildController");
                var presenter = Read<BuildingUIPresenter>(Startup, "_buildingUI");
                Catalog = Read<BuildingCatalogView>(presenter, "_catalog");
                Actions = Read<BuildingActionView>(presenter, "_actions");
                Wallet = Read<RunCurrencyManager>(Bootstrap, "_runCurrencyManager");
                ArtifactView = Read<ArtifactRewardView>(Read<ArtifactRewardPresenter>(Startup, "_artifactSelectionUI"), "_panel");
                Continue = Read<ContinueView>(Startup, "_continueUI");
                Decision = Read<RunDecisionView>(Startup, "_runDecisionUI");
                Slots = InScene<BuildingSlot>(scene).ToArray();
            }
            public UIScreen Screen(UIId id) => UI.TryGetScreen(id, out UIScreen screen) ? screen :
                throw new InvalidOperationException("Unregistered screen: " + id);
        }

        private static async UniTask EnsureArmyBuilding(Context c)
        {
            if (c.Slots.Any(slot => slot.IsOccupied && slot.CurrentBuilding.Data.HasSpawn)) return;
            var shortcut = Read<BuildingShortcut[]>(c.Startup, "_buildingShortcuts").First(item => item.SelectsFirstEmptySlot);
            await PointerClick(Read<Button>(shortcut, "_button"), "construction shortcut");
            var candidates = Read<Dictionary<string, BuildingData>>(c.Buildings, "_byId");
            var items = Read<List<BuildingCatalogItem>>(c.Catalog, "_items");
            int index = items.FindIndex(item => item.GoldCost.HasValue && candidates[item.Id].HasSpawn && candidates[item.Id].HasWorldVisual);
            Check(index >= 0, "actual catalog offers a building that spawns allied units");
            Array cards = Read<Array>(c.Catalog, "_cards");
            while (c.Catalog.PageIndex < index / cards.Length) await PointerClick(Read<Button>(c.Catalog, "_next"), "catalog next");
            UIItemSlot slotView = Read<UIItemSlot>(cards.GetValue(index % cards.Length), "Slot");
            await PointerClick(Read<Button>(slotView, "_button"), "army building candidate");
            BuildingActionOffer offer = Read<BuildingActionViewData>(c.Actions, "_data").Build;
            AddDeficit(c.Wallet, CurrencyType.Gold, offer.GoldAmount);
            AddDeficit(c.Wallet, CurrencyType.Gem, offer.GemAmount);
            await PointerClick(Read<Button>(Read<object>(c.Actions, "_build"), "_button"), "build allied spawner");
            Check(c.Slots.Any(slot => slot.IsOccupied && slot.CurrentBuilding.Data.HasSpawn), "original build API created the allied spawner");
            c.Buildings.ClearSelection();
        }

        private static void AddDeficit(RunCurrencyManager wallet, CurrencyType currency, int required)
        {
            int deficit = Math.Max(0, required - wallet.GetBalance(currency));
            if (deficit == 0) return;
            Check(wallet.TryAdd(currency, deficit), "runtime build fixture adds only the missing " + currency + ": " + deficit);
        }

        private static async UniTask StartBattle(Context c)
        {
            await WaitFor(c.Flow.CanEnterBuildMode, "Preparation");
            await PointerClick(Read<Button>(c.Hud, "_waveStartButton"), "HUD start wave");
            await WaitFor(() => c.Flow.CurPhase == GamePhase.Battle && c.Runtime.IsPreparationCompleted, "real unit preparation and Battle");
            Check(c.Runtime.AllyUnits.Any(unit => unit != null && unit.IsAlive) &&
                c.Runtime.EnemyUnits.Any(unit => unit != null && unit.IsAlive), "both teams have real live registered units");
        }

        private static void KillTeam(Context c, UnitTeam team)
        {
            Unit_Gateway[] targets = (team == UnitTeam.Ally ? c.Runtime.AllyUnits : c.Runtime.EnemyUnits).ToArray();
            Unit_Gateway attacker = (team == UnitTeam.Ally ? c.Runtime.EnemyUnits : c.Runtime.AllyUnits).First(unit => unit != null && unit.IsAlive);
            int deaths = 0;
            Action<Unit_Gateway> record = unit => { if (unit.Team == team) deaths++; };
            c.Runtime.UnitDied += record;
            try
            {
                foreach (Unit_Gateway unit in targets)
                {
                    if (unit == null || !unit.IsAlive) continue;
                    CombatApplicationResult applied = unit.TakeDamageWithResult(new DamageResult(attacker, unit,
                        unit.CurrentHp + unit.CurrentShield + 1f, DamageSourceType.BasicAttack, false));
                    Check(applied.WasApplied && !unit.IsAlive, "public damage confirms death for " + team + " unit");
                }
                Check(deaths > 0 && c.Flow.CurPhase == GamePhase.BattleResolving,
                    "registered unit deaths invoke WaveController result evaluation");
                Check(team == UnitTeam.Ally ? c.Runtime.AllyUnitCount == 0 && c.Runtime.EnemyUnitCount > 0 :
                    c.Runtime.EnemyUnitCount == 0 && c.Runtime.AllyUnitCount > 0, "only the intended team was eliminated before cleanup");
            }
            finally { c.Runtime.UnitDied -= record; }
        }

        private static async UniTask ClearOneWave(Context c)
        {
            await StartBattle(c);
            Note("INPUT", "Positive-reward fixture eliminates enemies through the public damage/death path, then uses real reward buttons.");
            KillTeam(c, UnitTeam.Enemy);
            await ResolveVictoryContents(c);
            await WaitFor(c.Flow.CanEnterBuildMode, "preparation after the first cleared wave");
            Check(c.Archive.CaptureSaveData().ClearWaveCount >= 1, "the archive recorded a real cleared wave");
        }

        private static async UniTask ResolveVictoryContents(Context c)
        {
            await WaitFor(() => c.ArtifactView.IsVisible || c.Flow.CurPhase == GamePhase.Event ||
                c.Flow.CurPhase == GamePhase.Store || c.Flow.CanEnterBuildMode() || c.Decision.IsPending, "victory reward");
            if (c.ArtifactView.IsVisible)
            {
                Array cards = Read<Array>(c.ArtifactView, "_cards");
                await PointerClick(Read<Button>(cards.GetValue(0), "Button"), "artifact candidate");
                await PointerClick(Read<Button>(c.ArtifactView, "_confirmButton"), "artifact confirmation");
            }
            await WaitFor(() => c.Continue.IsPending || c.Flow.CanEnterBuildMode() || c.Decision.IsPending,
                "post-reward content, preparation or quarter decision");
            if (c.Continue.IsPending) await PointerClick(Read<Button>(c.Continue, "_continueButton"), "event/store continuation");
            await WaitFor(() => c.Flow.CanEnterBuildMode() || c.Decision.IsPending, "content completion");
        }

        private static int CalculateReward(Context c, RunSummary summary)
        {
            CurrencyCatalog catalog = Read<CurrencyCatalog>(c.Rewards, "_currencyCatalog");
            if (!catalog.TryGetByType(CurrencyType.Bloodstone, out CurrencyData currency))
                throw new InvalidOperationException("No configured bloodstone currency.");
            EffectManager effects = Read<EffectManager>(c.Bootstrap, "_effectManager");
            TotemRunApplier totems = Read<TotemRunApplier>(c.Bootstrap, "_totemRunApplier");
            int baseAmount = new CurrencyRewardCalculator().CalculateRunSettlementReward(currency,
                summary.ClearWaveCount, summary.ClearBossCount, effects.CurrencyModifiers).Amount;
            return baseAmount + totems.CalculateBonusBloodstone(baseAmount);
        }

        private static async UniTask PointerClick(Button button, string label)
        {
            await WaitForCanvas();
            if (button == null || !button.gameObject.activeInHierarchy || !button.IsInteractable())
                throw new InvalidOperationException(label + " button is not active/interactable.");
            EventSystem events = EventSystem.current;
            if (events == null) throw new InvalidOperationException("No active EventSystem.");
            PointerEventData pointer = PointerAt(events, button);
            var hits = new List<RaycastResult>();
            events.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject,
                label + " is the actual top raycast click target");
            pointer.pointerCurrentRaycast = hits[0];
            pointer.pointerPressRaycast = hits[0];
            pointer.pointerPress = button.gameObject;
            pointer.pressPosition = pointer.position;
            pointer.eligibleForClick = true;
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static async UniTask AssertHudRaycastBlocked(Context c, Button start)
        {
            await WaitForCanvas();
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(PointerAt(EventSystem.current, start), hits);
            Check(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(c.Screen(UIId.RunResult).Root.transform) &&
                !hits.Any(hit => hit.gameObject == start.gameObject || hit.gameObject.transform.IsChildOf(start.transform)),
                "the real settlement backdrop intercepts raycasts over the HUD");
        }

        private static PointerEventData PointerAt(EventSystem events, Button button)
        {
            var rect = (RectTransform)button.transform;
            Canvas canvas = button.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return new PointerEventData(events) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)) };
        }

        private static async UniTask WaitForCanvas()
        {
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>실제 Game View 해상도를 잠시 바꿔 현재 플레이 화면을 PNG로 저장한 뒤 원상복구한다.</summary>
        public static void Capture(string label, int width = 1920, int height = 1080) =>
            CaptureAsync(label, width, height).Forget(Debug.LogException);

        public static async UniTask<string> CaptureAsync(string label, int width, int height)
        {
            if (!EditorApplication.isPlaying || !Armed) throw new InvalidOperationException("Capture in an armed Play session.");
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView", true);
            EditorWindow window = EditorWindow.GetWindow(gameViewType);
            PropertyInfo selected = gameViewType.GetProperty("selectedSizeIndex", Fields);
            int oldIndex = (int)selected.GetValue(window);
            Type sizesType = gameViewType.Assembly.GetType("UnityEditor.GameViewSizes", true);
            object sizes = sizesType.BaseType.GetProperty("instance", Static).GetValue(null);
            object groupType = sizesType.GetProperty("currentGroupType", Fields).GetValue(sizes);
            object group = sizesType.GetMethod("GetGroup", Fields).Invoke(sizes, new[] { groupType });
            Type groupClass = group.GetType();
            int builtIn = (int)groupClass.GetMethod("GetBuiltinCount", Fields).Invoke(group, null);
            int custom = (int)groupClass.GetMethod("GetCustomCount", Fields).Invoke(group, null);
            Type sizeType = gameViewType.Assembly.GetType("UnityEditor.GameViewSize", true);
            Type kindType = gameViewType.Assembly.GetType("UnityEditor.GameViewSizeType", true);
            object size = Activator.CreateInstance(sizeType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { Enum.Parse(kindType, "FixedResolution"), (object)width, height, "Settlement Validation" }, null);
            groupClass.GetMethod("AddCustomSize", Fields).Invoke(group, new[] { size });
            Texture2D image = null;
            try
            {
                selected.SetValue(window, builtIn + custom);
                window.Focus();
                window.Repaint();
                await WaitFor(() => Screen.width == width && Screen.height == height, "Game View rendering resolution");
                await WaitForCanvas();
                await UniTask.WaitForEndOfFrame();
                image = ScreenCapture.CaptureScreenshotAsTexture();
                if (image == null || image.width != width || image.height != height)
                    throw new InvalidOperationException("Screenshot dimensions do not match the real Game View.");
                string safeLabel = string.Concat(label.Where(character => char.IsLetterOrDigit(character) || character == '-' || character == '_'));
                string path = Path.Combine(OutputDirectory, safeLabel + "-" + width + "x" + height + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                Note("CAPTURE", path);
                return path;
            }
            finally
            {
                if (image != null) Object.Destroy(image);
                selected.SetValue(window, oldIndex);
                groupClass.GetMethod("RemoveCustomSize", Fields).Invoke(group, new object[] { builtIn + custom });
                window.Repaint();
            }
        }

        private static void VerifySaved(SaveManager save, int amount)
        {
            Check(save.DirectoryPath == SaveDirectory, "save manager remains isolated");
            if (!save.TryLoad(PersistentSaveCoordinator.SaveKey, out PersistentSaveData data, out string error))
                throw new IOException("Could not read settled profile: " + error);
            Check(data.Wallet.Amount == amount && TraitsEqual(data.Traits, SeedTraits()),
                "saved profile contains the settled bloodstones and unchanged trait levels");
        }

        private static TraitSaveData SeedTraits() => JsonUtility.FromJson<TraitSaveData>(SessionState.GetString(Key + "SeedTraits", ""));
        private static bool TraitsEqual(TraitSaveData a, TraitSaveData b) => a != null && b != null && a.Version == b.Version &&
            a._levels.Count == b._levels.Count && a._levels.All(left => b._levels.Any(right => left.Id == right.Id && left.Level == right.Level));

        private static void VerifyProtectedFiles()
        {
            var stamps = JsonUtility.FromJson<FileStamps>(SessionState.GetString(Key + "ProtectedFiles", ""));
            if (stamps == null) throw new InvalidOperationException("Protected file baseline is missing.");
            foreach (FileStamp stamp in stamps.Items)
                if (Hash(stamp.Path) != stamp.Hash) throw new InvalidOperationException("Protected file changed: " + stamp.Path);
            Note("PROTECTION", "Original font assets and default persistent profile match their pre-test bytes.");
        }

        private static string Hash(string path)
        {
            if (!File.Exists(path)) return "missing";
            using (SHA256 hash = SHA256.Create()) using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(hash.ComputeHash(stream));
        }

        private static string NormalizeMode(string mode)
        {
            mode = mode?.Trim().ToLowerInvariant();
            if (mode != "zero" && mode != "positive" && mode != "forced" && mode != "victory" && mode != "retry")
                throw new ArgumentException("Use zero, positive, forced, victory, or retry.", nameof(mode));
            return mode;
        }

        private static IEnumerable<T> InScene<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

        private static T Read<T>(object owner, string field)
        {
            FieldInfo info = owner?.GetType().GetField(field, Fields);
            if (info == null) throw new MissingFieldException(owner?.GetType().Name, field);
            object value = info.GetValue(owner);
            if (value == null) throw new InvalidOperationException(owner.GetType().Name + "." + field + " is not connected.");
            return (T)value;
        }

        private static async UniTask WaitFor(Func<bool> condition, string label)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + TimeoutSeconds;
            while (!condition())
            {
                if (!EditorApplication.isPlaying) throw new OperationCanceledException("Play stopped during " + label);
                if (Time.realtimeSinceStartupAsDouble >= deadline) throw new TimeoutException(label + " exceeded " + TimeoutSeconds + " seconds.");
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }

        private static void Check(bool success, string message)
        {
            if (!success) throw new InvalidOperationException(message);
            _checks++;
            Note("PASS", message);
        }
        private static void Note(string kind, string message) => Report.Append('[').Append(kind).Append("] ").AppendLine(message);
        private static void SetResult(string result)
        {
            SessionState.SetString(Key + "Result", result);
            File.WriteAllText(Path.Combine(OutputDirectory, "results.txt"), result, Encoding.UTF8);
            Debug.Log("[RunSettlementPlayValidation] " + result);
        }
    }
}
#endif
