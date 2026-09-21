using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.UI.Samples;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.Editor
{
    /// <summary>명시적으로 실행한 격리된 Editor에서만 프리뷰를 만들고 Play Mode 검사/촬영한다.</summary>
    [InitializeOnLoad]
    public static partial class PlayerUiValidation
    {
        private const string SessionKey = "Game.UI.PlayerValidation.Running";
        private static int _checks;
        private static int _errors;
        private static int _editorSearchErrors;
        static PlayerUiValidation() { EditorApplication.playModeStateChanged += HandleState; }

        public static void RunBatch()
        {
            if (!Application.isBatchMode || !Path.GetFullPath(Application.dataPath).Replace('\\', '/').Contains("/UnityUIValidation/"))
                throw new InvalidOperationException("Validation requires an isolated UnityUIValidation batch project.");
            // Only the generated scene in this isolated validation copy is disposable.
            if (File.Exists(PlayerUiBuilder.ScenePath)) AssetDatabase.DeleteAsset(PlayerUiBuilder.ScenePath);
            PlayerUiBuilder.Build();
            EditorSceneManager.OpenScene(PlayerUiBuilder.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(SessionKey, true);
            EditorApplication.EnterPlaymode();
        }

        public static void RunWireframeBatch()
        {
            PlayerUiBuilder.ApplyWireframeBatch();
            // Open the migrated scene without regenerating it or replacing its GUID.
            EditorSceneManager.OpenScene(PlayerUiBuilder.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(SessionKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static void HandleState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(SessionKey, false)) RunAsync().Forget();
        }

        private static async UniTaskVoid RunAsync()
        {
            _checks = _errors = _editorSearchErrors = 0; Application.logMessageReceived += HandleLog;
            try
            {
                var preview = UnityEngine.Object.FindFirstObjectByType<PlayerUiPreviewBindings>();
                var sample = UnityEngine.Object.FindFirstObjectByType<MvpRuntimeHudSample>();
                await Wait(() => preview != null && preview.IsReady && sample != null && sample.IsReady, "initialization");
                await UniTask.DelayFrame(3);
                var hud = UnityEngine.Object.FindFirstObjectByType<GameUIController>();
                var building = UnityEngine.Object.FindFirstObjectByType<BuildingInfoPanel>();
                var actions = UnityEngine.Object.FindFirstObjectByType<BuildingActionPanel>();
                var unit = UnityEngine.Object.FindFirstObjectByType<UnitInfoPanel>();
                var catalog = UnityEngine.Object.FindFirstObjectByType<BuildingCatalogPanel>();
                var popups = UnityEngine.Object.FindObjectsByType<PlayerPopup>(FindObjectsSortMode.None);
                var flow = UnityEngine.Object.FindFirstObjectByType<GameFlowController>();
                var wallet = UnityEngine.Object.FindFirstObjectByType<RunCurrencyManager>();
                var reward = UnityEngine.Object.FindFirstObjectByType<ArtifactRewardPanel>();
                Check(popups.All(p => !p.IsVisible) && !reward.IsVisible, "only the minimal HUD is initially visible");
                Check(!sample.transform.Find("DeveloperOnly").gameObject.activeInHierarchy, "developer controls are invisible");
                await Capture("01-hud", 1920, 1080);
                var launch = GameObject.Find("Player HUD/BottomBar/Build").GetComponent<Button>();
                for (int i = 0; i < 3; i++)
                {
                    launch.onClick.Invoke(); Check(catalog.Popup.IsVisible && catalog.ItemCount == 4, "click opens compact catalog");
                    catalog.Popup.Hide(); Check(!catalog.Popup.IsVisible, "repeat close is safe");
                }
                EventSystem.current.SetSelectedGameObject(launch.gameObject); launch.onClick.Invoke();
                await Capture("02-catalog", 1280, 720);
                var card = (Button)new SerializedObject(catalog).FindProperty("_cards").GetArrayElementAtIndex(0).FindPropertyRelative("Button").objectReferenceValue;
                card.onClick.Invoke();
                var buildingPopup = PlayerUiBuilder.Ref<PlayerPopup>(building, "_playerPopup");
                Check(building.HasSelection && buildingPopup.IsVisible && !catalog.Popup.IsVisible && popups.Count(p => p.IsVisible) == 1,
                    "catalog selection opens exactly one building popup");
                Check(!new SerializedObject(actions).FindProperty("_build").FindPropertyRelative("_button").objectReferenceValue.As<Button>().interactable,
                    "preview does not pretend a missing building backend can execute");
                await Capture("03-building", 1280, 720);
                buildingPopup.SetExpanded(true); await Capture("04-building-details", 1280, 720);
                buildingPopup.Hide(); Check(!building.HasSelection && actions.TargetId == null, "dismiss clears both building views");
                PlayerUiBuilder.Ref<Button>(preview, "_unitButton").onClick.Invoke();
                var unitPopup = PlayerUiBuilder.Ref<PlayerPopup>(unit, "_playerPopup");
                Check(unit.HasSelection && unitPopup.IsVisible, "unit click displays real runtime HP");
                await Capture("05-unit", 1280, 720);
                unitPopup.SetExpanded(true); await Capture("06-unit-details", 1280, 720);
                var navigation = UnityEngine.Object.FindFirstObjectByType<PlayerUiNavigation>();
                // Unfocused batch Editors dispatch input to Editor updates. Test the same close action called by Escape;
                // physical keyboard/Game View focus must be checked manually, without taking control of the user's Editor.
                Check(navigation.TryCloseActivePopup() && !unitPopup.IsVisible && !unit.HasSelection,
                    "shared Escape close action dismisses and clears the selected unit");
                Check(!navigation.TryCloseActivePopup(), "closing an already closed popup is a no-op");
                Check(wallet.GetBalance(CurrencyType.Gold) == 100, "opening informational views never spends gold");

                await MvpVictoryRewardValidation.RunChecksAsync();
                Check(true, "existing authoritative victory/economy regression suite passes on the compact scene");
                PlayerUiBuilder.Ref<Button>(sample, "_resetButton").onClick.Invoke();
                flow.StagingTime = .02f; flow.AutoContinue = false;
                launch.onClick.Invoke(); PlayerUiBuilder.Ref<Button>(hud, "_waveStartButton").onClick.Invoke();
                await Wait(() => flow.CurPhase == GamePhase.Battle, "battle");
                Check(popups.All(p => !p.IsVisible) && !launch.interactable, "battle closes catalog and locks building controls");
                PlayerUiBuilder.Ref<Button>(sample, "_winButton").onClick.Invoke();
                await Wait(() => reward.IsVisible, "victory reward");
                catalog.Show(); Check(!catalog.Popup.IsVisible, "required reward blocks optional catalog");
                var owned = UnityEngine.Object.FindFirstObjectByType<ArtifactInventoryPanel>();
                if (owned != null)
                {
                    owned.Show(); Check(!owned.Popup.IsVisible, "required reward blocks optional owned inventory");
                }
                var rewardCard = new SerializedObject(reward).FindProperty("_cards").GetArrayElementAtIndex(0).FindPropertyRelative("Button").objectReferenceValue as Button;
                rewardCard.onClick.Invoke(); await Capture("07-victory-reward", 1920, 1080); await Capture("08-victory-reward-small", 1280, 720);
                Check(reward.SelectedArtifactId != null && PlayerUiBuilder.Ref<TMP_Text>(reward, "_confirmText").text == "획득하기", "selected reward uses concise player copy");
                PlayerUiBuilder.Ref<Button>(reward, "_confirmButton").onClick.Invoke();
                await MvpRuntimeHudValidation.WaitForPhaseAfterContentAsync(flow, GamePhase.Preparation);
                if (UnityEngine.Object.FindFirstObjectByType<ArtifactInventoryPanel>() != null)
                    await RunWireframeChecks(hud, wallet, reward, navigation);
                hud.ShowRunResult(true, 100); await Capture("09-result-presentation", 1280, 720); hud.HideRunResult();
                Check(_errors == 0, "no UI/gameplay error or exception logs during player UI checks");
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PlayerUiBuilder.FontPath);
                foreach (var atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
                EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font);
                File.WriteAllText(Output("results.txt"), "PASS: " + _checks + " player checks; authoritative victory regression also passed.\n" +
                    "Screenshots: real Unity render, 1920x1080 and 1280x720. Result screen was explicitly presented, not a Core completion integration.\n" +
                    "Escape close action passed; physical keyboard/Game View focus was not tested in unfocused batch mode.\n" +
                    "Unity Editor Search startup exceptions (separately classified): " + _editorSearchErrors + ". See full batch log.\n");
                Debug.Log("[UI/PlayerUiValidation] PASS " + _checks);
                SessionState.SetBool(SessionKey, false); EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error); File.WriteAllText(Output("results.txt"), "FAIL\n" + error);
                SessionState.SetBool(SessionKey, false); EditorApplication.Exit(1);
            }
            finally { Application.logMessageReceived -= HandleLog; }
        }

        private static void HandleLog(string condition, string stack, LogType type)
        {
            if (type == LogType.Exception && condition.StartsWith("ArgumentOutOfRangeException") &&
                stack.Contains("UnityEditor.Search.SearchDatabase") && stack.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"))
            {
                _editorSearchErrors++;
                return; // Editor search-index startup failure, not game/UI code. Keep and report it separately.
            }
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors++;
        }

        private static async UniTask Wait(Func<bool> predicate, string stage)
        {
            float start = Time.realtimeSinceStartup;
            while (!predicate())
            {
                if (Time.realtimeSinceStartup - start > 30) throw new TimeoutException(stage);
                await UniTask.NextFrame();
            }
        }

        private static void Check(bool ok, string description)
        {
            if (!ok) throw new InvalidOperationException(description);
            _checks++; Debug.Log("[UI/PlayerUiValidation] PASS " + description);
        }

        private static string Output(string filename)
        {
            var path = Path.GetFullPath("Logs/PlayerUI"); Directory.CreateDirectory(path); return Path.Combine(path, filename);
        }

        private static async UniTask Capture(string name, int width, int height)
        {
            await UniTask.DelayFrame(2);
            var camera = Camera.main;
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas).ToArray();
            var modes = canvases.Select(c => c.renderMode).ToArray(); var scales = canvases.Select(c => c.scaleFactor).ToArray();
            var scalers = canvases.Select(c => c.GetComponent<CanvasScaler>()).ToArray();
            var target = new RenderTexture(width, height, 24); target.Create();
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (scalers[i] != null) scalers[i].enabled = false;
                    canvases[i].renderMode = RenderMode.ScreenSpaceCamera; canvases[i].worldCamera = camera; canvases[i].planeDistance = 1;
                    canvases[i].scaleFactor = Mathf.Sqrt(width / 1920f * height / 1080f);
                }
                Canvas.ForceUpdateCanvases();
                var overflowing = new System.Collections.Generic.List<string>();
                foreach (var label in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                {
                    label.ForceMeshUpdate();
                    if (string.IsNullOrEmpty(label.text)) continue;
                    Check(label.font.HasCharacters(label.text, out uint[] missing, false, true), "font: " + label.name);
                    var preferred = label.GetPreferredValues(label.text, label.rectTransform.rect.width, Mathf.Infinity);
                    bool tooWide = label.textWrappingMode == TextWrappingModes.NoWrap && label.GetPreferredValues(label.text).x > label.rectTransform.rect.width + 1;
                    if (label.GetComponentInParent<ScrollRect>() == null &&
                        (label.isTextOverflowing || tooWide || preferred.y > label.rectTransform.rect.height + 1))
                        overflowing.Add(label.name + ": " + label.text);
                }
                camera.Render(); RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                File.WriteAllBytes(Output(name + ".png"), texture.EncodeToPNG());
                Check(overflowing.Count == 0, "text fits in " + name + ": " + string.Join(" / ", overflowing));
            }
            finally
            {
                RenderTexture.active = oldActive; camera.targetTexture = oldTarget;
                for (int i = 0; i < canvases.Length; i++)
                {
                    canvases[i].renderMode = modes[i]; canvases[i].worldCamera = null; canvases[i].scaleFactor = scales[i];
                    if (scalers[i] != null) scalers[i].enabled = true;
                }
                UnityEngine.Object.Destroy(texture); target.Release(); UnityEngine.Object.Destroy(target);
            }
        }

        private static T As<T>(this UnityEngine.Object value) where T : UnityEngine.Object => value as T;
    }
}
