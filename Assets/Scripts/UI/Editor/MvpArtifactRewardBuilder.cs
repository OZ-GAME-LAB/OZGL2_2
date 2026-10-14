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
        public const string FallbackIconPath = "Assets/Art/Sprites/UI/ArtifactUnknownRelic.png";

        private static readonly Color Paper = new Color32(244, 231, 211, 255);
        private static readonly Color Muted = new Color32(184, 150, 142, 255);
        private static readonly Color Mint = new Color32(219, 157, 70, 255);
        private static readonly Color Frame = new Color32(139, 98, 58, 255);
        private static readonly Color Board = new Color32(24, 15, 24, 255);
        private static readonly Color Surface = new Color32(38, 24, 37, 255);
        private static readonly Color Primary = new Color32(132, 31, 51, 255);
        private static readonly Color Ember = new Color32(118, 18, 50, 68);

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
                    Vector2.zero, Vector2.zero, new Color32(7, 5, 9, 244));
                var card = Box(overlay, "RewardCard", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                    new Vector2(-640, -380), new Vector2(640, 380), Board);
                Label(card, "Title", "승리 보상", 40, Paper, 430, 688, 420, 72);
                Label(card, "Instruction", "3개의 유물 중 하나를 선택하세요", 20, Muted, 64, 596, 1152, 34);
                var reward = Label(card, "Reward", "보상 집계 중", 28, Mint, 260, 635, 760, 40);
                var fields = new SerializedObject(panel);
                var cards = fields.FindProperty("_cards");
                cards.arraySize = 3;
                var buttons = new Button[3];
                for (int i = 0; i < 3; i++)
                {
                    float x = 56 + i * 452;
                    var item = Box(card, "Candidate" + i, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero,
                        Surface);
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
                        new Color32(20, 14, 22, 255));
                    Place(iconRoot, 24, 166, 376, 98);
                    iconRoot.GetComponent<Image>().raycastTarget = false;
                    var icon = Box(iconRoot, "Icon", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                        new Vector2(-43, -43), new Vector2(43, 43), Color.white).GetComponent<Image>();
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                    var placeholder = Label(iconRoot, "MissingIcon", "유물", 30, Muted, 0, 0, 376, 98);
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
                var confirm = Button(card, "Confirm", "건너뛰기", 600, 64);
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
            var rewardCard = asset.transform.Find("RewardOverlay/RewardCard") as RectTransform;
            var title = asset.transform.Find("RewardOverlay/RewardCard/Title")?.GetComponent<TMP_Text>();
            var rewardRect = asset.transform.Find("RewardOverlay/RewardCard/Reward") as RectTransform;
            var instructionRect = asset.transform.Find("RewardOverlay/RewardCard/Instruction") as RectTransform;
            var pageRect = asset.transform.Find("RewardOverlay/RewardCard/PageCounter") as RectTransform;
            var firstCardRect = asset.transform.Find("RewardOverlay/RewardCard/Candidate0") as RectTransform;
            var previousRect = asset.transform.Find("RewardOverlay/RewardCard/PreviousPage") as RectTransform;
            var previousLabel = asset.transform.Find("RewardOverlay/RewardCard/PreviousPage/Label")?.GetComponent<TMP_Text>();
            var confirmLabel = asset.transform.Find("RewardOverlay/RewardCard/Confirm/Label")?.GetComponent<TMP_Text>();
            var effectRect = asset.transform.Find("RewardOverlay/RewardCard/Candidate0/Effect") as RectTransform;
            var watermark = asset.transform.Find("RewardOverlay/RewardCard/RelicWatermark");
            var runeGlow = asset.transform.Find("RewardOverlay/RewardCard/Candidate0/IconFrame/RuneGlow");
            if (fields.FindProperty("_previousPageButton").objectReferenceValue != null &&
                fields.FindProperty("_nextPageButton").objectReferenceValue != null &&
                fields.FindProperty("_pageText").objectReferenceValue != null &&
                fields.FindProperty("_instructionText").objectReferenceValue != null &&
                fields.FindProperty("_fallbackIcon").objectReferenceValue != null &&
                asset.transform.Find("RewardOverlay/RewardCard/VictoryHeaderFrame") != null &&
                rewardCard != null && Mathf.Approximately(rewardCard.sizeDelta.x, 1280) &&
                Mathf.Approximately(rewardCard.sizeDelta.y, 760) && title != null && title.text == "승리 보상" &&
                rewardRect != null && Mathf.Approximately(rewardRect.sizeDelta.y, 42) &&
                instructionRect != null && Mathf.Approximately(instructionRect.sizeDelta.y, 34) &&
                pageRect != null && Mathf.Approximately(pageRect.sizeDelta.y, 24) &&
                firstCardRect != null && Mathf.Approximately(firstCardRect.anchoredPosition.y, 135) &&
                previousRect != null && Mathf.Approximately(previousRect.anchoredPosition.x, 32) &&
                previousLabel != null && previousLabel.text == "이전" && confirmLabel != null &&
                Mathf.Approximately(confirmLabel.fontSize, 18) && watermark != null &&
                runeGlow is RectTransform runeGlowRect &&
                runeGlowRect.pivot == new Vector2(0.5f, 0.5f) &&
                rewardCard.GetComponent<Image>().color == Board && effectRect != null &&
                Mathf.Approximately(effectRect.sizeDelta.x, 306)) return;
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

        // PDF는 배치용 와이어프레임이다. 기존 오브젝트와 직렬화 참조를 재사용하고
        // 플레이어용 어두운 판타지 팔레트만 적용해 공유 씬이나 게임 규칙을 변경하지 않는다.
        private static void ConfigureReferenceLayout(GameObject root)
        {
            var fallbackIcon = GetFallbackIcon();
            Assign(root.GetComponent<ArtifactRewardPanel>(), "_fallbackIcon", fallbackIcon);
            var card = (RectTransform)root.transform.Find("RewardOverlay/RewardCard");
            card.sizeDelta = new Vector2(1280, 760);
            card.GetComponent<Image>().color = Board;
            card.parent.GetComponent<Image>().color = new Color32(5, 2, 7, 247);
            AddFrame(card, Frame);
            var header = card.Find("VictoryHeaderFrame") as RectTransform;
            if (header == null)
                header = Box(card, "VictoryHeaderFrame", Vector2.zero, Vector2.zero,
                    Vector2.zero, Vector2.zero, new Color32(49, 31, 43, 255));
            Place(header, 430, 688, 420, 72);
            header.GetComponent<Image>().color = new Color32(54, 24, 39, 255);
            header.GetComponent<Image>().raycastTarget = false;
            header.SetAsFirstSibling();
            AddFrame(header, Frame);

            var title = LayoutLabel(card, "Title", 430, 688, 420, 72, 38, TextAlignmentOptions.Center);
            title.text = "승리 보상";
            title.color = Paper;
            var reward = LayoutLabel(card, "Reward", 260, 638, 760, 42, 24, TextAlignmentOptions.Center);
            reward.text = "보상 집계 중";
            reward.color = Mint;
            var instruction = LayoutLabel(card, "Instruction", 64, 596, 1152, 34, 20, TextAlignmentOptions.Center);
            instruction.text = "3개의 유물 중 하나를 선택하세요";
            instruction.color = Muted;

            for (int i = 0; i < 3; i++)
            {
                var candidate = (RectTransform)card.Find("Candidate" + i);
                Place(candidate, 115 + i * 360, 135, 330, 420);
                candidate.GetComponent<Image>().color = Surface;
                AddFrame(candidate, Frame);
                var iconFrame = (RectTransform)candidate.Find("IconFrame");
                Place(iconFrame, 22, 224, 286, 158);
                iconFrame.GetComponent<Image>().color = new Color32(14, 8, 17, 255);
                AddFrame(iconFrame, Frame);
                var runeGlow = Decoration(iconFrame, "RuneGlow", Ember);
                Place(runeGlow, 107, 43, 72, 72);
                RotateAroundCenter(runeGlow, 45);
                runeGlow.SetAsFirstSibling();
                var icon = (RectTransform)iconFrame.Find("Icon");
                icon.sizeDelta = new Vector2(136, 136);
                var missing = LayoutLabel(iconFrame, "MissingIcon", 0, 0, 286, 158, 30, TextAlignmentOptions.Center);
                missing.text = "유물";
                missing.color = Muted;
                LayoutLabel(candidate, "Name", 14, 174, 302, 42, 24, TextAlignmentOptions.Center).color = Paper;
                LayoutLabel(candidate, "Rarity", 14, 132, 302, 34, 19, TextAlignmentOptions.Center);
                LayoutLabel(candidate, "Effect", 12, 24, 306, 92, 18, TextAlignmentOptions.Top).color = Paper;
                var selection = candidate.Find("Selection").GetComponent<Image>();
                selection.color = Mint;
                var selected = LayoutLabel(candidate, "Selection/Selected", 12, -28, 306, 26, 17, TextAlignmentOptions.Center);
                selected.text = "선택";
                selected.color = Mint;
            }

            ConfigureThemeDecorations(card, fallbackIcon);

            LayoutLabel(card, "Status", 64, 94, 1152, 30, 18, TextAlignmentOptions.Center).color = Muted;
            LayoutButton(card, "Confirm", 440, 18, 400, 60, true);
            LayoutButton(card, "Clear", 260, 22, 160, 52, false);
            LayoutButton(card, "PreviousPage", 32, 317, 64, 56, false);
            LayoutButton(card, "NextPage", 1184, 317, 64, 56, false);
            LayoutLabel(card, "PageCounter", 550, 566, 180, 24, 15, TextAlignmentOptions.Center).color = Muted;
        }

        private static void ConfigureThemeDecorations(RectTransform card, Sprite fallbackIcon)
        {
            var watermark = Decoration(card, "RelicWatermark", Color.white);
            Place(watermark, 1000, 48, 220, 220);
            var watermarkImage = watermark.GetComponent<Image>();
            watermarkImage.sprite = fallbackIcon;
            watermarkImage.preserveAspect = true;
            watermarkImage.color = new Color(0.48f, 0.08f, 0.18f, 0.035f);
            watermark.SetAsFirstSibling();

            var headerRelic = Decoration(card, "HeaderRelic", Color.white);
            Place(headerRelic, 439, 694, 60, 60);
            var headerImage = headerRelic.GetComponent<Image>();
            headerImage.sprite = fallbackIcon;
            headerImage.preserveAspect = true;
            headerImage.color = new Color(1, 1, 1, .72f);

            ConfigureAccent(card, "TopAccentLeft", 64, 724, 330, 2, new Color32(139, 98, 58, 190));
            ConfigureAccent(card, "TopAccentRight", 886, 724, 330, 2, new Color32(139, 98, 58, 190));
            var leftRune = ConfigureAccent(card, "TopRuneLeft", 405, 718, 12, 12, new Color32(199, 132, 62, 220));
            RotateAroundCenter(leftRune, 45);
            var rightRune = ConfigureAccent(card, "TopRuneRight", 863, 718, 12, 12, new Color32(199, 132, 62, 220));
            RotateAroundCenter(rightRune, 45);

            Color innerFrame = new Color32(139, 98, 58, 105);
            ConfigureAccent(card, "InnerFrameTop", 14, 744, 1252, 2, innerFrame);
            ConfigureAccent(card, "InnerFrameBottom", 14, 14, 1252, 2, innerFrame);
            ConfigureAccent(card, "InnerFrameLeft", 14, 14, 2, 732, innerFrame);
            ConfigureAccent(card, "InnerFrameRight", 1264, 14, 2, 732, innerFrame);
        }

        private static RectTransform ConfigureAccent(
            Transform parent,
            string name,
            float x,
            float y,
            float width,
            float height,
            Color color)
        {
            var accent = Decoration(parent, name, color);
            Place(accent, x, y, width, height);
            accent.GetComponent<Image>().color = color;
            return accent;
        }

        private static void RotateAroundCenter(RectTransform rect, float degrees)
        {
            var centerOffset = Vector2.Scale(rect.sizeDelta,
                new Vector2(0.5f - rect.pivot.x, 0.5f - rect.pivot.y));
            rect.anchoredPosition += centerOffset;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localEulerAngles = new Vector3(0, 0, degrees);
        }

        private static RectTransform Decoration(Transform parent, string name, Color color)
        {
            var decoration = parent.Find(name) as RectTransform;
            if (decoration == null)
                decoration = Box(parent, name, Vector2.zero, Vector2.zero,
                    Vector2.zero, Vector2.zero, color);
            var image = decoration.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return decoration;
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
            if (name == "Confirm")
            {
                label.text = "건너뛰기";
                label.fontSize = label.fontSizeMax = 18;
            }
            else if (name == "Clear") label.text = "선택 취소";
            else if (name == "PreviousPage")
            {
                label.text = "이전";
                label.fontSize = label.fontSizeMax = 18;
            }
            else if (name == "NextPage")
            {
                label.text = "다음";
                label.fontSize = label.fontSizeMax = 18;
            }
            var image = button.GetComponent<Image>();
            var colors = button.colors;
            image.color = Color.white;
            if (primary)
            {
                colors.normalColor = Primary;
                colors.highlightedColor = new Color32(155, 61, 70, 255);
                colors.selectedColor = colors.highlightedColor;
                colors.pressedColor = new Color32(96, 35, 45, 255);
                colors.disabledColor = new Color32(70, 53, 61, 190);
                label.color = Paper;
                AddFrame((RectTransform)button.transform, new Color32(170, 105, 79, 255));
            }
            else
            {
                colors.normalColor = new Color32(53, 40, 52, 255);
                colors.highlightedColor = new Color32(76, 55, 69, 255);
                colors.selectedColor = colors.highlightedColor;
                colors.pressedColor = new Color32(39, 29, 39, 255);
                colors.disabledColor = new Color32(45, 38, 45, 180);
                label.color = Paper;
                AddFrame((RectTransform)button.transform, Frame);
            }
            button.colors = colors;
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

        private static Sprite GetFallbackIcon()
        {
            var importer = AssetImporter.GetAtPath(FallbackIconPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Artifact fallback icon is required at " + FallbackIconPath);
            bool needsImport = importer.textureType != TextureImporterType.Sprite ||
                               importer.spriteImportMode != SpriteImportMode.Single ||
                               importer.maxTextureSize != 512 || !importer.alphaIsTransparency;
            if (needsImport)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.maxTextureSize = 512;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(FallbackIconPath) ??
                   throw new InvalidOperationException("Could not load artifact fallback icon as a Sprite.");
        }
    }
}
