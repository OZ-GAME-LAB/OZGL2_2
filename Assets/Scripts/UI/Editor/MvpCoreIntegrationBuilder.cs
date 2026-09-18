using System;
using System.Linq;
using Game.Core;
using Game.UI.Samples;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Game.UI.Editor
{
    /// <summary>우리 Runtime HUD 테스트 씬에만 최신 코어 참조와 UI를 추가한다. 공유 에셋은 저장하지 않는다.</summary>
    public static class MvpCoreIntegrationBuilder
    {
        public const string CatalogPath = "Assets/Data/Waves/TestWaveCatalog.asset";
        public const string FontPath = "Assets/Data/UI/Tests/CoreIntegrationTestFont.asset";

        [MenuItem("Game/UI/Upgrade Runtime HUD For Core Quarters")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(MvpRuntimeHudBuilder.ScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(MvpRuntimeHudBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new InvalidOperationException("Save the Runtime HUD test scene before upgrading.");
                SceneManager.SetActiveScene(scene);
                var ui = FindOne<GameUIController>(scene);
                var sample = FindOne<MvpRuntimeHudSample>(scene);
                var flow = FindOne<GameFlowController>(scene);
                var waves = FindOne<WaveController>(scene);
                var coreBinding = FindOne<CoreHudBinding>(scene);
                var catalog = AssetDatabase.LoadAssetAtPath<WaveSODictionary>(CatalogPath);
                if (catalog == null) throw new InvalidOperationException("Team wave catalog is required: " + CatalogPath);
                var title = ui.transform.Find("TopBar/WaveTitle")?.GetComponent<TMP_Text>();
                if (title == null) throw new InvalidOperationException("Expected Runtime HUD wave title is missing.");

                var sampleFields = new SerializedObject(sample);
                var binding = sampleFields.FindProperty("_runDecisionBinding").objectReferenceValue as CoreRunDecisionBinding;
                bool complete = binding != null &&
                    new SerializedObject(waves).FindProperty("_waveCatalog").objectReferenceValue == catalog &&
                    new SerializedObject(coreBinding).FindProperty("_quarterText").objectReferenceValue == title &&
                    sampleFields.FindProperty("_lastWaveButton").objectReferenceValue != null &&
                    sampleFields.FindProperty("_lastQuarterButton").objectReferenceValue != null;
                if (complete) return;

                MvpHudBuilder.EnsureFont();
                Undo.RegisterFullObjectHierarchyUndo(ui.gameObject, "Connect UI core integration");
                Undo.RegisterFullObjectHierarchyUndo(sample.gameObject, "Connect UI test controls");
                Undo.RecordObject(waves, "Connect team wave catalog in UI test scene");
                MvpHudBuilder.Assign(waves, "_waveCatalog", catalog);
                MvpHudBuilder.Assign(coreBinding, "_quarterText", title);
                if (binding == null) binding = CreateDecisionView(ui.transform, flow, waves);
                var shortcutRoot = sample.transform.Find("CoreShortcuts");
                if (shortcutRoot == null)
                {
                    shortcutRoot = MvpHudBuilder.Box(sample.transform, "CoreShortcuts", new Vector2(.5f, .5f),
                        new Vector2(.5f, .5f), new Vector2(-630, -325), new Vector2(630, -245),
                        new Color32(25, 36, 48, 250));
                    CreateShortcut(shortcutRoot, "LastWave", "마지막 웨이브로", 32);
                    CreateShortcut(shortcutRoot, "LastQuarter", "마지막 분기로", 340);
                    MvpHudBuilder.Label(shortcutRoot, "Hint", "테스트 이동 · 보상 없음", 22,
                        new Color32(145, 169, 182, 255), 648, 10, 570, 60);
                }
                MvpHudBuilder.Assign(sample, "_runDecisionBinding", binding,
                    "_lastWaveButton", shortcutRoot.Find("LastWave").GetComponent<Button>(),
                    "_lastQuarterButton", shortcutRoot.Find("LastQuarter").GetComponent<Button>());
                var help = sample.transform.Find("TestCard/Help")?.GetComponent<TMP_Text>();
                if (help != null)
                {
                    var helpFields = new SerializedObject(help);
                    helpFields.FindProperty("m_text").stringValue =
                        "웨이브 시작 → 적 전멸 → 보상 처리 / 기본 구간 돌파 후 종료·계속 선택";
                    helpFields.ApplyModifiedPropertiesWithoutUndo();
                }
                // New integration copy must not populate the team's shared dynamic font asset.
                var testFont = GetTestFont();
                foreach (var label in ui.GetComponentsInChildren<TMP_Text>(true)
                    .Concat(sample.GetComponentsInChildren<TMP_Text>(true)))
                {
                    label.font = testFont;
                    EditorUtility.SetDirty(label);
                    if (PrefabUtility.IsPartOfPrefabInstance(label))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save UI test scene.");
                Debug.Log("[UI/MvpCoreIntegrationBuilder] Updated only " + MvpRuntimeHudBuilder.ScenePath);
            }
            finally
            {
                if (openedHere && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static T FindOne<T>(Scene scene) where T : Component
        {
            var items = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            if (items.Length != 1) throw new InvalidOperationException("Expected one " + typeof(T).Name + " in UI test scene.");
            return items[0];
        }

        private static TMP_FontAsset GetTestFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (existing != null) return existing;
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/NotoSansKR/NotoSansCJKkr-Regular.otf");
            if (source == null) throw new InvalidOperationException("Existing Korean source font is required.");
            if (!AssetDatabase.IsValidFolder("Assets/Data/UI/Tests"))
                throw new InvalidOperationException("Existing UI test data folder is required.");
            var font = TMP_FontAsset.CreateFontAsset(source, 64, 8, GlyphRenderMode.SDFAA,
                2048, 2048, AtlasPopulationMode.Dynamic, true);
            font.name = "CoreIntegrationTestFont";
            AssetDatabase.CreateAsset(font, FontPath);
            foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
            AssetDatabase.AddObjectToAsset(font.material, font);
            AssetDatabase.SaveAssetIfDirty(font);
            return font;
        }

        private static void CreateShortcut(Transform parent, string name, string label, float x)
        {
            var button = MvpHudBuilder.Button(parent, name, label, 274, 60);
            MvpHudBuilder.Place((RectTransform)button.transform, x, 10, 274, 60);
        }

        private static CoreRunDecisionBinding CreateDecisionView(Transform parent, GameFlowController flow, WaveController waves)
        {
            var owner = new GameObject("Core Run Decision", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            owner.SetActive(false);
            owner.transform.SetParent(parent, false);
            var rect = (RectTransform)owner.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var canvas = owner.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
            var binding = owner.AddComponent<CoreRunDecisionBinding>();
            var overlay = MvpHudBuilder.Box(owner.transform, "DecisionOverlay", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, new Color32(4, 10, 16, 230));
            overlay.GetComponent<Image>().raycastTarget = true;
            var card = MvpHudBuilder.Box(overlay, "DecisionCard", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(-440, -210), new Vector2(440, 210), new Color32(25, 36, 48, 255));
            MvpHudBuilder.Label(card, "Title", "다음 도전을 선택하세요", 38, Color.white, 40, 304, 800, 62);
            var description = MvpHudBuilder.Label(card, "Description", "분기 돌파", 26,
                new Color32(145, 169, 182, 255), 40, 159, 800, 126);
            var finish = MvpHudBuilder.Button(card, "Finish", "승리로 종료", 380, 72);
            var next = MvpHudBuilder.Button(card, "Continue", "계속 도전", 380, 72);
            MvpHudBuilder.Place((RectTransform)finish.transform, 40, 51, 380, 72);
            MvpHudBuilder.Place((RectTransform)next.transform, 460, 51, 380, 72);
            finish.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = next };
            next.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = finish };
            MvpHudBuilder.Assign(binding, "_flow", flow, "_waves", waves, "_panelRoot", overlay.gameObject,
                "_descriptionText", description, "_finishButton", finish, "_continueButton", next);
            overlay.gameObject.SetActive(false);
            owner.SetActive(true);
            return binding;
        }
    }
}
