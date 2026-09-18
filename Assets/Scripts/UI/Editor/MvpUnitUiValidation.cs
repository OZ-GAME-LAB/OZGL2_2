using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI.Editor
{
    /// <summary>실제 유닛 정보 프리팹의 자체 Editor 검사. Test Runner 테스트와 구분한다.</summary>
    public static class MvpUnitUiValidation
    {
        private static int _checks;
        private static int _engineErrors;

        [MenuItem("Game/UI/Validate Unit Info UI")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            _checks = _engineErrors = 0;
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            Texture2D iconTexture = null;
            Sprite icon = null;
            Application.logMessageReceived += HandleLog;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MvpUnitUiBuilder.UnitPrefabPath);
                Check(prefab != null, "prefab exists");
                Check(AssetDatabase.LoadAssetAtPath<SceneAsset>(MvpUnitUiBuilder.UnitScenePath) != null, "test scene exists");
                var root = UnityEngine.Object.Instantiate(prefab);
                var panel = root.GetComponent<UnitInfoPanel>();
                Lifecycle(panel, "OnEnable");
                var serialized = new SerializedObject(panel);
                var iterator = serialized.GetIterator();
                while (iterator.NextVisible(true))
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                        Check(iterator.objectReferenceValue != null, "reference " + iterator.propertyPath);
                var name = Get<TMP_Text>(serialized, "_nameText");
                var health = Get<TMP_Text>(serialized, "_healthText");
                var state = Get<TMP_Text>(serialized, "_healthStateText");
                var fill = Get<RectTransform>(serialized, "_healthFill");
                var content = Get<GameObject>(serialized, "_contentPanel");
                var empty = Get<GameObject>(serialized, "_emptyState");
                var combat = Get<TMP_Text>(serialized, "_combatText");
                var traits = Get<TMP_Text>(serialized, "_traitText");
                var image = Get<Image>(serialized, "_icon");
                var placeholder = Get<GameObject>(serialized, "_iconPlaceholder");
                var close = Get<Button>(serialized, "_closeButton");
                var scroll = Get<ScrollRect>(serialized, "_detailsScroll");
                var closes = 0;
                string closedId = null;
                panel.InfoPanelClosed += id => { closes++; closedId = id; };
                panel.HideUnitInfo();
                Check(!panel.HasSelection && empty.activeSelf && !content.activeSelf, "initial empty state");
                Check(!panel.TryUpdateHealth(null, 50, 100) && !panel.TryHideUnitInfo(null), "no selection rejects callbacks");
                close.onClick.Invoke();
                Check(closes == 0, "empty close ignored");
                Expect<ArgumentException>(() => new UnitInfoData(" ", "전사", "아군", "전열", 1, 1));
                Expect<ArgumentException>(() => new UnitInfoData("a", " ", "아군", "전열", 1, 1));
                Expect<ArgumentException>(() => new UnitInfoData("a", "전사", " ", "전열", 1, 1));
                Expect<ArgumentException>(() => new UnitInfoData("a", "전사", "아군", " ", 1, 1));
                foreach (var invalid in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                    Expect<ArgumentOutOfRangeException>(() => Data("a", invalid, 100));
                foreach (var invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                    Expect<ArgumentOutOfRangeException>(() => Data("a", 1, invalid));

                var warrior = Data("warrior-a", 100, 100);
                panel.ShowUnitInfo(warrior);
                Check(panel.SelectionId == "warrior-a" && name.text == "전사", "snapshot identity and title");
                Check(health.text == "체력 100 / 100" && fill.anchorMax.x == 1, "full health display");
                Check(combat.gameObject.activeSelf && traits.gameObject.activeSelf, "combat and traits shown");
                Check(!image.gameObject.activeSelf && placeholder.activeSelf, "missing icon fallback");
                Expect<ArgumentNullException>(() => panel.ShowUnitInfo(null));
                Check(panel.SelectionId == "warrior-a" && health.text == "체력 100 / 100", "null update atomic");
                Check(!panel.TryUpdateHealth("old-warrior", 0, 100) && !panel.TryHideUnitInfo("old-warrior"), "stale identity ignored");
                Check(health.text == "체력 100 / 100" && panel.HasSelection, "stale callback leaves view unchanged");
                Check(!panel.TryUpdateHealth("warrior-a", -1, 100) && !panel.TryUpdateHealth("warrior-a", 1, 0), "invalid health ignored");
                Check(!panel.TryUpdateHealth("warrior-a", float.NaN, 100) && !panel.TryUpdateHealth("warrior-a", 1, float.PositiveInfinity), "nonfinite health ignored");
                Check(panel.TryUpdateHealth("warrior-a", 75, 100) && health.text == "체력 75 / 100" && fill.anchorMax.x == 0.75f, "health delta applied");
                Check(panel.TryUpdateHealth("warrior-a", 75, 100), "same health idempotent");
                Render(root, "unit-warrior-1920x1080", 1920, 1080);
                Render(root, "unit-warrior-1280x720", 1280, 720);
                Render(root, "unit-warrior-1024x768", 1024, 768);
                panel.TryUpdateHealth("warrior-a", 0, 100);
                Check(fill.anchorMax.x == 0 && state.text == "체력 소진" && panel.HasSelection, "zero health does not infer despawn");
                panel.TryUpdateHealth("warrior-a", 150, 100);
                Check(fill.anchorMax.x == 1 && health.text == "체력 150 / 100" && state.text == "", "over-max value visible; bar only clamped");
                panel.TryUpdateHealth("warrior-a", 50, 200);
                Check(fill.anchorMax.x == 0.25f && health.text == "체력 50 / 200", "max health change");

                iconTexture = new Texture2D(2, 2);
                icon = Sprite.Create(iconTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
                panel.ShowUnitInfo(new UnitInfoData("enemy-a", "경비병", "적군 · 비정규군", "탱커", 150, 150, icon: icon));
                Check(Get<TMP_Text>(serialized, "_factionText").text.Contains("적군"), "enemy label displayed");
                Check(image.sprite == icon && image.gameObject.activeSelf && !placeholder.activeSelf, "icon shown");
                Check(!combat.gameObject.activeSelf && !traits.gameObject.activeSelf && combat.text == "" && traits.text == "", "missing detail clears previous values");
                Check(Get<TMP_Text>(serialized, "_descriptionText").text == "설명이 아직 없습니다.", "description fallback");
                Check(!panel.TryUpdateHealth("warrior-a", 0, 100), "prior unit cannot update new unit");
                panel.ShowUnitInfo(Data("warrior-b", 100, 100));
                Check(panel.SelectionId == "warrior-b" && image.sprite == null && placeholder.activeSelf, "same type different spawn");

                for (var i = 0; i < 3; i++) { Lifecycle(panel, "OnDisable"); Lifecycle(panel, "OnEnable"); }
                close.onClick.Invoke();
                close.onClick.Invoke();
                Check(closes == 1 && closedId == "warrior-b" && !panel.HasSelection, "one close with instance ID");
                panel.ShowUnitInfo(warrior);
                Check(panel.TryHideUnitInfo("warrior-a") && closes == 1, "external removal does not echo user close");
                Check(health.text == "" && fill.anchorMax.x == 0 && name.text == "", "removal clears stale health and title");
                Check(!panel.TryHideUnitInfo("warrior-a"), "duplicate removal ignored");
                panel.ShowUnitInfo(warrior);
                Lifecycle(panel, "OnDisable");
                close.onClick.Invoke();
                Check(panel.HasSelection && closes == 1, "disabled listener removed");
                root.SetActive(false);
                Check(panel.TryUpdateHealth("warrior-a", 25, 100), "inactive update accepted");
                root.SetActive(true);
                Lifecycle(panel, "OnEnable");
                Check(health.text == "체력 25 / 100", "inactive state retained");

                var longText = string.Concat(Enumerable.Repeat("긴 설명입니다. 체력 변화에도 읽던 스크롤 위치를 유지합니다.\n", 24));
                panel.ShowUnitInfo(new UnitInfoData("long-a", "긴 설명 확인용 유닛", "아군", "지원", 80, 100, longText));
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                Check(scroll.content.rect.height > scroll.viewport.rect.height, "long content scrollable");
                scroll.verticalNormalizedPosition = 0.4f;
                panel.TryUpdateHealth("long-a", 55, 100);
                Check(Mathf.Abs(scroll.verticalNormalizedPosition - 0.4f) < 0.001f, "health update preserves scroll");
                panel.ShowUnitInfo(new UnitInfoData("long-a", "긴 설명 확인용 유닛", "아군", "지원", 55, 100, longText));
                Check(Mathf.Abs(scroll.verticalNormalizedPosition - 0.4f) < 0.001f, "same unit refresh preserves scroll");
                panel.ShowUnitInfo(new UnitInfoData("long-b", "다른 긴 설명 유닛", "아군", "지원", 100, 100, longText));
                Check(scroll.verticalNormalizedPosition > 0.99f, "new unit scroll reset");
                Render(root, "unit-long-description", 1280, 720);
                panel.HideUnitInfo();
                Render(root, "unit-empty", 1280, 720);
                Check(_engineErrors == 0, "no engine errors during checks");
            }
            finally
            {
                Application.logMessageReceived -= HandleLog;
                if (icon != null) UnityEngine.Object.DestroyImmediate(icon);
                if (iconTexture != null) UnityEngine.Object.DestroyImmediate(iconTexture);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
            MvpBuildingActionValidation.Run();
            Debug.Log($"[UI/MvpUnitUiValidation] PASS: {_checks} unit checks plus action/info/HUD regression checks. Images: Logs/UnitUiValidation.");
        }

        private static UnitInfoData Data(string id, float current, float maximum) => new UnitInfoData(id, "전사", "아군 · 마계", "전열 · 균형형 근거리", current, maximum,
            "공격과 방어가 균형 잡힌 전열 병종입니다.", "공격력 12 · 방어력 5 (임시)", "건물 업그레이드로 전직 유닛 생산");
        private static T Get<T>(SerializedObject data, string path) where T : UnityEngine.Object => (T)data.FindProperty(path).objectReferenceValue;
        private static void Lifecycle(UnitInfoPanel panel, string name) => typeof(UnitInfoPanel).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);
        private static void Check(bool value, string name)
        {
            _checks++;
            if (!value) throw new InvalidOperationException("Unit UI check failed: " + name);
        }
        private static void Expect<T>(Action action) where T : Exception
        {
            try { action(); } catch (T) { Check(true, typeof(T).Name); return; }
            Check(false, "expected " + typeof(T).Name);
        }
        private static void HandleLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _engineErrors++;
        }

        private static void Render(GameObject source, string name, int width, int height)
        {
            var root = UnityEngine.Object.Instantiate(source);
            source.SetActive(false);
            var cameraObject = new GameObject("Unit UI Validation Camera", typeof(Camera));
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
                var scroll = root.GetComponentInChildren<ScrollRect>();
                if (scroll != null) LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                Canvas.ForceUpdateCanvases();
                foreach (var label in root.GetComponentsInChildren<TMP_Text>())
                {
                    label.ForceMeshUpdate();
                    Check(label.font.HasCharacters(label.text, out uint[] missing, false, true), name + " glyphs " + label.name);
                    Check(!label.isTextOverflowing, name + " text fits " + label.name);
                }
                var corners = new Vector3[4];
                ((RectTransform)root.transform.Find("UnitCard")).GetWorldCorners(corners);
                var min = camera.WorldToViewportPoint(corners[0]);
                var max = camera.WorldToViewportPoint(corners[2]);
                Check(min.x >= 0 && min.y >= 0 && max.x <= 1 && max.y <= 1, name + " card on-screen");
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("Logs/UnitUiValidation");
                File.WriteAllBytes("Logs/UnitUiValidation/" + name + ".png", pixels.EncodeToPNG());
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
