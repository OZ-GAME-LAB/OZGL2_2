using System;
using Game.Core;
using Game.UI.Samples;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using static Game.UI.Editor.MvpHudBuilder;

namespace Game.UI.Editor
{
    /// <summary>기존 UI 테스트 씬의 별도 사본만 구성한다. 원본 씬/프리팹/공유 폰트는 저장하지 않는다.</summary>
    public static class MvpBuildingPhaseBuilder
    {
        public const string ScenePath = "Assets/Scenes/Test/MvpBuildingPhaseTest.unity";
        public const string FontPath = "Assets/Data/UI/Tests/BuildingPhaseTestFont.asset";

        [MenuItem("Game/UI/Create Missing Building Phase Test Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) return;
            if (System.IO.File.Exists(ScenePath)) throw new InvalidOperationException("Unexpected scene file: " + ScenePath);
            var sourceScene = SceneManager.GetSceneByPath(MvpRuntimeHudBuilder.ScenePath);
            if (sourceScene.IsValid() && sourceScene.isDirty) throw new InvalidOperationException("Save the source UI test scene first.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MvpBuildingActionBuilder.ActionPrefabPath);
            if (prefab == null) throw new InvalidOperationException("Existing building action prefab is required.");
            if (!AssetDatabase.CopyAsset(MvpRuntimeHudBuilder.ScenePath, ScenePath))
                throw new InvalidOperationException("Could not copy our runtime UI test scene.");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var flow = FindOne<GameFlowController>(scene);
                var controls = FindOne<MvpRuntimeHudSample>(scene);
                var font = GetFont();
                LayoutCoreControls(controls.transform);
                var panel = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<BuildingActionPanel>();
                Assign(panel.gameObject.AddComponent<CoreBuildingActionBinding>(), "_panel", panel, "_flow", flow);
                var samples = CreateCanvas("Building Phase Mock Selection - NOT GAMEPLAY", 6);
                samples.SetActive(false);
                var card = Box(samples.transform, "MockControls", Vector2.zero, Vector2.zero,
                    new Vector2(40, 20), new Vector2(800, 175), new Color32(25, 36, 48, 255));
                var slot = MakeButton(card, "Slot", "공간 선택", 18, 78, 228, 58);
                var building = MakeButton(card, "Building", "건물 선택", 264, 78, 228, 58);
                var unavailable = MakeButton(card, "Unavailable", "미개방 공간", 510, 78, 228, 58);
                var status = Label(card, "Status", "모의 견적 · 실제 건설/차감 없음", 20, Color.white, 18, 6, 720, 60);
                status.textWrappingMode = TextWrappingModes.Normal;
                Assign(samples.AddComponent<MvpBuildingPhaseSample>(), "_panel", panel, "_slotButton", slot,
                    "_buildingButton", building, "_unavailableButton", unavailable, "_status", status);
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var label in root.GetComponentsInChildren<TMP_Text>(true)) label.font = font;
                samples.SetActive(true);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save building phase test scene.");
                Debug.Log("[UI/MvpBuildingPhaseBuilder] Created isolated UI scene: " + ScenePath);
            }
            finally
            {
                if (!Application.isBatchMode)
                {
                    EditorSceneManager.CloseScene(scene, true);
                    if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                }
            }
        }

        [MenuItem("Game/UI/Apply Building Phase Test Layout And Validate")]
        public static void ApplyLayoutAndValidate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scenes first.");
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                LayoutCoreControls(FindOne<MvpRuntimeHudSample>(scene).transform);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save our test layout.");
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
            MvpRuntimeHudValidation.RunBuildingPhase();
        }

        private static void LayoutCoreControls(Transform controls)
        {
            var card = (RectTransform)controls.Find("TestCard");
            Place(card, 840, 200, 1000, 690);
            SetLabel(card, "Title", "건설 조작 · 코어 단계 연동 테스트", 32, 28, 604, 944, 58);
            SetLabel(card, "Description", "왼쪽에서 모의 공간/건물 선택 → 오른쪽 아래 웨이브 시작 → 조작 잠금 확인", 22, 28, 528, 944, 72);
            SetLabel(card, "Status", "초기화 대기", 22, 28, 450, 944, 72);
            SetLabel(card, "Help", "실제 코어 단계 사용 / 전투·보상·건물 견적은 테스트 입력", 21, 28, 22, 944, 64);
            string[] names = { "AddGold", "SpendGold", "RejectSpend", "Win", "Reward", "Lose", "Reset", "ToggleHud" };
            for (int i = 0; i < names.Length; i++)
            {
                var button = card.Find(names[i]).GetComponent<Button>();
                Place((RectTransform)button.transform, 28 + (i % 2) * 482, 358 - (i / 2) * 86, 454, 64);
                Place(button.GetComponentInChildren<TMP_Text>().rectTransform, 10, 0, 434, 64);
            }
            var shortcuts = (RectTransform)controls.Find("CoreShortcuts");
            Place(shortcuts, 840, 20, 660, 155);
            Place((RectTransform)shortcuts.Find("LastWave"), 20, 78, 294, 58);
            Place((RectTransform)shortcuts.Find("LastQuarter"), 330, 78, 310, 58);
            SetLabel(shortcuts, "Hint", "테스트 이동 · 보상 없음", 21, 20, 6, 620, 60);
        }

        private static Button MakeButton(Transform parent, string name, string title, float x, float y, float w, float h)
        {
            var button = Button(parent, name, title, w, h);
            Place((RectTransform)button.transform, x, y, w, h);
            return button;
        }

        private static void SetLabel(Transform parent, string name, string text, float size, float x, float y, float w, float h)
        {
            var label = parent.Find(name).GetComponent<TMP_Text>();
            label.text = text;
            label.fontSize = size;
            label.textWrappingMode = TextWrappingModes.Normal;
            Place(label.rectTransform, x, y, w, h);
        }

        private static T FindOne<T>(Scene scene) where T : Component
        {
            T found = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var item in root.GetComponentsInChildren<T>(true))
                {
                    if (found != null) throw new InvalidOperationException("Multiple " + typeof(T).Name);
                    found = item;
                }
            return found != null ? found : throw new InvalidOperationException("Missing " + typeof(T).Name);
        }

        private static TMP_FontAsset GetFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) return font;
            if (System.IO.File.Exists(FontPath)) throw new InvalidOperationException("Unexpected font at " + FontPath);
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/NotoSansKR/NotoSansCJKkr-Regular.otf");
            if (source == null) throw new InvalidOperationException("Korean source font is required.");
            font = TMP_FontAsset.CreateFontAsset(source, 64, 8, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            font.name = "BuildingPhaseTestFont";
            AssetDatabase.CreateAsset(font, FontPath);
            foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
            AssetDatabase.AddObjectToAsset(font.material, font);
            AssetDatabase.SaveAssetIfDirty(font);
            return font;
        }
    }
}
