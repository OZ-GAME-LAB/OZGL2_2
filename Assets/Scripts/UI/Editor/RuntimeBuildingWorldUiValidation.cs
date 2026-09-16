using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.UI.Samples;
using OZGL.KDH;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.Editor
{
    /// <summary>격리된 Editor에서 실제 2D/UI raycast와 건설 API를 함께 검증한다.</summary>
    [InitializeOnLoad]
    public static class RuntimeBuildingWorldUiValidation
    {
        private const string SessionKey = "Game.UI.BuildingWorldInput.Running";
        private static int _checks;
        private static int _errors;
        private static int _searchErrors;

        static RuntimeBuildingWorldUiValidation() => EditorApplication.playModeStateChanged += HandleState;

        public static void RunBatch()
        {
            if (!Application.isBatchMode || !Path.GetFullPath(Application.dataPath).Replace('\\', '/').Contains("/UnityUIValidation/"))
                throw new InvalidOperationException("Use an isolated UnityUIValidation batch project.");
            RuntimeBuildingWorldUiSetup.CreateScene();
            EditorSceneManager.OpenScene(RuntimeBuildingWorldUiSetup.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(SessionKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static void HandleState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(SessionKey, false)) RunAsync().Forget();
        }

        private static async UniTaskVoid RunAsync()
        {
            _checks = _errors = _searchErrors = 0;
            Application.logMessageReceived += HandleLog;
            float previousTimeScale = Time.timeScale;
            try
            {
                var sample = UnityEngine.Object.FindFirstObjectByType<MvpRuntimeHudSample>();
                var binding = UnityEngine.Object.FindFirstObjectByType<RuntimeBuildingUiBinding>();
                var flow = UnityEngine.Object.FindFirstObjectByType<GameFlowController>();
                var wallet = UnityEngine.Object.FindFirstObjectByType<RunCurrencyManager>();
                var catalog = UnityEngine.Object.FindFirstObjectByType<BuildingCatalogPanel>();
                var actions = UnityEngine.Object.FindFirstObjectByType<BuildingActionPanel>();
                var info = UnityEngine.Object.FindFirstObjectByType<BuildingInfoPanel>();
                var controller = UnityEngine.Object.FindFirstObjectByType<BuildingBuildController>();
                var slots = UnityEngine.Object.FindObjectsByType<BuildingSlot>(FindObjectsSortMode.None).OrderBy(s => s.name).ToArray();
                await Wait(() => sample.IsReady && flow.CanEnterBuildMode());
                await UniTask.NextFrame();
                Check(slots.Length == 4, "four real world slots");
                Check(Camera.main.GetComponent<Physics2DRaycaster>() != null && EventSystem.current != null, "real event system and world raycaster");
                Check(!UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Any(c => c.name.StartsWith("Battlefield Backdrop")), "old diagram input hidden");
                var build = Ref<Button>(actions, "_build._button");
                var dismantle = Ref<Button>(actions, "_dismantle._button");
                int initialGold = wallet.GetBalance(CurrencyType.Gold);
                var target = slots[0].GetComponent<RuntimeBuildingSelectionTarget>();
                Check(TargetAt(Point(slots[0])) == target.gameObject, "slot is frontmost hit");
                await Capture("01-world-slots");

                Click(Point(slots[0]), PointerEventData.InputButton.Right);
                Check(binding.SelectedSlot == null, "right click ignored");
                var held = Press(Point(slots[0]));
                held.dragging = true;
                Release(held);
                Check(binding.SelectedSlot == null, "dragging release ignored");
                held = Press(Point(slots[0]));
                ExecuteEvents.Execute(target.gameObject, held, ExecuteEvents.dragHandler);
                Release(held);
                Check(binding.SelectedSlot == null, "drag handler cancels press");
                held = Press(Point(slots[0]));
                held.position += Vector2.right * (EventSystem.current.pixelDragThreshold + 1);
                Release(held);
                Check(binding.SelectedSlot == null, "release frame movement ignored");
                held = Press(Point(slots[0]));
                held.pointerId = 42;
                Release(held);
                Check(binding.SelectedSlot == null, "different pointer cannot complete press");
                held = Press(Point(slots[0]));
                target.enabled = false; target.enabled = true;
                Release(held);
                Check(binding.SelectedSlot == null, "target disable cancels press");
                target.OnPointerDown(null); target.OnPointerClick(null);
                Check(binding.SelectedSlot == null, "null input safe");

                Click(Point(slots[0]));
                Check(binding.SelectedSlot == slots[0] && catalog.Popup.IsVisible, "world click opens catalog for exact slot");
                await UniTask.NextFrame();
                Check(TargetAt(Point(slots[1])) != slots[1].gameObject, "modal blocks world raycast");
                Click(Point(slots[1]));
                Check(binding.SelectedSlot != slots[1], "popup click does not select background slot");
                binding.ClearSelection();
                Click(Point(slots[0]));
                await Capture("02-world-catalog");
                var cards = new SerializedObject(catalog).FindProperty("_cards");
                var candidate = cards.GetArrayElementAtIndex(0).FindPropertyRelative("Button").objectReferenceValue as Button;
                Click(ButtonPoint(candidate));
                Check(info.HasSelection && build.interactable, "actual UI card raycast opens valid build offer");
                await UniTask.NextFrame();
                Click(ButtonPoint(build));
                Check(slots[0].IsOccupied && wallet.GetBalance(CurrencyType.Gold) == initialGold - 30,
                    "pointer build invokes real controller once");
                binding.ClearSelection();
                await UniTask.NextFrame();
                Check(slots[0].GetComponentInChildren<TextMeshPro>().text == "골드 생산소", "world label reflects actual building");
                Check(slots[0].transform.Find("UI Slot Marker").GetComponent<SpriteRenderer>().enabled, "occupied UI marker remains visible");
                await Capture("03-world-built");
                Click(Point(slots[0]));
                Check(info.HasSelection && dismantle.interactable && !catalog.Popup.IsVisible, "occupied world slot opens building info");
                await UniTask.NextFrame();
                Click(ButtonPoint(dismantle));
                await UniTask.NextFrame();
                Check(!slots[0].IsOccupied && wallet.GetBalance(CurrencyType.Gold) == initialGold, "pointer demolition refunds real wallet once");
                Check(slots[0].GetComponentInChildren<TextMeshPro>().text == "+ 건설", "destroyed building restores empty label");

                held = Press(Point(slots[0]));
                slots[0].GetComponent<Collider2D>().enabled = false;
                Release(held);
                Check(binding.SelectedSlot == null, "disabled collider rejects held input");
                slots[0].GetComponent<Collider2D>().enabled = true;
                held = Press(Point(slots[0]));
                slots[0].enabled = false;
                Release(held);
                Check(binding.SelectedSlot == null, "disabled slot rejects input");
                slots[0].enabled = true;
                held = Press(Point(slots[0]));
                binding.enabled = false;
                Release(held);
                Check(binding.SelectedSlot == null, "disabled UI rejects input");
                binding.enabled = true;

                var mine = AssetDatabase.LoadAssetAtPath<BuildingData>(RuntimeBuildingUiSetup.DataFolder + "/Resource.asset");
                held = Press(Point(slots[0]));
                Check(controller.TryBuild(slots[0], mine), "external build changes held slot");
                Release(held);
                Check(binding.SelectedSlot == null, "occupancy change rejects stale press");
                Check(controller.TryDemolish(slots[0]), "external build cleanup");
                await UniTask.NextFrame();

                held = Press(Point(slots[0]));
                var starting = flow.TryStartWave();
                Release(held);
                Check(binding.SelectedSlot == null, "phase transition cancels press");
                Check(await starting, "existing core starts test battle");
                Click(Point(slots[0]));
                Check(binding.SelectedSlot == null, "battle blocks world construction selection");
                await flow.ResolveBattleAsync(ResultType.Defeat);
                flow.ResetRun();
                await Wait(() => flow.CanEnterBuildMode());
                for (int i = 0; i < 3; i++)
                {
                    target.Initialize(binding, slots[0], flow);
                    target.enabled = false; target.enabled = true;
                    Click(Point(slots[0]));
                    Check(catalog.Popup.IsVisible && binding.SelectedSlot == slots[0], "reinitialize/re-enable click " + i);
                    binding.ClearSelection();
                }
                Time.timeScale = 0;
                Click(Point(slots[0]));
                Check(binding.SelectedSlot == slots[0], "UI click works with paused time");
                Time.timeScale = previousTimeScale;
                binding.ClearSelection();
                Check(_errors == 0, "no new runtime errors");
                File.WriteAllText(Output("results.txt"), $"PASS: {_checks} world input/build/visual assertions.\nRuntime errors: {_errors}. Editor Search startup exceptions: {_searchErrors}.\nPhysics2DRaycaster + GraphicRaycaster + ExecuteEvents, not OS mouse automation.\nFixture scene, not team combat map.\n");
                SessionState.SetBool(SessionKey, false);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                File.WriteAllText(Output("results.txt"), "FAIL\n" + exception);
                SessionState.SetBool(SessionKey, false);
                EditorApplication.Exit(1);
            }
            finally { Time.timeScale = previousTimeScale; Application.logMessageReceived -= HandleLog; }
        }

        private static RaycastResult Hit(Vector2 point)
        {
            Canvas.ForceUpdateCanvases(); Physics2D.SyncTransforms();
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            if (hits.Count == 0) throw new InvalidOperationException("No hit at " + point);
            return hits[0];
        }

        private static GameObject TargetAt(Vector2 point) => ExecuteEvents.GetEventHandler<IPointerClickHandler>(Hit(point).gameObject);
        private static Vector2 Point(Component component) => Camera.main.WorldToScreenPoint(component.transform.position);
        private static Vector2 ButtonPoint(Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        }

        private static PointerEventData Press(Vector2 point, PointerEventData.InputButton button = PointerEventData.InputButton.Left)
        {
            var hit = Hit(point);
            var data = new PointerEventData(EventSystem.current)
            {
                position = point, pressPosition = point, button = button, pointerId = -1,
                pointerCurrentRaycast = hit, pointerPressRaycast = hit, eligibleForClick = true
            };
            data.pointerPress = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
            ExecuteEvents.ExecuteHierarchy(hit.gameObject, data, ExecuteEvents.pointerDownHandler);
            return data;
        }

        private static void Release(PointerEventData data)
        {
            if (data.pointerPress == null) return;
            ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerClickHandler);
        }

        private static void Click(Vector2 point, PointerEventData.InputButton button = PointerEventData.InputButton.Left) => Release(Press(point, button));
        private static T Ref<T>(UnityEngine.Object target, string field) where T : UnityEngine.Object =>
            new SerializedObject(target).FindProperty(field).objectReferenceValue as T;
        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            _checks++; Debug.Log("[UI/WorldInput] PASS " + name);
        }

        private static async UniTask Wait(Func<bool> condition)
        {
            float start = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (Time.realtimeSinceStartup - start > 30) throw new TimeoutException("Scene/core initialization");
                await UniTask.NextFrame();
            }
        }

        private static string Output(string name)
        {
            string directory = Path.GetFullPath("Logs/BuildingWorldInput20260915");
            Directory.CreateDirectory(directory); return Path.Combine(directory, name);
        }

        private static void HandleLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception && stack.Contains("UnityEditor.Search.SearchDatabase") &&
                stack.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup")) { _searchErrors++; return; }
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors++;
        }

        private static async UniTask Capture(string name)
        {
            await UniTask.DelayFrame(2);
            var camera = Camera.main;
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas).ToArray();
            var modes = canvases.Select(c => c.renderMode).ToArray();
            var scales = canvases.Select(c => c.scaleFactor).ToArray();
            var scalers = canvases.Select(c => c.GetComponent<CanvasScaler>()).ToArray();
            var target = new RenderTexture(1280, 720, 24); target.Create();
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (scalers[i] != null) scalers[i].enabled = false;
                    canvases[i].renderMode = RenderMode.ScreenSpaceCamera; canvases[i].worldCamera = camera;
                    canvases[i].planeDistance = 1; canvases[i].scaleFactor = 2f / 3f;
                }
                Canvas.ForceUpdateCanvases();
                foreach (var label in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                {
                    if (string.IsNullOrEmpty(label.text) || label.GetComponentInParent<ScrollRect>() != null) continue;
                    label.ForceMeshUpdate(); Check(!label.isTextOverflowing, "text fits: " + label.name);
                }
                camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                File.WriteAllBytes(Output(name + ".png"), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = oldActive; camera.targetTexture = oldTarget;
                for (int i = 0; i < canvases.Length; i++)
                {
                    canvases[i].renderMode = modes[i]; canvases[i].worldCamera = null; canvases[i].scaleFactor = scales[i];
                    if (scalers[i] != null) scalers[i].enabled = true;
                }
                UnityEngine.Object.Destroy(image); target.Release(); UnityEngine.Object.Destroy(target);
            }
            // 복원한 Overlay Canvas의 렌더 깊이가 갱신된 뒤 다음 포인터 입력을 보낸다.
            await UniTask.NextFrame();
        }
    }
}
