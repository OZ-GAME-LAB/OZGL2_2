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
    /// <summary>실제 행동 프리팹을 대상으로 하는 Editor 계약 검사. Unity Test Runner 테스트와 구분한다.</summary>
    public static class MvpBuildingActionValidation
    {
        private static int _checks;

        [MenuItem("Game/UI/Validate Building Actions UI")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            _checks = 0;
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MvpBuildingActionBuilder.ActionPrefabPath);
                Check(prefab != null, "prefab exists");
                Check(AssetDatabase.LoadAssetAtPath<SceneAsset>(MvpBuildingActionBuilder.ActionScenePath) != null, "scene exists");
                var root = UnityEngine.Object.Instantiate(prefab);
                var panel = root.GetComponent<BuildingActionPanel>();
                Lifecycle(panel, "OnEnable");
                var serialized = new SerializedObject(panel);
                var iterator = serialized.GetIterator();
                while (iterator.NextVisible(true))
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                        Check(iterator.objectReferenceValue != null, "reference " + iterator.propertyPath);
                var build = Get<Button>(serialized, "_build._button");
                var upgrade = Get<Button>(serialized, "_upgrade._button");
                var dismantle = Get<Button>(serialized, "_dismantle._button");
                var status = Get<TMP_Text>(serialized, "_status");
                var quote = Get<TMP_Text>(serialized, "_build._quote");
                var reason = Get<TMP_Text>(serialized, "_build._reason");
                var empty = Empty("a");
                var occupied = Occupied("b");
                var count = 0;
                BuildingActionRequest request = null;
                Action<BuildingActionRequest> receiver = value => { count++; request = value; };

                panel.ShowActions(empty);
                panel.SetActionsAllowed(true);
                Check(!build.interactable, "no receiver disabled");
                build.onClick.Invoke();
                Check(!panel.IsRequestPending, "no receiver creates no pending request");
                panel.ActionRequested += receiver;
                Check(build.interactable && !upgrade.interactable && !dismantle.interactable, "build offer only");
                Check(quote.text.Contains("30 골드"), "authoritative quote displayed");
                Expect<ArgumentNullException>(() => panel.ShowActions(null));
                Check(panel.TargetId == "a", "null update atomic");
                Expect<ArgumentOutOfRangeException>(() => new BuildingActionOffer(BuildingUiAction.Upgrade, "x", -1, true));
                Expect<ArgumentException>(() => new BuildingActionOffer(BuildingUiAction.Build, "x", 1, true));
                Expect<ArgumentException>(() => new BuildingActionOffer(BuildingUiAction.Upgrade, "x", 1, false));
                Expect<ArgumentException>(() => new BuildingActionViewData("x", "x", build: occupied.Upgrade));

                panel.SetActionsAllowed(false);
                build.onClick.Invoke();
                Check(count == 0 && !build.interactable && status.text.Contains("전투"), "phase locked");
                panel.SetActionsAllowed(true);
                Render(root, "actions-build-1920x1080", 1920, 1080);
                Render(root, "actions-build-1280x720", 1280, 720);
                Render(root, "actions-build-1024x768", 1024, 768);
                build.onClick.Invoke();
                build.onClick.Invoke();
                Check(count == 1 && panel.IsRequestPending && !build.interactable, "double click blocked");
                Check(request.TargetId == "a" && request.OptionId == "warrior" && request.Action == BuildingUiAction.Build,
                    "captured target and option");
                var firstId = request.RequestId;
                panel.ShowActions(empty);
                panel.SetActionsAllowed(true);
                build.onClick.Invoke();
                Check(count == 1 && !build.interactable, "refresh does not unlock pending");
                Check(!panel.TryResolveRequest(Guid.NewGuid(), false) && panel.IsRequestPending, "unknown response ignored");
                Check(panel.TryResolveRequest(firstId, false, "조건 변경"), "failure accepted");
                Check(build.interactable && status.text == "조건 변경" && panel.TargetId == "a", "failure keeps selection and permits retry");
                Check(!panel.TryResolveRequest(firstId, true), "duplicate response ignored");
                build.onClick.Invoke();
                Check(count == 2 && request.RequestId != firstId, "retry gets unique ID");
                panel.TryResolveRequest(request.RequestId, true);
                Check(panel.IsAwaitingRefresh && !build.interactable, "success waits for authoritative refresh");
                panel.ShowActions(Occupied("a"));
                Check(!panel.IsAwaitingRefresh && upgrade.interactable && dismantle.interactable, "updated occupied offers");
                Check(Get<TMP_Text>(serialized, "_dismantle._quote").text.Contains("환급 21 골드"), "refund displayed without calculation");
                upgrade.onClick.Invoke();
                Check(request.Action == BuildingUiAction.Upgrade && request.TargetId == "a", "upgrade request");
                panel.ShowActions(occupied);
                Check(!upgrade.interactable, "other selection globally locked while pending");
                panel.TryResolveRequest(request.RequestId, false, "OLD RESPONSE");
                Check(panel.TargetId == "b" && !status.text.Contains("OLD RESPONSE") && upgrade.interactable, "old selection response does not replace current view");

                dismantle.onClick.Invoke();
                Check(request.Action == BuildingUiAction.Dismantle && request.TargetId == "b", "dismantle identity");
                panel.HideActions();
                panel.ShowActions(occupied);
                Check(panel.IsRequestPending && !upgrade.interactable, "clear and reselect cannot duplicate operation");
                panel.TryResolveRequest(request.RequestId, false, "OLD AFTER RESELECT");
                Check(!status.text.Contains("OLD AFTER RESELECT"), "selection generation guards ABA");
                for (var i = 0; i < 3; i++) { Lifecycle(panel, "OnDisable"); Lifecycle(panel, "OnEnable"); }
                var before = count;
                upgrade.onClick.Invoke();
                Check(count == before + 1, "lifecycle listener not duplicated");
                root.SetActive(false);
                Lifecycle(panel, "OnDisable");
                upgrade.onClick.Invoke();
                Check(count == before + 1 && panel.IsRequestPending, "disable keeps pending but removes input");
                panel.TryResolveRequest(request.RequestId, false);
                root.SetActive(true);
                Lifecycle(panel, "OnEnable");
                Check(upgrade.interactable, "response while inactive retained");

                panel.ShowActions(new BuildingActionViewData("c", "골드 부족 테스트",
                    new BuildingActionOffer(BuildingUiAction.Build, "전사 훈련소", 30, false, "골드가 부족합니다.", "warrior")));
                before = count;
                build.onClick.Invoke();
                Check(count == before && !build.interactable && reason.text == "골드가 부족합니다.", "disabled offer reason");
                Render(root, "actions-disabled", 1280, 720);
                panel.ShowActions(new BuildingActionViewData("free", "무료 건설 테스트",
                    new BuildingActionOffer(BuildingUiAction.Build, "전사 훈련소", 0, true, optionId: "warrior")));
                Check(build.interactable && quote.text.Contains("0 골드"), "zero cost allowed");
                panel.ActionRequested -= receiver;
                Check(!build.interactable, "receiver removal reflected immediately");
                Action<BuildingActionRequest> synchronous = value => panel.TryResolveRequest(value.RequestId, false, "즉시 응답");
                panel.ActionRequested += synchronous;
                build.onClick.Invoke();
                Check(!panel.IsRequestPending && build.interactable && status.text == "즉시 응답", "synchronous response safe");
                panel.ActionRequested -= synchronous;
                panel.HideActions();
                Check(panel.TargetId == null && !build.interactable && !upgrade.interactable, "hidden view clears offers");
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
            MvpBuildingUiValidation.Run();
            MvpHudValidation.Run();
            Debug.Log($"[UI/MvpBuildingActionValidation] PASS: {_checks} action checks plus building-info and HUD regression checks. Images: Logs/BuildingActionValidation.");
        }

        private static BuildingActionViewData Empty(string id) => new BuildingActionViewData(id, "빈 건설 공간",
            new BuildingActionOffer(BuildingUiAction.Build, "전사 훈련소", 30, true, optionId: "warrior"));
        private static BuildingActionViewData Occupied(string id) => new BuildingActionViewData(id, "전사 훈련소 · 레벨 1",
            upgrade: new BuildingActionOffer(BuildingUiAction.Upgrade, "생산 건물 강화", 50, true),
            dismantle: new BuildingActionOffer(BuildingUiAction.Dismantle, "선택한 건물", 21, true));
        private static T Get<T>(SerializedObject data, string path) where T : UnityEngine.Object =>
            (T)data.FindProperty(path).objectReferenceValue;
        private static void Lifecycle(BuildingActionPanel panel, string name) =>
            typeof(BuildingActionPanel).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);
        private static void Check(bool value, string name)
        {
            _checks++;
            if (!value) throw new InvalidOperationException("Action UI check failed: " + name);
        }
        private static void Expect<T>(Action action) where T : Exception
        {
            try { action(); } catch (T) { Check(true, typeof(T).Name); return; }
            Check(false, "expected " + typeof(T).Name);
        }

        private static void Render(GameObject source, string name, int width, int height)
        {
            var root = UnityEngine.Object.Instantiate(source);
            source.SetActive(false);
            var cameraObject = new GameObject("Action Validation Camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            var target = new RenderTexture(width, height, 24);
            var previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(15, 22, 31, 255);
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true;
                camera.targetTexture = target;
                var canvas = root.GetComponent<Canvas>();
                root.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                canvas.scaleFactor = Mathf.Sqrt((width / 1920f) * (height / 1080f));
                Canvas.ForceUpdateCanvases();
                foreach (var label in root.GetComponentsInChildren<TMP_Text>())
                {
                    label.ForceMeshUpdate();
                    Check(label.font.HasCharacters(label.text, out uint[] missing, false, true), name + " glyphs " + label.name);
                    Check(!label.isTextOverflowing, name + " text fits " + label.name);
                }
                var corners = new Vector3[4];
                ((RectTransform)root.transform.Find("ActionCard")).GetWorldCorners(corners);
                var min = camera.WorldToViewportPoint(corners[0]);
                var max = camera.WorldToViewportPoint(corners[2]);
                Check(min.x >= 0 && min.y >= 0 && max.x <= 1 && max.y <= 1, name + " on screen");
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("Logs/BuildingActionValidation");
                File.WriteAllBytes("Logs/BuildingActionValidation/" + name + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(target);
                source.SetActive(true);
            }
        }
    }
}
