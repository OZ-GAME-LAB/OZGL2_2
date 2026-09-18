using System;
using System.IO;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.UI.Samples;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Editor
{
    /// <summary>실제 PlayerLoop와 팀원 매니저를 사용하는 Play Mode 통합 검사.</summary>
    [InitializeOnLoad]
    public static class MvpRuntimeHudValidation
    {
        private const string VictoryKey = "Game.UI.RuntimeValidation.VictoryReward";
        private const string ActiveKey = "Game.UI.RuntimeValidation.Active";
        private const string ResultKey = "Game.UI.RuntimeValidation.Result";
        private const string PreviousSceneKey = "Game.UI.RuntimeValidation.PreviousScene";
        private const string SelectionKey = "Game.UI.RuntimeValidation.UnitSelection";
        private const string CountKey = "Game.UI.RuntimeValidation.UnitCount";
        private const string ArtifactKey = "Game.UI.RuntimeValidation.ArtifactReward";
        private const string BuildingPhaseKey = "Game.UI.RuntimeValidation.BuildingPhase";
        private const string LifecycleErrorKey = "Game.UI.RuntimeValidation.LifecycleError";
        private static int _checks;
        private static int _errors;
        private static bool _isQueued;

        [Serializable]
        private sealed class SceneSnapshot
        {
            public SavedScene[] Scenes;
        }

        [Serializable]
        private struct SavedScene
        {
            public string Path;
            public bool IsLoaded;
            public bool IsActive;
        }

        static MvpRuntimeHudValidation()
        {
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            Application.logMessageReceived += HandleValidationLifecycleLog;
        }

        [MenuItem("Game/UI/Validate Runtime HUD In Play Mode")]
        public static void Run()
        {
            if (_isQueued) throw new InvalidOperationException("Validation is already queued.");
            EnsureReady();
            SessionState.SetBool(VictoryKey, false);
            SessionState.SetBool(SelectionKey, false);
            SessionState.SetBool(BuildingPhaseKey, false);
            SessionState.SetBool(CountKey, false);
            SessionState.SetBool(ArtifactKey, false);
            // Let Editor startup callbacks create their resources before requesting Play.
            // Search's first-use index creation is skipped once Play is already pending.
            _isQueued = true;
            EditorApplication.delayCall += BeginValidation;
        }

        [MenuItem("Game/UI/Validate Runtime Unit Selection In Play Mode")]
        public static void RunUnitSelection()
        {
            if (_isQueued) throw new InvalidOperationException("Validation is already queued.");
            EnsureReady();
            SessionState.SetBool(VictoryKey, false);
            SessionState.SetBool(BuildingPhaseKey, false);
            SessionState.SetBool(SelectionKey, true);
            SessionState.SetBool(CountKey, false);
            SessionState.SetBool(ArtifactKey, false);
            _isQueued = true;
            EditorApplication.delayCall += BeginValidation;
        }

        [MenuItem("Game/UI/Validate Runtime Unit Counts In Play Mode")]
        public static void RunUnitCounts()
        {
            if (_isQueued) throw new InvalidOperationException("Validation is already queued.");
            EnsureReady();
            SessionState.SetBool(VictoryKey, false);
            SessionState.SetBool(BuildingPhaseKey, false);
            SessionState.SetBool(SelectionKey, true);
            SessionState.SetBool(CountKey, true);
            SessionState.SetBool(ArtifactKey, false);
            _isQueued = true;
            EditorApplication.delayCall += BeginValidation;
        }

        [MenuItem("Game/UI/Validate Artifact Reward In Play Mode")]
        public static void RunArtifacts()
        {
            if (_isQueued) throw new InvalidOperationException("Validation is already queued.");
            EnsureReady();
            SessionState.SetBool(VictoryKey, false);
            SessionState.SetBool(BuildingPhaseKey, false);
            SessionState.SetBool(SelectionKey, true);
            SessionState.SetBool(CountKey, false);
            SessionState.SetBool(ArtifactKey, true);
            _isQueued = true;
            EditorApplication.delayCall += BeginValidation;
        }

        [MenuItem("Game/UI/Validate Building Phase In Play Mode")]
        public static void RunBuildingPhase()
        {
            if (_isQueued) throw new InvalidOperationException("Validation is already queued.");
            EnsureReady();
            SessionState.SetBool(VictoryKey, false);
            SessionState.SetBool(SelectionKey, true);
            SessionState.SetBool(CountKey, false);
            SessionState.SetBool(ArtifactKey, false);
            SessionState.SetBool(BuildingPhaseKey, true);
            _isQueued = true;
            EditorApplication.delayCall += BeginValidation;
        }

        [MenuItem("Game/UI/Validate Victory Reward Integration In Play Mode")]
        public static void RunVictoryRewards()
        {
            if (_isQueued) throw new InvalidOperationException("Validation is already queued.");
            EnsureReady();
            SessionState.SetBool(VictoryKey, true);
            SessionState.SetBool(SelectionKey, true);
            SessionState.SetBool(CountKey, false);
            SessionState.SetBool(ArtifactKey, false);
            SessionState.SetBool(BuildingPhaseKey, false);
            _isQueued = true;
            EditorApplication.delayCall += BeginValidation;
        }

        private static void BeginValidation()
        {
            _isQueued = false;
            bool savedSetup = false;
            try
            {
                EnsureReady();
                SaveSceneSetup();
                savedSetup = true;
                if (SessionState.GetBool(VictoryKey, false))
                {
                    // 기존 씬/프리팹 원본을 생성하거나 저장하지 않고, 메모리상의 테스트 씬만 연결한다.
                    EditorSceneManager.OpenScene(MvpRuntimeHudBuilder.ScenePath, OpenSceneMode.Single);
                }
                else if (SessionState.GetBool(BuildingPhaseKey, false))
                {
                    MvpBuildingPhaseBuilder.Build();
                    EditorSceneManager.OpenScene(MvpBuildingPhaseBuilder.ScenePath, OpenSceneMode.Single);
                }
                else if (SessionState.GetBool(ArtifactKey, false))
                {
                    MvpArtifactRewardBuilder.Build();
                    EditorSceneManager.OpenScene(MvpArtifactRewardBuilder.ScenePath, OpenSceneMode.Single);
                }
                else if (SessionState.GetBool(CountKey, false))
                {
                    MvpRuntimeUnitCountBuilder.Build();
                    EditorSceneManager.OpenScene(MvpRuntimeUnitCountBuilder.ScenePath, OpenSceneMode.Single);
                }
                else if (SessionState.GetBool(SelectionKey, false))
                {
                    MvpRuntimeUnitSelectionBuilder.Build();
                    EditorSceneManager.OpenScene(MvpRuntimeUnitSelectionBuilder.ScenePath, OpenSceneMode.Single);
                }
                else
                {
                    MvpRuntimeHudBuilder.Build();
                    EditorSceneManager.OpenScene(MvpRuntimeHudBuilder.ScenePath, OpenSceneMode.Single);
                }
                var testScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (SessionState.GetBool(VictoryKey, false))
                    MvpVictoryRewardSetup.ConfigureScene(testScene, false);
                else if (MvpEconomyUiSetup.IsSupportedScene(testScene.path))
                {
                    MvpEconomyUiSetup.ConfigureScene(testScene, false);
                    // 최신 Core는 수동 재화 테스트에서도 실제 유물 매니저 참조를 요구한다.
                    MvpVictoryRewardSetup.ConfigureScene(testScene, false);
                }
                if (!SessionState.GetBool(VictoryKey, false))
                {
                    // 사용자가 승리 UI 연결을 저장한 씬도 기존 수동 지급 검사는 독립적으로 검사한다.
                    // 테스트용 메모리 참조만 끊고, 원본 씬은 저장하지 않는다.
                    foreach (var root in testScene.GetRootGameObjects())
                        foreach (var sample in root.GetComponentsInChildren<MvpRuntimeHudSample>(true))
                        {
                            var fields = new SerializedObject(sample);
                            fields.FindProperty("_artifactRewards").objectReferenceValue = null;
                            fields.ApplyModifiedPropertiesWithoutUndo();
                        }
                }
                SessionState.SetInt(ResultKey, 1);
                SessionState.SetBool(LifecycleErrorKey, false);
                SessionState.SetBool(ActiveKey, true);
                EditorApplication.isPlaying = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetBool(ActiveKey, false);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else if (savedSetup) RestoreSceneSetup();
            }
        }

        [MenuItem("Game/UI/Validate Existing UI Regression")]
        public static void RunRegression()
        {
            EnsureReady();
            SessionState.SetBool(VictoryKey, false);
            SaveSceneSetup();
            try
            {
                MvpRuntimeHudBuilder.Build();
                // Existing validators open additive scenes, so start from a saved scene.
                EditorSceneManager.OpenScene(MvpRuntimeHudBuilder.ScenePath, OpenSceneMode.Single);
                MvpUnitUiValidation.Run();
            }
            finally
            {
                if (!Application.isBatchMode) RestoreSceneSetup();
            }
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                if (SessionState.GetBool(SelectionKey, false)) RunSelectionChecksAsync().Forget();
                else RunChecksAsync().Forget();
            }
            if (state != PlayModeStateChange.EnteredEditMode) return;
            SessionState.SetBool(ActiveKey, false);
            int result = SessionState.GetBool(LifecycleErrorKey, false) ? 1 : SessionState.GetInt(ResultKey, 1);
            if (Application.isBatchMode)
                EditorApplication.Exit(result);
            else RestoreSceneSetup();
        }

        private static void HandleValidationLifecycleLog(string message, string stackTrace, LogType type)
        {
            // 마지막 검사 이후 Play Mode 종료 과정의 파괴 시점 예외도 실패로 기록한다.
            if (SessionState.GetBool(ActiveKey, false) &&
                (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                SessionState.SetBool(LifecycleErrorKey, true);
        }

        private static async UniTask RunChecksAsync()
        {
            _checks = 0;
            _errors = 0;
            Application.logMessageReceived += HandleLog;
            try
            {
                await UniTask.NextFrame();
                var sample = UnityEngine.Object.FindFirstObjectByType<MvpRuntimeHudSample>();
                Check(sample != null && sample.IsReady, "real core/economy startup");
                var ui = UnityEngine.Object.FindFirstObjectByType<GameUIController>();
                var wallet = UnityEngine.Object.FindFirstObjectByType<RunCurrencyManager>();
                var flow = UnityEngine.Object.FindFirstObjectByType<GameFlowController>();
                var waves = UnityEngine.Object.FindFirstObjectByType<WaveController>();
                var coreBinding = ui.GetComponent<CoreHudBinding>();
                var goldBinding = ui.GetComponent<RunGoldHudBinding>();
                var uiFields = new SerializedObject(ui);
                var goldText = Field<TMP_Text>(uiFields, "_goldText");
                var waveText = Field<TMP_Text>(uiFields, "_waveText");
                var phaseText = Field<TMP_Text>(uiFields, "_phaseText");
                var start = Field<Button>(uiFields, "_waveStartButton");
                var message = Field<GameObject>(uiFields, "_messagePanel");
                var sampleFields = new SerializedObject(sample);
                var add = Field<Button>(sampleFields, "_addGoldButton");
                var spend = Field<Button>(sampleFields, "_spendGoldButton");
                var reject = Field<Button>(sampleFields, "_rejectSpendButton");
                var win = Field<Button>(sampleFields, "_winButton");
                var reward = Field<Button>(sampleFields, "_rewardButton");
                var lose = Field<Button>(sampleFields, "_loseButton");
                var reset = Field<Button>(sampleFields, "_resetButton");
                var toggle = Field<Button>(sampleFields, "_toggleHudButton");
                Check(wallet.GetBalance(CurrencyType.Gold) == 100 && goldText.text == "100", "initial authoritative gold");
                Check(flow.CurPhase == GamePhase.Preparation && phaseText.text == "건설", "initial core phase");
                int wavesPerQuarter = WaveController.MAX_WAVE;
                Check(waveText.text == $"1 / {wavesPerQuarter}" && start.interactable, "initial wave and input unlocked after phase event");
                add.onClick.Invoke();
                Check(wallet.GetBalance(CurrencyType.Gold) == 150 && goldText.text == "150", "real add event updates HUD");
                spend.onClick.Invoke();
                Check(wallet.GetBalance(CurrencyType.Gold) == 120 && goldText.text == "120", "real spending updates HUD");
                reject.onClick.Invoke();
                Check(wallet.GetBalance(CurrencyType.Gold) == 120 && goldText.text == "120", "insufficient spending is atomic");
                var catalog = AssetDatabase.LoadAssetAtPath<CurrencyCatalog>("Assets/Data/Economy/S.O/CurrencyCatalog.asset");
                Check(catalog.TryGetByType(CurrencyType.Gem, out var gem), "gem is registered");
                Check(wallet.TryAdd(CurrencyType.Gem, 7) && goldText.text == "120", "non-gold currency does not replace HUD gold");

                toggle.onClick.Invoke();
                Check(!ui.gameObject.activeSelf, "HUD hides");
                add.onClick.Invoke();
                Check(goldText.text == "120" && wallet.GetBalance(CurrencyType.Gold) == 170, "disabled HUD unsubscribes without changing economy");
                toggle.onClick.Invoke();
                Check(goldText.text == "170" && start.interactable, "enable reads latest state");
                goldBinding.Initialize(ui, wallet);
                goldBinding.Initialize(ui, wallet);
                coreBinding.Initialize(ui, flow, waves);
                coreBinding.Initialize(ui, flow, waves);
                add.onClick.Invoke();
                Check(goldText.text == "220" && wallet.GetBalance(CurrencyType.Gold) == 220, "repeated binding preserves one action");
                Check(wallet.TryEndRun(), "end real currency run");
                goldBinding.Refresh();
                Check(goldText.text == "--", "uninitialized balance is unknown instead of zero");
                reset.onClick.Invoke();
                Check(wallet.GetBalance(CurrencyType.Gold) == 100 && goldText.text == "100", "explicit refresh after run initialization");

                int preparingCount = 0;
                Action<GamePhase> countPreparing = phase => { if (phase == GamePhase.BattlePreparing) preparingCount++; };
                flow.PhaseChanged += countPreparing;
                start.onClick.Invoke();
                start.onClick.Invoke();
                Check(preparingCount == 1 && flow.CurPhase == GamePhase.BattlePreparing, "double click creates one start");
                Check(!start.interactable && coreBinding.IsStartPending && phaseText.text == "전투 준비", "input locks until actual Battle entry");
                reset.onClick.Invoke();
                Check(flow.CurPhase == GamePhase.Preparation && start.interactable && !coreBinding.IsStartPending, "reset cancels old request and unlocks new run");
                start.onClick.Invoke();
                await WaitForPhase(flow, GamePhase.Battle);
                Check(preparingCount == 2 && !coreBinding.IsStartPending && !start.interactable, "new request survives old canceled completion");
                Check(!message.activeSelf && phaseText.text == "전투", "old cancellation does not show stale message");
                flow.PhaseChanged -= countPreparing;
                lose.onClick.Invoke();
                Check(flow.CurPhase == GamePhase.BattleResolving && phaseText.text == "전투 정산", "defeat waits for core staging");
                await WaitForPhase(flow, GamePhase.Finished);
                Check(flow.CurPhase == GamePhase.Finished && phaseText.text == "결과", "actual core defeat changes phase");
                Check(!start.interactable && !Field<GameObject>(uiFields, "_runResultPanel").activeSelf,
                    "no guessed win/loss result when core has no result payload");

                reset.onClick.Invoke();
                start.onClick.Invoke();
                toggle.onClick.Invoke();
                await WaitForPhase(flow, GamePhase.Battle);
                toggle.onClick.Invoke();
                Check(phaseText.text == "전투" && !start.interactable && !coreBinding.IsStartPending,
                    "hide during async start keeps core running and restores current phase");
                reset.onClick.Invoke();

                int totalWaveGold = 100;
                for (int wave = 1; wave <= wavesPerQuarter; wave++)
                {
                    Check(waveText.text == $"{wave} / {wavesPerQuarter}" && start.interactable, "wave " + wave + " ready");
                    start.onClick.Invoke();
                    await WaitForPhase(flow, GamePhase.Battle);
                    Check(phaseText.text == "전투" && !start.interactable, "wave " + wave + " battle");
                    win.onClick.Invoke();
                    Check(flow.CurPhase == GamePhase.BattleResolving && !start.interactable, "wave " + wave + " staging locks start");
                    await WaitForPhase(flow, GamePhase.Reward);
                    Check(flow.CurPhase == GamePhase.Reward && phaseText.text == "보상", "wave " + wave + " reward phase");
                    int beforeReward = wallet.GetBalance(CurrencyType.Gold);
                    int expectedReward = MvpEconomyUiValidation.GetExpectedReward(waves, CurrencyType.Gold);
                    int expectedGem = wallet.GetBalance(CurrencyType.Gem) +
                        MvpEconomyUiValidation.GetExpectedReward(waves, CurrencyType.Gem);
                    totalWaveGold += expectedReward;
                    reward.onClick.Invoke();
                    reward.onClick.Invoke();
                    Check(wallet.GetBalance(CurrencyType.Gold) == beforeReward + expectedReward &&
                        goldText.text == (beforeReward + expectedReward).ToString("N0"), "wave " + wave + " table reward applied once by real manager");
                    Check(wallet.GetBalance(CurrencyType.Gem) == expectedGem, "wave " + wave + " table Gem reward");
                    await WaitForPhaseAfterContentAsync(flow, GamePhase.Preparation, phaseText);
                }
                Check(waves.CurQuarter == 2 && waves.CurWave == 1 && waveText.text == $"1 / {wavesPerQuarter}" && phaseText.text == "건설", "first quarter continues to next quarter");
                Check(wallet.GetBalance(CurrencyType.Gold) == totalWaveGold && goldText.text == totalWaveGold.ToString("N0"), "all first-quarter reward transactions reflected");
                reset.onClick.Invoke();
                Check(waveText.text == $"1 / {wavesPerQuarter}" && goldText.text == "100" && start.interactable, "restart resets core/economy/view");
                await MvpEconomyUiValidation.RunChecksAsync();
                await MvpCoreQuarterValidation.RunChecksAsync();
                // Let only the button color transition settle before visual capture.
                await UniTask.Delay(TimeSpan.FromSeconds(0.2), DelayType.Realtime);
                Capture(ui, sample, 1280, 720);
                Capture(ui, sample, 1920, 1080);
                await MvpRuntimeUnitInfoValidation.RunChecksAsync();
                Check(_errors == 0, "no new engine errors during Play checks");
                Debug.Log("[UI/MvpRuntimeHudValidation] PASS: " + _checks +
                    " checks in actual Play Mode. Real RunCurrencyManager/GameFlowController/WaveController; test spawner and team wave reward table.");
                SessionState.SetInt(ResultKey, 0);
            }
            catch (Exception exception)
            {
                SessionState.SetInt(ResultKey, 1);
                Debug.LogException(exception);
            }
            finally
            {
                Application.logMessageReceived -= HandleLog;
                EditorApplication.isPlaying = false;
            }
        }

        private static async UniTask RunSelectionChecksAsync()
        {
            _errors = 0;
            Application.logMessageReceived += HandleLog;
            try
            {
                if (SessionState.GetBool(VictoryKey, false)) await MvpVictoryRewardValidation.RunChecksAsync();
                else if (SessionState.GetBool(BuildingPhaseKey, false)) await MvpBuildingPhaseValidation.RunChecksAsync();
                else if (SessionState.GetBool(ArtifactKey, false)) await MvpArtifactRewardValidation.RunChecksAsync();
                else
                {
                    await MvpRuntimeUnitSelectionValidation.RunChecksAsync();
                    if (SessionState.GetBool(CountKey, false)) await MvpRuntimeUnitCountValidation.RunChecksAsync();
                }
                if (_errors != 0) throw new InvalidOperationException("Engine errors occurred during selection checks.");
                SessionState.SetInt(ResultKey, 0);
            }
            catch (Exception exception)
            {
                SessionState.SetInt(ResultKey, 1);
                Debug.LogException(exception);
            }
            finally
            {
                Application.logMessageReceived -= HandleLog;
                EditorApplication.isPlaying = false;
            }
        }

        private static async UniTask WaitForPhase(GameFlowController flow, GamePhase phase)
        {
            float deadline = Time.realtimeSinceStartup + 12;
            while (flow.CurPhase != phase)
            {
                if (Time.realtimeSinceStartup > deadline)
                    throw new TimeoutException("Expected phase " + phase + "; got " + flow.CurPhase);
                await UniTask.NextFrame();
            }
            // PhaseChanged occurs inside the core call; allow completion/finally to finish.
            await UniTask.NextFrame();
        }

        internal static async UniTask WaitForPhaseAfterContentAsync(
            GameFlowController flow, GamePhase phase, TMP_Text phaseText = null)
        {
            var gate = UnityEngine.Object.FindFirstObjectByType<TestWaitingScript>();
            float deadline = Time.realtimeSinceStartup + 12;
            while (flow.CurPhase != phase)
            {
                GamePhase current = flow.CurPhase;
                if (current == GamePhase.Event || current == GamePhase.Store)
                {
                    await UniTask.NextFrame();
                    string label = current == GamePhase.Event ? "이벤트" : "상점";
                    if (phaseText != null && phaseText.text != label)
                        throw new InvalidOperationException($"Expected {label} HUD label, got {phaseText.text}");
                    if (gate == null || !gate.IsWaitingForPostBattleContent)
                        throw new InvalidOperationException($"{current} phase has no pending test content");
                    gate.CompletePostBattleContent(gate.PostBattleRequestId);
                }
                if (Time.realtimeSinceStartup > deadline)
                    throw new TimeoutException($"Expected phase {phase}; got {flow.CurPhase}");
                await UniTask.NextFrame();
            }
            await UniTask.NextFrame();
        }

        private static T Field<T>(SerializedObject fields, string name) where T : UnityEngine.Object
        {
            return (T)fields.FindProperty(name).objectReferenceValue;
        }

        private static void Check(bool condition, string description)
        {
            _checks++;
            if (!condition) throw new InvalidOperationException("Runtime HUD: " + description);
        }

        private static void HandleLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors++;
        }

        private static void EnsureReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scene.isDirty || (!Application.isBatchMode && string.IsNullOrEmpty(scene.path)))
                    throw new InvalidOperationException("Save all current scenes before running validation.");
            }
        }

        private static void SaveSceneSetup()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var snapshot = new SceneSnapshot { Scenes = new SavedScene[setup.Length] };
            for (int i = 0; i < setup.Length; i++)
                snapshot.Scenes[i] = new SavedScene
                {
                    Path = setup[i].path, IsLoaded = setup[i].isLoaded, IsActive = setup[i].isActive
                };
            SessionState.SetString(PreviousSceneKey, JsonUtility.ToJson(snapshot));
        }

        private static void RestoreSceneSetup()
        {
            string json = SessionState.GetString(PreviousSceneKey, "");
            if (string.IsNullOrEmpty(json)) return;
            var snapshot = JsonUtility.FromJson<SceneSnapshot>(json);
            var setup = new SceneSetup[snapshot.Scenes.Length];
            for (int i = 0; i < setup.Length; i++)
                setup[i] = new SceneSetup
                {
                    path = snapshot.Scenes[i].Path,
                    isLoaded = snapshot.Scenes[i].IsLoaded,
                    isActive = snapshot.Scenes[i].IsActive
                };
            EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        internal static void Capture(GameUIController ui, MvpRuntimeHudSample sample, int width, int height, string suffix = "")
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var camera = Camera.main;
            var canvases = new[] { ui.GetComponent<Canvas>(), sample.GetComponent<Canvas>() };
            var scales = new float[canvases.Length];
            var target = new RenderTexture(width, height, 24);
            var previousTarget = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                camera.targetTexture = target;
                for (int i = 0; i < canvases.Length; i++)
                {
                    var canvas = canvases[i];
                    scales[i] = canvas.scaleFactor;
                    canvas.GetComponent<CanvasScaler>().enabled = false;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1 + i;
                    canvas.scaleFactor = Mathf.Sqrt((width / 1920f) * (height / 1080f));
                    Canvas.ForceUpdateCanvases();
                    foreach (var label in canvas.GetComponentsInChildren<TMP_Text>())
                    {
                        label.ForceMeshUpdate();
                        Check(label.font.HasCharacters(label.text, out uint[] missing, false, true),
                            width + " glyphs exist: " + label.name);
                        Check(!label.isTextOverflowing, width + " text fits: " + label.name);
                    }
                }
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("Logs/RuntimeHudValidation");
                File.WriteAllBytes("Logs/RuntimeHudValidation/hud-" + width + "x" + height + suffix + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousTarget;
                camera.targetTexture = null;
                for (int i = 0; i < canvases.Length; i++)
                {
                    canvases[i].renderMode = RenderMode.ScreenSpaceOverlay;
                    canvases[i].worldCamera = null;
                    canvases[i].scaleFactor = scales[i];
                    canvases[i].GetComponent<CanvasScaler>().enabled = true;
                }
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
