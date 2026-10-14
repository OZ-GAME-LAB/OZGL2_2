using System;
using Game.UI.Samples;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using static Game.UI.Editor.MvpHudBuilder;

namespace Game.UI.Editor
{
    /// <summary>UI 전용 에셋 생성 및 보상 와이어프레임 배치 보완. 공유 폰트/씬/빌드 설정은 변경하지 않는다.</summary>
    public static class MvpArtifactRewardBuilder
    {
        public const string ScenePath = "Assets/Scenes/Test/MvpArtifactRewardTest.unity";
        public const string PrefabPath = "Assets/Prefabs/UI/MvpArtifactReward.prefab";
        public const string FontPath = "Assets/Data/UI/Tests/ArtifactRewardTestFont.asset";

        private static readonly Color Paper = new Color32(233, 241, 244, 255);
        private static readonly Color Muted = new Color32(145, 169, 182, 255);
        private static readonly Color Mint = new Color32(121, 229, 195, 255);

        [MenuItem("Game/UI/Create Missing Artifact Reward Assets")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            UpgradeRewardLayout();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) != null) return;
            var previous = SceneManager.GetActiveScene();
            // 신규 작업은 빈 보조 씬에서 생성해 사용자 씬을 dirty로 만들지 않는다.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var font = GetFont();
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                {
                    if (System.IO.File.Exists(PrefabPath)) throw new InvalidOperationException("Unexpected asset at " + PrefabPath);
                    CreatePrefab(font);
                }
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) return;
                if (System.IO.File.Exists(ScenePath)) throw new InvalidOperationException("Unexpected asset at " + ScenePath);
                var camera = new GameObject("Artifact UI Test Camera", typeof(Camera)).GetComponent<Camera>();
                camera.tag = "MainCamera";
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(15, 22, 31, 255);
                camera.orthographic = true;
                camera.transform.position = new Vector3(0, 0, -10);
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                var panel = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene))
                    .GetComponent<ArtifactRewardPanel>();
                var demo = CreateCanvas("Artifact Reward Sample - NOT GAMEPLAY", 0);
                demo.SetActive(false);
                var area = Box(demo.transform, "TestControls", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                    new Vector2(-650, -220), new Vector2(650, 220), new Color32(25, 36, 48, 255));
                Label(area, "Title", "아티팩트 선택 UI 테스트", 40, Paper, 48, 328, 1204, 64);
                Label(area, "Scope", "모의 보상 · 효과 적용 없음 · 코어 진행과 연결되지 않은 독립 씬", 26, Muted, 48, 245, 1204, 52);
                var status = Label(area, "Status", "모의 요청 대기", 28, Mint, 48, 160, 1204, 52);
                var open = Button(area, "OpenReward", "새 모의 전투 보상 열기", 500, 72);
                Place((RectTransform)open.transform, 48, 48, 500, 72);
                Assign(demo.AddComponent<MvpArtifactRewardSample>(), "_panel", panel, "_openButton", open, "_statusText", status);
                ApplyFont(demo, font);
                demo.SetActive(true);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save reward test scene.");
                Debug.Log("[UI/MvpArtifactRewardBuilder] Created " + ScenePath);
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

        private static void CreatePrefab(TMP_FontAsset font)
        {
            var root = CreateCanvas("MvpArtifactReward", 100);
            root.SetActive(false);
            try
            {
                var panel = root.AddComponent<ArtifactRewardPanel>();
                var overlay = Box(root.transform, "RewardOverlay", Vector2.zero, Vector2.one,
                    Vector2.zero, Vector2.zero, new Color32(4, 10, 16, 242));
                var card = Box(overlay, "RewardCard", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                    new Vector2(-720, -430), new Vector2(720, 430), new Color32(25, 36, 48, 255));
                Label(card, "Title", "전투 승리 · 아티팩트 선택", 42, Paper, 56, 764, 1328, 62);
                Label(card, "Instruction", "3개 중 1개 선택 → 확정     /     미선택 시 모두 포기", 24, Muted, 56, 719, 1328, 40);
                var reward = Label(card, "Reward", "획득 골드 --     ·     획득 보석 --", 28, Mint, 56, 665, 1328, 44);
                var fields = new SerializedObject(panel);
                var cards = fields.FindProperty("_cards");
                cards.arraySize = 3;
                var buttons = new Button[3];
                for (int i = 0; i < 3; i++)
                {
                    float x = 56 + i * 452;
                    var item = Box(card, "Candidate" + i, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero,
                        new Color32(37, 52, 66, 255));
                    Place(item, x, 210, 424, 430);
                    var button = item.gameObject.AddComponent<Button>();
                    button.targetGraphic = item.GetComponent<Image>();
                    buttons[i] = button;
                    var selection = Box(item, "Selection", new Vector2(0, 1), Vector2.one,
                        new Vector2(0, -6), Vector2.zero, Mint);
                    selection.GetComponent<Image>().raycastTarget = false;
                    Label(selection, "Selected", "선택됨", 21, Mint, 24, -44, 376, 34);
                    var name = Label(item, "Name", "아티팩트", 28, Paper, 24, 326, 376, 42);
                    name.enableAutoSizing = true;
                    name.fontSizeMin = 20;
                    var rarity = Label(item, "Rarity", "등급", 23, Mint, 24, 284, 376, 36);
                    var iconRoot = Box(item, "IconFrame", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero,
                        new Color32(21, 32, 43, 255));
                    Place(iconRoot, 24, 166, 376, 98);
                    iconRoot.GetComponent<Image>().raycastTarget = false;
                    var icon = Box(iconRoot, "Icon", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                        new Vector2(-43, -43), new Vector2(43, 43), Color.white).GetComponent<Image>();
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                    var placeholder = Label(iconRoot, "MissingIcon", "이미지 준비 중", 22, Muted, 0, 0, 376, 98);
                    placeholder.alignment = TextAlignmentOptions.Center;
                    var effect = Label(item, "Effect", "효과 설명", 23, Paper, 24, 32, 376, 112);
                    effect.textWrappingMode = TextWrappingModes.Normal;
                    effect.enableAutoSizing = true;
                    effect.fontSizeMin = 18;
                    effect.alignment = TextAlignmentOptions.TopLeft;
                    var row = cards.GetArrayElementAtIndex(i);
                    row.FindPropertyRelative("Button").objectReferenceValue = button;
                    row.FindPropertyRelative("Name").objectReferenceValue = name;
                    row.FindPropertyRelative("Rarity").objectReferenceValue = rarity;
                    row.FindPropertyRelative("Effect").objectReferenceValue = effect;
                    row.FindPropertyRelative("Icon").objectReferenceValue = icon;
                    row.FindPropertyRelative("MissingIcon").objectReferenceValue = placeholder.gameObject;
                    row.FindPropertyRelative("Selection").objectReferenceValue = selection.gameObject;
                    selection.gameObject.SetActive(false);
                }
                fields.ApplyModifiedPropertiesWithoutUndo();
                var status = Label(card, "Status", "", 24, Muted, 56, 135, 1328, 46);
                status.alignment = TextAlignmentOptions.Center;
                var clear = Button(card, "Clear", "선택 해제", 270, 64);
                var confirm = Button(card, "Confirm", "모두 포기하고 계속", 600, 64);
                Place((RectTransform)clear.transform, 269, 50, 270, 64);
                Place((RectTransform)confirm.transform, 571, 50, 600, 64);
                for (int i = 0; i < 3; i++) buttons[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit, selectOnLeft = i > 0 ? buttons[i - 1] : null,
                    selectOnRight = i < 2 ? buttons[i + 1] : null, selectOnDown = confirm
                };
                clear.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = confirm, selectOnUp = buttons[0] };
                confirm.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = clear, selectOnUp = buttons[1] };
                Assign(panel, "_panelRoot", overlay.gameObject, "_rewardText", reward, "_statusText", status,
                    "_confirmButton", confirm, "_confirmText", confirm.GetComponentInChildren<TMP_Text>(), "_clearButton", clear);
                ConfigurePaging(root, font);
                ConfigureReferenceLayout(root);
                ApplyFont(root, font);
                overlay.gameObject.SetActive(false);
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void ApplyFont(GameObject root, TMP_FontAsset font)
        {
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true)) label.font = font;
        }

        private static void UpgradeRewardLayout()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (asset == null) return;
            var panel = asset.GetComponent<ArtifactRewardPanel>();
            if (panel == null) throw new InvalidOperationException("Unexpected prefab at " + PrefabPath);
            var fields = new SerializedObject(panel);
            var rarityRect = asset.transform.Find("RewardOverlay/RewardCard/Candidate0/Rarity") as RectTransform;
            if (fields.FindProperty("_previousPageButton").objectReferenceValue != null &&
                fields.FindProperty("_nextPageButton").objectReferenceValue != null &&
                fields.FindProperty("_pageText").objectReferenceValue != null &&
                fields.FindProperty("_instructionText").objectReferenceValue != null &&
                asset.transform.Find("RewardOverlay/RewardCard/VictoryHeaderFrame") != null &&
                rarityRect != null && rarityRect.sizeDelta.y >= 40) return;
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                ConfigurePaging(root, GetFont());
                ConfigureReferenceLayout(root);
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new InvalidOperationException("Could not save reward layout upgrade.");
                Debug.Log("[UI/MvpArtifactRewardBuilder] Applied PDF reward layout only to " + PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ConfigurePaging(GameObject root, TMP_FontAsset font)
        {
            var card = root.transform.Find("RewardOverlay/RewardCard");
            var title = card != null ? card.Find("Title")?.GetComponent<TMP_Text>() : null;
            var instruction = card != null ? card.Find("Instruction")?.GetComponent<TMP_Text>() : null;
            if (title == null || instruction == null) throw new InvalidOperationException("Reward view structure does not match this builder.");
            Place(title.rectTransform, 56, 764, 896, 62);
            var previous = card.Find("PreviousPage")?.GetComponent<Button>() ?? Button(card, "PreviousPage", "이전", 100, 60);
            var next = card.Find("NextPage")?.GetComponent<Button>() ?? Button(card, "NextPage", "다음", 100, 60);
            var page = card.Find("PageCounter")?.GetComponent<TMP_Text>() ?? Label(card, "PageCounter", "1 / 1", 24, Muted, 1092, 764, 180, 60);
            Place((RectTransform)previous.transform, 980, 764, 100, 60);
            Place((RectTransform)next.transform, 1284, 764, 100, 60);
            page.alignment = TextAlignmentOptions.Center;
            foreach (var label in previous.GetComponentsInChildren<TMP_Text>(true)) label.font = font;
            foreach (var label in next.GetComponentsInChildren<TMP_Text>(true)) label.font = font;
            page.font = font;
            Assign(root.GetComponent<ArtifactRewardPanel>(), "_instructionText", instruction,
                "_previousPageButton", previous, "_nextPageButton", next, "_pageText", page);
            previous.gameObject.SetActive(false);
            next.gameObject.SetActive(false);
            page.gameObject.SetActive(false);
        }

        // PDF는 배치용 와이어프레임이다. 색상/폰트는 기존 임시 스타일을 유지하고
        // 기존 오브젝트와 직렬화 참조를 재사용해 공유 씬이나 게임 규칙을 변경하지 않는다.
        private static void ConfigureReferenceLayout(GameObject root)
        {
            var card = (RectTransform)root.transform.Find("RewardOverlay/RewardCard");
            card.sizeDelta = new Vector2(1440, 900);
            AddFrame(card, new Color32(75, 99, 113, 255));
            var header = card.Find("VictoryHeaderFrame") as RectTransform;
            if (header == null)
                header = Box(card, "VictoryHeaderFrame", Vector2.zero, Vector2.zero,
                    Vector2.zero, Vector2.zero, new Color32(32, 47, 60, 255));
            Place(header, 460, 824, 520, 92);
            header.GetComponent<Image>().raycastTarget = false;
            header.SetAsFirstSibling();
            AddFrame(header, new Color32(75, 99, 113, 255));

            var title = LayoutLabel(card, "Title", 460, 824, 520, 92, 40, TextAlignmentOptions.Center);
            title.text = "전투 승리";
            var reward = LayoutLabel(card, "Reward", 310, 714, 820, 88, 29, TextAlignmentOptions.Center);
            reward.text = "획득 골드 --\n획득 보석 --";
            LayoutLabel(card, "Instruction", 56, 666, 1328, 36, 23, TextAlignmentOptions.Center);

            for (int i = 0; i < 3; i++)
            {
                var candidate = (RectTransform)card.Find("Candidate" + i);
                Place(candidate, 164 + i * 380, 178, 352, 466);
                AddFrame(candidate, new Color32(80, 102, 115, 255));
                var iconFrame = (RectTransform)candidate.Find("IconFrame");
                Place(iconFrame, 24, 248, 304, 182);
                AddFrame(iconFrame, new Color32(75, 99, 113, 255));
                // 실제 이미지가 제공될 때까지 기존 빈 이미지 안내를 유지한다.
                var icon = (RectTransform)iconFrame.Find("Icon");
                icon.sizeDelta = new Vector2(158, 158);
                LayoutLabel(iconFrame, "MissingIcon", 0, 0, 304, 182, 22, TextAlignmentOptions.Center);
                LayoutLabel(candidate, "Name", 16, 194, 320, 42, 28, TextAlignmentOptions.Center);
                LayoutLabel(candidate, "Rarity", 16, 148, 320, 40, 23, TextAlignmentOptions.Center);
                LayoutLabel(candidate, "Effect", 24, 24, 304, 110, 23, TextAlignmentOptions.Top);
                LayoutLabel(candidate, "Selection/Selected", 12, -30, 328, 28, 18, TextAlignmentOptions.Center);
            }

            LayoutLabel(card, "Status", 56, 120, 1328, 40, 23, TextAlignmentOptions.Center);
            LayoutButton(card, "Confirm", 480, 30, 480, 68, true);
            LayoutButton(card, "Clear", 252, 38, 188, 52, false);
            // 가변 후보용 추가 조작은 주 버튼과 분리해 카드 좌우에 배치한다.
            LayoutButton(card, "PreviousPage", 52, 382, 88, 60, false);
            LayoutButton(card, "NextPage", 1300, 382, 88, 60, false);
            LayoutLabel(card, "PageCounter", 1210, 730, 174, 56, 23, TextAlignmentOptions.Center);
        }

        private static TMP_Text LayoutLabel(Transform parent, string path, float x, float y,
            float width, float height, float size, TextAlignmentOptions alignment)
        {
            var label = parent.Find(path)?.GetComponent<TMP_Text>();
            if (label == null) throw new InvalidOperationException("Missing reward label: " + path);
            Place(label.rectTransform, x, y, width, height);
            label.fontSize = size;
            label.fontSizeMax = size;
            label.alignment = alignment;
            return label;
        }

        private static void LayoutButton(Transform parent, string name, float x, float y,
            float width, float height, bool primary)
        {
            var button = parent.Find(name).GetComponent<Button>();
            Place((RectTransform)button.transform, x, y, width, height);
            var label = LayoutLabel(button.transform, "Label", 10, 0, width - 20, height, 24, TextAlignmentOptions.Center);
            if (!primary)
            {
                button.GetComponent<Image>().color = new Color32(43, 61, 75, 255);
                label.color = Paper;
                AddFrame((RectTransform)button.transform, new Color32(75, 99, 113, 255));
            }
        }

        private static void AddFrame(RectTransform rect, Color color)
        {
            var outline = rect.GetComponent<Outline>() ?? rect.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(2, -2);
        }

        private static TMP_FontAsset GetFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) return font;
            if (System.IO.File.Exists(FontPath)) throw new InvalidOperationException("Unexpected font asset at " + FontPath);
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/NotoSansKR/NotoSansCJKkr-Regular.otf");
            if (source == null) throw new InvalidOperationException("Korean source font is required.");
            font = TMP_FontAsset.CreateFontAsset(source, 64, 8, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            font.name = "ArtifactRewardTestFont";
            AssetDatabase.CreateAsset(font, FontPath);
            foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
            AssetDatabase.AddObjectToAsset(font.material, font);
            AssetDatabase.SaveAssetIfDirty(font);
            return font;
        }
    }
}
