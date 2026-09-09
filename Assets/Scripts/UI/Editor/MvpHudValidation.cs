using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI.Editor
{
    /// <summary>추가 테스트 패키지 없이 프리팹의 표시/요청 계약을 검사하는 에디터 도구.</summary>
    public static class MvpHudValidation
    {
        private static int _checks;
        private static int _engineErrors;

        [MenuItem("Game/UI/Validate MVP HUD")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            _checks = 0;
            _engineErrors = 0;
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            Application.logMessageReceived += HandleLogReceived;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MvpHudBuilder.PrefabPath);
                Check(prefab != null, "prefab exists");
                var root = UnityEngine.Object.Instantiate(prefab);
                var ui = root.GetComponent<GameUIController>();
                // Edit Mode does not invoke this non-ExecuteAlways component's OnEnable.
                InvokeLifecycle(ui, "OnEnable");
                var data = new SerializedObject(ui);
                var iterator = data.GetIterator();
                while (iterator.NextVisible(true))
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                        Check(iterator.objectReferenceValue != null, "assigned reference: " + iterator.name);
                var gold = Get<TMP_Text>(data, "_goldText");
                var wave = Get<TMP_Text>(data, "_waveText");
                var phase = Get<TMP_Text>(data, "_phaseText");
                var start = Get<Button>(data, "_waveStartButton");
                var next = Get<Button>(data, "_continueButton");
                var restart = Get<Button>(data, "_restartButton");
                var reward = Get<GameObject>(data, "_waveRewardPanel");
                var result = Get<GameObject>(data, "_runResultPanel");
                var message = Get<GameObject>(data, "_messagePanel");
                ui.Initialize();
                Check(gold.text == "--" && wave.text == "-- / --" && phase.text == "연결 대기", "initial placeholders");
                Check(!start.interactable && !reward.activeSelf && !result.activeSelf && !message.activeSelf, "initial controls");
                ui.SetWaveStartInteractable(true);
                Check(!start.interactable, "no receiver cannot start");
                ui.ShowWaveReward(30, true);
                Check(!next.interactable, "no receiver cannot continue");
                ui.ShowRunResult(true, 90);
                Check(!restart.interactable, "no receiver cannot restart");
                ui.Initialize();

                var startCount = 0;
                var nextCount = 0;
                var restartCount = 0;
                ui.WaveStartRequested += () => startCount++;
                ui.ContinueRequested += () => nextCount++;
                ui.RestartRequested += () => restartCount++;
                ui.SetGold(0);
                Check(gold.text == "0", "zero gold");
                ui.SetGold(int.MaxValue);
                Check(gold.text == int.MaxValue.ToString("N0"), "large gold");
                ExpectRangeError(() => ui.SetGold(-1));
                Check(gold.text == int.MaxValue.ToString("N0"), "invalid gold is atomic");
                ui.SetWaveProgress(3, 3);
                ExpectRangeError(() => ui.SetWaveProgress(0, 3));
                ExpectRangeError(() => ui.SetWaveProgress(4, 3));
                ExpectRangeError(() => ui.SetWaveProgress(1, 0));
                Check(wave.text == "3 / 3", "invalid wave is atomic");
                ui.SetWaveStartInteractable(true);
                start.onClick.Invoke();
                start.onClick.Invoke();
                Check(startCount == 1 && !start.interactable, "start request duplicate guard");
                ui.ShowMessage("테스트: 시작 요청이 거절되었습니다. 다시 시도하세요.");
                ui.SetWaveStartInteractable(true);
                start.onClick.Invoke();
                Check(startCount == 2, "rejection acknowledgement allows retry");
                ui.ShowMessage("   ");
                Check(!message.activeSelf, "blank message hides");

                ui.ShowWaveReward(30, true);
                Check(reward.activeSelf && !result.activeSelf && !start.interactable, "reward modal state");
                next.onClick.Invoke();
                next.onClick.Invoke();
                Check(nextCount == 1 && !next.interactable, "continue duplicate guard");
                ui.ShowWaveReward(30, true);
                next.onClick.Invoke();
                Check(nextCount == 2, "continue retry response");
                ui.ShowWaveReward(30, false);
                next.onClick.Invoke();
                Check(!next.gameObject.activeSelf && nextCount == 2, "no next wave cannot continue");
                ui.ShowRunResult(false, 60);
                Check(!reward.activeSelf && result.activeSelf &&
                    Get<TMP_Text>(data, "_runResultTitleText").text == "패배", "defeat replaces reward");
                restart.onClick.Invoke();
                restart.onClick.Invoke();
                Check(restartCount == 1, "restart duplicate guard");
                ui.ShowRunResult(true, 90);
                Check(Get<TMP_Text>(data, "_runResultTitleText").text == "승리", "victory text");
                restart.onClick.Invoke();
                Check(restartCount == 2, "restart retry response");
                ExpectRangeError(() => ui.ShowRunResult(true, -1));
                ExpectRangeError(() => ui.ShowWaveReward(-1, true));

                for (var i = 0; i < 3; i++)
                {
                    InvokeLifecycle(ui, "OnDisable");
                    InvokeLifecycle(ui, "OnEnable");
                }
                ui.Initialize();
                ui.SetWaveStartInteractable(true);
                start.onClick.Invoke();
                Check(startCount == 3, "enable cycles do not duplicate listeners");
                InvokeLifecycle(ui, "OnDisable");
                ui.SetWaveStartInteractable(true);
                start.onClick.Invoke();
                Check(startCount == 3, "disable removes listeners");
                InvokeLifecycle(ui, "OnEnable");
                ui.Initialize();
                Check(!reward.activeSelf && !result.activeSelf && !message.activeSelf, "restart clears panels");

                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MvpHudBuilder.FontPath);
                Check(font != null && font.sourceFontFile != null, "portable Korean font source");
                // TryAddCharacters returns false when every glyph already exists.
                // HasCharacters is the repeatable coverage check, including dynamic additions.
                Check(font.HasCharacters("골드웨이브건설전투준비보상결과승리패배다시시작계속하기",
                    out uint[] missingGlyphs, false, true), "Korean glyphs");
                ui.SetGold(int.MaxValue);
                ui.SetWaveProgress(3, 3);
                ui.SetPhaseLabel("전투 준비");
                ui.SetWaveStartInteractable(true);
                Render(root, "hud-1920x1080", 1920, 1080);
                Render(root, "hud-1280x720", 1280, 720);
                Render(root, "hud-1024x768", 1024, 768);
                ui.ShowWaveReward(30, true);
                Render(root, "hud-reward", 1920, 1080);
                ui.ShowRunResult(false, 90);
                Render(root, "hud-defeat", 1920, 1080);
                Check(_engineErrors == 0, "no Unity errors, exceptions or assertions during validation");
                Debug.Log($"[UI/MvpHudValidation] PASS: {_checks} checks. Images: Logs/HudValidation.");
            }
            finally
            {
                Application.logMessageReceived -= HandleLogReceived;
                if (!Application.isBatchMode)
                {
                    EditorSceneManager.CloseScene(scene, true);
                    if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                }
            }
        }

        private static void HandleLogReceived(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _engineErrors++;
        }

        private static void InvokeLifecycle(GameUIController ui, string method)
        {
            // Directly exercise listener setup/cleanup in Edit Mode without sending native
            // lifecycle messages to a component that is intentionally not ExecuteAlways.
            typeof(GameUIController).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(ui, null);
        }

        private static T Get<T>(SerializedObject data, string name) where T : UnityEngine.Object
        {
            return (T)data.FindProperty(name).objectReferenceValue;
        }

        private static void Check(bool condition, string description)
        {
            _checks++;
            if (!condition) throw new InvalidOperationException("HUD check failed: " + description);
        }

        private static void ExpectRangeError(Action action)
        {
            try { action(); }
            catch (ArgumentOutOfRangeException) { Check(true, "invalid argument rejected"); return; }
            Check(false, "expected ArgumentOutOfRangeException");
        }

        private static void Render(GameObject source, string name, int width, int height)
        {
            // Use a fresh renderer per capture so same-frame modal deactivation cannot
            // leave stale native canvas batches in an offscreen snapshot.
            var root = UnityEngine.Object.Instantiate(source);
            var sourceWasActive = source.activeSelf;
            source.SetActive(false);
            var cameraObject = new GameObject("HUD Validation Camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            var texture = new RenderTexture(width, height, 24);
            var previousTarget = RenderTexture.active;
            Texture2D readback = null;
            var canvas = root.GetComponent<Canvas>();
            var scaler = root.GetComponent<CanvasScaler>();
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(15, 22, 31, 255);
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true;
                camera.targetTexture = texture;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                // Same geometric-mean scaling as matchWidthOrHeight = 0.5.
                scaler.enabled = false;
                canvas.scaleFactor = Mathf.Sqrt((width / 1920f) * (height / 1080f));
                Canvas.ForceUpdateCanvases();
                foreach (var text in root.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate();
                    Check(!text.isTextOverflowing, name + " text fits: " + text.name);
                }
                camera.Render();
                RenderTexture.active = texture;
                readback = new Texture2D(width, height, TextureFormat.RGB24, false);
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readback.Apply();
                var output = Path.GetFullPath("Logs/HudValidation");
                Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output, name + ".png"), readback.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousTarget;
                camera.targetTexture = null;
                canvas.worldCamera = null;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                scaler.enabled = true;
                if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(root);
                source.SetActive(sourceWasActive);
            }
        }
    }
}
