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
    /// <summary>실제 프리팹의 표시 계약, 리스너 수명 및 레이아웃을 검사한다.</summary>
    public static class MvpBuildingUiValidation
    {
        private static int _checks;
        private static int _errors;

        [MenuItem("Game/UI/Validate Building Info UI")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            _checks = 0;
            _errors = 0;
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            Application.logMessageReceived += HandleLogReceived;
            Texture2D iconTexture = null;
            Sprite iconSprite = null;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MvpBuildingUiBuilder.BuildingPrefabPath);
                Check(prefab != null, "building prefab exists");
                Check(AssetDatabase.LoadAssetAtPath<SceneAsset>(MvpBuildingUiBuilder.BuildingScenePath) != null,
                    "test scene exists");
                var root = UnityEngine.Object.Instantiate(prefab);
                var panel = root.GetComponent<BuildingInfoPanel>();
                InvokeLifecycle(panel, "OnEnable");
                var serialized = new SerializedObject(panel);
                var iterator = serialized.GetIterator();
                while (iterator.NextVisible(true))
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                        Check(iterator.objectReferenceValue != null, "assigned: " + iterator.name);
                var content = Get<GameObject>(serialized, "_contentPanel");
                var empty = Get<GameObject>(serialized, "_emptyState");
                var name = Get<TMP_Text>(serialized, "_nameText");
                var level = Get<TMP_Text>(serialized, "_levelText");
                var production = Get<TMP_Text>(serialized, "_productionText");
                var effect = Get<TMP_Text>(serialized, "_effectText");
                var description = Get<TMP_Text>(serialized, "_descriptionText");
                var close = Get<Button>(serialized, "_closeButton");
                var icon = Get<Image>(serialized, "_icon");
                var placeholder = Get<GameObject>(serialized, "_iconPlaceholder");
                var scroll = Get<ScrollRect>(serialized, "_detailsScroll");
                var closes = 0;
                string closedId = null;
                panel.InfoPanelClosed += id => { closes++; closedId = id; };

                panel.HideBuildingInfo();
                Check(!panel.HasSelection && !content.activeSelf && empty.activeSelf, "empty initial selection");
                close.onClick.Invoke();
                Check(closes == 0, "hidden close ignored");
                Expect<ArgumentException>(() => new BuildingInfoData(" ", "전사", "유닛", 1));
                Expect<ArgumentException>(() => new BuildingInfoData("slot", " ", "유닛", 1));
                Expect<ArgumentException>(() => new BuildingInfoData("slot", "전사", " ", 1));
                Expect<ArgumentOutOfRangeException>(() => new BuildingInfoData("slot", "전사", "유닛", 0));

                var warrior = new BuildingInfoData("slot-02", "전사 훈련소", "유닛 생산 건물", 1,
                    "공격과 방어가 균형 잡힌 전열 병종을 생산합니다.", "전사 · 전열 근거리 병종");
                panel.ShowBuildingInfo(warrior);
                Check(panel.SelectionId == "slot-02" && content.activeSelf && !empty.activeSelf, "selection shown");
                Check(name.text == "전사 훈련소" && level.text == "레벨 1", "name and level");
                Check(production.gameObject.activeSelf && production.text.Contains("전사"), "production details");
                Check(!effect.gameObject.activeSelf && !icon.gameObject.activeSelf && placeholder.activeSelf,
                    "optional effect and icon fallback");
                Expect<ArgumentNullException>(() => panel.ShowBuildingInfo(null));
                Check(panel.SelectionId == "slot-02" && name.text == "전사 훈련소", "null update is atomic");
                Render(root, "building-warrior-1920x1080", 1920, 1080);
                Render(root, "building-warrior-1280x720", 1280, 720);
                Render(root, "building-warrior-1024x768", 1024, 768);

                panel.ShowBuildingInfo(new BuildingInfoData("slot-02", "전사 훈련소", "유닛 생산 건물", 2,
                    productionSummary: "밸런스 전사(1차)"));
                Check(panel.SelectionId == "slot-02" && level.text == "레벨 2" &&
                    production.text.Contains("밸런스 전사"), "same instance receives upgraded snapshot");
                Check(description.text == "설명이 아직 없습니다.", "description fallback");
                panel.ShowBuildingInfo(new BuildingInfoData("slot-99", "전사 훈련소", "유닛 생산 건물", 1));
                Check(panel.SelectionId == "slot-99", "same type different instance identity");

                iconTexture = new Texture2D(2, 2);
                iconSprite = Sprite.Create(iconTexture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
                panel.ShowBuildingInfo(new BuildingInfoData("slot-01", "골드 생산소", "자원 건물", 1,
                    "현재 플레이에서 사용할 골드를 생산합니다.", effectSummary: "골드 생산", icon: iconSprite));
                Check(production.text == "" && !production.gameObject.activeSelf && effect.gameObject.activeSelf,
                    "switch clears stale production");
                Check(icon.sprite == iconSprite && icon.gameObject.activeSelf && !placeholder.activeSelf,
                    "provided icon displayed");
                panel.ShowBuildingInfo(new BuildingInfoData("slot-03", "궁사 훈련소", "유닛 생산 건물", 1,
                    "후열 원거리 병종입니다.", "궁사"));
                Check(effect.text == "" && !effect.gameObject.activeSelf && icon.sprite == null && placeholder.activeSelf,
                    "switch clears stale effect and icon");
                panel.ShowBuildingInfo(new BuildingInfoData("slot-04", "지원 건물", "지원 건물", 1,
                    effectSummary: "아군 강화"));
                Check(!production.gameObject.activeSelf && effect.text.Contains("아군 강화"), "support snapshot");

                for (var i = 0; i < 3; i++)
                {
                    InvokeLifecycle(panel, "OnDisable");
                    InvokeLifecycle(panel, "OnEnable");
                }
                close.onClick.Invoke();
                close.onClick.Invoke();
                Check(closes == 1 && closedId == "slot-04", "one close event with displayed instance ID");
                Check(!panel.HasSelection && empty.activeSelf && name.text == "" && icon.sprite == null,
                    "close clears stale selection");
                panel.ShowBuildingInfo(warrior);
                InvokeLifecycle(panel, "OnDisable");
                close.onClick.Invoke();
                Check(panel.HasSelection && closes == 1, "disabled listener removed");
                InvokeLifecycle(panel, "OnEnable");
                close.onClick.Invoke();
                Check(closes == 2, "listener restored once");
                panel.ShowBuildingInfo(warrior);
                panel.HideBuildingInfo();
                Check(closes == 2, "external hide does not echo a close event");
                root.SetActive(false);
                panel.ShowBuildingInfo(warrior);
                root.SetActive(true);
                Check(panel.SelectionId == warrior.SelectionId && name.text == warrior.DisplayName,
                    "snapshot while inactive retained");

                var longText = string.Concat(System.Linq.Enumerable.Repeat(
                    "긴 건물 설명입니다. 스크롤로 모든 내용을 확인할 수 있어야 합니다.\n", 24));
                panel.ShowBuildingInfo(new BuildingInfoData("long-slot", "긴 설명 확인용 건물", "지원 건물", 1, longText));
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                Check(scroll.content.rect.height > scroll.viewport.rect.height, "long description is scrollable");
                scroll.verticalNormalizedPosition = 0;
                panel.ShowBuildingInfo(new BuildingInfoData("long-slot", "긴 설명 확인용 건물", "지원 건물", 2, longText));
                Check(scroll.verticalNormalizedPosition < 0.01f, "same selection refresh preserves scroll");
                panel.ShowBuildingInfo(new BuildingInfoData("other-long-slot", "다른 건물", "지원 건물", 1, longText));
                Check(scroll.verticalNormalizedPosition > 0.99f, "new selection starts at top");
                Render(root, "building-long-description", 1280, 720);
                panel.HideBuildingInfo();
                Render(root, "building-empty", 1280, 720);
                Check(_errors == 0, "no engine error/exception/assert during checks");
                Debug.Log($"[UI/MvpBuildingUiValidation] PASS: {_checks} checks. Images: Logs/BuildingUiValidation.");
            }
            finally
            {
                Application.logMessageReceived -= HandleLogReceived;
                if (iconSprite != null) UnityEngine.Object.DestroyImmediate(iconSprite);
                if (iconTexture != null) UnityEngine.Object.DestroyImmediate(iconTexture);
                if (!Application.isBatchMode)
                {
                    EditorSceneManager.CloseScene(scene, true);
                    if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                }
            }
        }

        private static void InvokeLifecycle(BuildingInfoPanel panel, string method)
        {
            // Edit Mode does not run this non-ExecuteAlways component's runtime lifecycle.
            typeof(BuildingInfoPanel).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(panel, null);
        }

        private static T Get<T>(SerializedObject data, string name) where T : UnityEngine.Object
        {
            return (T)data.FindProperty(name).objectReferenceValue;
        }

        private static void Check(bool condition, string description)
        {
            _checks++;
            if (!condition) throw new InvalidOperationException("Building UI check failed: " + description);
        }

        private static void Expect<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { Check(true, typeof(T).Name); return; }
            Check(false, "expected " + typeof(T).Name);
        }

        private static void HandleLogReceived(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors++;
        }

        private static void Render(GameObject source, string name, int width, int height)
        {
            var root = UnityEngine.Object.Instantiate(source);
            source.SetActive(false);
            var cameraObject = new GameObject("Building UI Validation Camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            var target = new RenderTexture(width, height, 24);
            var previousTarget = RenderTexture.active;
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
                var scroll = root.GetComponentInChildren<ScrollRect>();
                if (scroll != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                    Canvas.ForceUpdateCanvases();
                }
                foreach (var text in root.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate();
                    Check(text.font.HasCharacters(text.text, out uint[] missing, false, true), name + " glyphs: " + text.name);
                    Check(!text.isTextOverflowing, name + " text fits: " + text.name);
                }
                var bounds = new Vector3[4];
                ((RectTransform)root.transform.Find("InfoCard")).GetWorldCorners(bounds);
                var min = camera.WorldToViewportPoint(bounds[0]);
                var max = camera.WorldToViewportPoint(bounds[2]);
                Check(min.x >= 0 && min.y >= 0 && max.x <= 1 && max.y <= 1, name + " card on-screen");
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("Logs/BuildingUiValidation");
                File.WriteAllBytes("Logs/BuildingUiValidation/" + name + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousTarget;
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
