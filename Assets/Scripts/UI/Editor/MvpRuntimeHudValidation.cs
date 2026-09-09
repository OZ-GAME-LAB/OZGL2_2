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
        private const string ActiveKey = "Game.UI.RuntimeValidation.Active";
        private const string ResultKey = "Game.UI.RuntimeValidation.Result";
        private const string PreviousSceneKey = "Game.UI.RuntimeValidation.PreviousScene";
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
        }

        [MenuItem("Game/UI/Validate Runtime HUD In Play Mode")]
        public static void Run()
        {
            if (_isQueued) throw new InvalidOperationException("Validation is already queued.");
            EnsureReady();
            // Let Editor startup callbacks create their resources before requesting Play.
            // Search's first-use index creation is skipped once Play is already pending.
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
                MvpRuntimeHudBuilder.Build();
                EditorSceneManager.OpenScene(MvpRuntimeHudBuilder.ScenePath, OpenSceneMode.Single);
                SessionState.SetInt(ResultKey, 1);
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
            if (state == PlayModeStateChange.EnteredPlayMode) RunChecksAsync().Forget();
            if (state != PlayModeStateChange.EnteredEditMode) return;
            SessionState.SetBool(ActiveKey, false);
            int result = SessionState.GetInt(ResultKey, 1);
            if (Application.isBatchMode)
                EditorApplication.Exit(result);
            else RestoreSceneSetup();
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
                Check(waveText.text == "1 / 3" && start.interactable, "initial wave and input unlocked after phase event");
                add.onClick.Invoke();
                Check(wallet.GetBalance(CurrencyType.Gold) == 150 && goldText.text == "150", "real add event updates HUD");
                spend.onClick.Invoke();
                Check(wallet.GetBalance(CurrencyType.Gold) == 120 && goldText.text == "120", "real spending updates HUD");
                reject.onClick.Invoke();
                Check(wallet.GetBalance(CurrencyType.Gold) == 120 && goldText.text == "120", "insufficient spending is atomic");
                var catalog = AssetDatabase.LoadAssetAtPath<CurrencyCatalog>("Assets/Data/Economy/S.O/CurrencyCatalog.asset");
                Check(catalog.TryGetByType(CurrencyType.Gem, out var gem), "gem is registered");
                Check(wallet.TryAdd(new CurrencyAmount(gem, 7)) && goldText.text == "120", "non-gold currency does not replace HUD gold");

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

                for (int wave = 1; wave <= 3; wave++)
                {
                    Check(waveText.text == wave + " / 3" && start.interactable, "wave " + wave + " ready");
                    start.onClick.Invoke();
                    await WaitForPhase(flow, GamePhase.Battle);
                    Check(phaseText.text == "전투" && !start.interactable, "wave " + wave + " battle");
                    win.onClick.Invoke();
                    Check(flow.CurPhase == GamePhase.Reward && phaseText.text == "보상", "wave " + wave + " reward phase");
                    int beforeReward = wallet.GetBalance(CurrencyType.Gold);
                    reward.onClick.Invoke();
                    reward.onClick.Invoke();
                    Check(wallet.GetBalance(CurrencyType.Gold) == beforeReward + 30 &&
                        goldText.text == (beforeReward + 30).ToString("N0"), "wave " + wave + " test reward applied once by real manager");
                    await WaitForPhase(flow, wave == 3 ? GamePhase.Finished : GamePhase.Preparation);
                }
                Check(waves.CurWave == 3 && waveText.text == "3 / 3" && phaseText.text == "결과", "last wave stops at three");
                Check(wallet.GetBalance(CurrencyType.Gold) == 190 && goldText.text == "190", "three actual reward transactions reflected");
                reset.onClick.Invoke();
                Check(waveText.text == "1 / 3" && goldText.text == "100" && start.interactable, "restart resets core/economy/view");
                // Let only the button color transition settle before visual capture.
                await UniTask.Delay(TimeSpan.FromSeconds(0.2), DelayType.Realtime);
                Capture(ui, sample, 1280, 720);
                Capture(ui, sample, 1920, 1080);
                Check(_errors == 0, "no new engine errors during Play checks");
                Debug.Log("[UI/MvpRuntimeHudValidation] PASS: " + _checks +
                    " checks in actual Play Mode. Real RunCurrencyManager/GameFlowController/WaveController; test spawner and reward inputs.");
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

        private static void Capture(GameUIController ui, MvpRuntimeHudSample sample, int width, int height)
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
                File.WriteAllBytes("Logs/RuntimeHudValidation/hud-" + width + "x" + height + ".png", pixels.EncodeToPNG());
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
