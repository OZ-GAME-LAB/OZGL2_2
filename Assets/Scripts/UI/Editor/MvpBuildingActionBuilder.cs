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
    public static class MvpBuildingActionBuilder
    {
        public const string ActionPrefabPath = "Assets/Prefabs/UI/MvpBuildingActions.prefab";
        public const string ActionScenePath = "Assets/Scenes/Test/MvpBuildingActionTest.unity";

        private static readonly Color Panel = new Color32(25, 36, 48, 255);
        private static readonly Color Paper = new Color32(233, 241, 244, 255);
        private static readonly Color Mint = new Color32(121, 229, 195, 255);
        private static readonly Color Muted = new Color32(145, 169, 182, 255);

        [MenuItem("Game/UI/Create Missing Building Action Assets")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(MvpBuildingUiBuilder.BuildingPrefabPath) == null)
                throw new InvalidOperationException("Create common HUD and building info assets first.");
            EnsureFont();
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(ActionPrefabPath) == null) CreatePrefab();
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ActionScenePath) == null) CreateScene(scene);
                Debug.Log("[UI/MvpBuildingActionBuilder] Missing action UI assets created; existing assets preserved.");
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void CreatePrefab()
        {
            var root = CreateCanvas("MvpBuildingActions", 5);
            root.SetActive(false);
            try
            {
                var controller = root.AddComponent<BuildingActionPanel>();
                var card = Box(root.transform, "ActionCard", new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                    new Vector2(40, -340), new Vector2(800, 350), Panel);
                var title = Label(card, "Title", "", 30, Paper, 28, 618, 704, 52);
                title.enableAutoSizing = true;
                title.fontSizeMin = 20;
                title.fontSizeMax = 30;
                Label(card, "Subtitle", "건물 행동 · 표시된 금액은 담당 시스템의 견적", 22, Muted, 28, 563, 704, 40);
                var status = Label(card, "Status", "", 22, Mint, 28, 16, 704, 98);
                status.textWrappingMode = TextWrappingModes.Normal;
                Assign(controller, "_title", title, "_status", status);
                CreateRow(controller, card, "_build", "Build", "건설", 425);
                CreateRow(controller, card, "_upgrade", "Upgrade", "업그레이드", 280);
                CreateRow(controller, card, "_dismantle", "Dismantle", "해체", 135);
                controller.HideActions();
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, ActionPrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void CreateRow(BuildingActionPanel controller, Transform parent, string field, string name, string text, float y)
        {
            var row = Box(parent, name + "Row", Vector2.zero, Vector2.zero,
                new Vector2(28, y), new Vector2(732, y + 126), new Color32(34, 49, 63, 255));
            var button = Button(row, name, text, 174, 54);
            Place((RectTransform)button.transform, 16, 58, 174, 54);
            var quote = Label(row, "Quote", "", 24, Paper, 206, 52, 482, 64);
            quote.textWrappingMode = TextWrappingModes.Normal;
            quote.enableAutoSizing = true;
            quote.fontSizeMin = 18;
            quote.fontSizeMax = 24;
            var reason = Label(row, "Reason", "", 20, Muted, 16, 4, 672, 50);
            reason.textWrappingMode = TextWrappingModes.Normal;
            Assign(controller, field + "._button", button, field + "._quote", quote, field + "._reason", reason);
        }

        private static void CreateScene(Scene scene)
        {
            var camera = new GameObject("Action UI Test Camera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(15, 22, 31, 255);
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule))
                .GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var hud = InstantiatePrefab<GameUIController>(PrefabPath, scene);
            var info = InstantiatePrefab<BuildingInfoPanel>(MvpBuildingUiBuilder.BuildingPrefabPath, scene);
            var panel = InstantiatePrefab<BuildingActionPanel>(ActionPrefabPath, scene);
            var controls = CreateCanvas("Action Test Controls - NOT GAMEPLAY", 0);
            controls.SetActive(false);
            var card = Box(controls.transform, "TestControls", Vector2.zero, Vector2.zero,
                new Vector2(40, 20), new Vector2(1130, 185), Panel);
            Label(card, "Title", "UI 테스트 전용 · 아래 버튼으로 응답을 직접 재현합니다", 22, Mint, 16, 118, 1058, 38);
            var a = ControlButton(card, "SlotA", "공간 A", 16);
            var b = ControlButton(card, "SlotB", "건물 B", 193);
            var blocked = ControlButton(card, "Blocked", "조건 불가", 370);
            var phase = ControlButton(card, "Phase", "전투 전환", 547);
            var success = ControlButton(card, "Success", "성공 응답", 724);
            var failure = ControlButton(card, "Failure", "실패 응답", 901);
            var status = Label(card, "Status", "", 18, Paper, 16, 6, 1058, 46);
            status.textWrappingMode = TextWrappingModes.Normal;
            var sample = controls.AddComponent<MvpBuildingActionSample>();
            Assign(sample, "_panel", panel, "_info", info, "_hud", hud, "_slotAButton", a,
                "_slotBButton", b, "_blockedButton", blocked, "_phaseButton", phase,
                "_successButton", success, "_failureButton", failure, "_status", status);
            controls.SetActive(true);
            EditorSceneManager.SaveScene(scene, ActionScenePath);
        }

        private static T InstantiatePrefab<T>(string path, Scene scene) where T : Component =>
            ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene)).GetComponent<T>();

        private static Button ControlButton(Transform parent, string name, string text, float x)
        {
            var button = Button(parent, name, text, 163, 54);
            Place((RectTransform)button.transform, x, 58, 163, 54);
            return button;
        }
    }
}
