using System;
using Game.UI.Samples;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Game.UI.Editor.MvpHudBuilder;

namespace Game.UI.Editor
{
    /// <summary>누락된 건물 UI 에셋만 생성한다. 기존 에셋과 열린 씬은 덮어쓰지 않는다.</summary>
    public static class MvpBuildingUiBuilder
    {
        public const string BuildingPrefabPath = "Assets/Prefabs/UI/MvpBuildingInfo.prefab";
        public const string BuildingScenePath = "Assets/Scenes/Test/MvpBuildingInfoTest.unity";

        private static readonly Color PanelColor = new Color32(25, 36, 48, 255);
        private static readonly Color PaperColor = new Color32(233, 241, 244, 255);
        private static readonly Color MintColor = new Color32(121, 229, 195, 255);
        private static readonly Color MutedColor = new Color32(145, 169, 182, 255);

        [MenuItem("Game/UI/Create Missing Building Info Assets")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                throw new InvalidOperationException("Create the common HUD assets first.");
            EnsureFont();
            var previous = SceneManager.GetActiveScene();
            var staging = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(staging);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(BuildingPrefabPath) == null) CreateInfoPrefab();
            }
            finally
            {
                if (!Application.isBatchMode)
                {
                    if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                    EditorSceneManager.CloseScene(staging, true);
                }
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BuildingScenePath) == null) CreateInfoScene();
            Debug.Log("[UI/MvpBuildingUiBuilder] Building info prefab and isolated test scene are ready.");
        }

        private static void CreateInfoPrefab()
        {
            var root = CreateCanvas("MvpBuildingInfo", 5);
            root.SetActive(false);
            try
            {
                var controller = root.AddComponent<BuildingInfoPanel>();
                var card = Box(root.transform, "InfoCard", new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                    new Vector2(-620, -348), new Vector2(-40, 320), PanelColor);
                Box(card, "Accent", new Vector2(0, 1), Vector2.one,
                    new Vector2(0, -4), Vector2.zero, MintColor);
                var empty = Label(card, "EmptyState", "건물을 선택하세요\n선택한 건물의 정보가 여기에 표시됩니다.",
                    27, MutedColor, 36, 185, 508, 190);
                empty.textWrappingMode = TextWrappingModes.Normal;
                empty.alignment = TextAlignmentOptions.Center;
                var content = new GameObject("Content", typeof(RectTransform));
                content.transform.SetParent(card, false);
                var contentRect = (RectTransform)content.transform;
                contentRect.anchorMin = Vector2.zero;
                contentRect.anchorMax = Vector2.one;
                contentRect.offsetMin = contentRect.offsetMax = Vector2.zero;

                var category = Label(content.transform, "Category", "", 20, MintColor, 32, 607, 360, 40);
                var close = Button(content.transform, "Close", "닫기", 100, 44);
                Place((RectTransform)close.transform, 448, 598, 100, 44);
                var name = Label(content.transform, "Name", "", 34, PaperColor, 32, 515, 390, 76);
                name.textWrappingMode = TextWrappingModes.Normal;
                name.enableAutoSizing = true;
                name.fontSizeMin = 22;
                name.fontSizeMax = 34;
                var level = Label(content.transform, "Level", "", 24, MutedColor, 32, 465, 390, 44);
                var iconBox = Box(content.transform, "IconFrame", Vector2.zero, Vector2.zero,
                    new Vector2(448, 495), new Vector2(548, 585), new Color32(41, 61, 73, 255));
                var icon = Box(iconBox, "Icon", Vector2.zero, Vector2.one,
                    new Vector2(8, 8), new Vector2(-8, -8), Color.white).GetComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                var placeholder = Label(iconBox, "Placeholder", "건물", 24, MutedColor, 0, 0, 100, 90);
                placeholder.alignment = TextAlignmentOptions.Center;

                var viewport = Box(content.transform, "DetailsViewport", Vector2.zero, Vector2.zero,
                    new Vector2(32, 72), new Vector2(548, 445), PanelColor);
                viewport.gameObject.AddComponent<RectMask2D>();
                var scroll = viewport.gameObject.AddComponent<ScrollRect>();
                scroll.viewport = viewport;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 28;
                var details = new GameObject("Details", typeof(RectTransform), typeof(VerticalLayoutGroup),
                    typeof(ContentSizeFitter));
                var detailsRect = (RectTransform)details.transform;
                detailsRect.SetParent(viewport, false);
                detailsRect.anchorMin = new Vector2(0, 1);
                detailsRect.anchorMax = Vector2.one;
                detailsRect.pivot = new Vector2(0.5f, 1);
                detailsRect.sizeDelta = Vector2.zero;
                var layout = details.GetComponent<VerticalLayoutGroup>();
                layout.spacing = 24;
                layout.padding = new RectOffset(0, 12, 0, 8);
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                details.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                scroll.content = detailsRect;
                var description = DetailLabel(detailsRect, "Description", PaperColor);
                var production = DetailLabel(detailsRect, "Production", MintColor);
                var effect = DetailLabel(detailsRect, "Effect", MintColor);
                Label(content.transform, "ScrollHint", "긴 내용은 스크롤해서 확인하세요", 18, MutedColor, 32, 22, 516, 32);
                Assign(controller, "_emptyState", empty.gameObject, "_contentPanel", content,
                    "_nameText", name, "_categoryText", category, "_levelText", level,
                    "_icon", icon, "_iconPlaceholder", placeholder.gameObject, "_closeButton", close,
                    "_detailsScroll", scroll, "_descriptionText", description,
                    "_productionText", production, "_effectText", effect);
                controller.HideBuildingInfo();
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, BuildingPrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static TMP_Text DetailLabel(Transform parent, string name, Color color)
        {
            var label = Label(parent, name, "", 24, color, 0, 0, 504, 60);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.alignment = TextAlignmentOptions.TopLeft;
            return label;
        }

        private static void CreateInfoScene()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var camera = new GameObject("Building UI Test Camera", typeof(Camera)).GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(15, 22, 31, 255);
                camera.orthographic = true;
                camera.transform.position = new Vector3(0, 0, -10);
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                var hud = ((GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene)).GetComponent<GameUIController>();
                var panel = ((GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(BuildingPrefabPath), scene)).GetComponent<BuildingInfoPanel>();
                var controls = CreateCanvas("Building Info Test Controls - NOT GAMEPLAY", 0);
                controls.SetActive(false);
                var card = Box(controls.transform, "TestCard", new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                    new Vector2(40, -280), new Vector2(780, 265), PanelColor);
                Label(card, "Eyebrow", "UI / BUILDING     ·     TEST SCENE", 18, MintColor, 32, 486, 676, 28);
                Label(card, "Title", "건물 선택 · 정보 테스트", 34, PaperColor, 32, 416, 676, 52);
                Label(card, "Description", "임시 데이터 · 건설/차감/전투 없음", 23, MutedColor, 32, 361, 676, 36);
                var resource = TestButton(card, "Resource", "자원 건물", 32, 263);
                var warrior = TestButton(card, "Warrior", "전사 생산", 388, 263);
                var archer = TestButton(card, "Archer", "궁사 생산", 32, 183);
                var support = TestButton(card, "Support", "지원 건물", 388, 183);
                var refresh = TestButton(card, "Refresh", "레벨 표시 갱신", 32, 103);
                var clear = TestButton(card, "Clear", "선택 해제", 388, 103);
                var status = Label(card, "Status", "", 19, PaperColor, 32, 23, 676, 58);
                status.textWrappingMode = TextWrappingModes.Normal;
                var sample = controls.AddComponent<MvpBuildingInfoSample>();
                Assign(sample, "_panel", panel, "_hud", hud, "_resourceButton", resource,
                    "_warriorButton", warrior, "_archerButton", archer, "_supportButton", support,
                    "_refreshButton", refresh, "_clearButton", clear, "_statusText", status);
                controls.SetActive(true);
                EditorSceneManager.SaveScene(scene, BuildingScenePath);
            }
            finally
            {
                if (!Application.isBatchMode)
                {
                    EditorSceneManager.CloseScene(scene, true);
                    if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                }
            }
        }

        private static Button TestButton(Transform parent, string name, string text, float x, float y)
        {
            var button = Button(parent, name, text, 320, 60);
            Place((RectTransform)button.transform, x, y, 320, 60);
            return button;
        }
    }
}
