using System;
using System.IO;
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

namespace Game.UI.Editor
{
    /// <summary>최초 생성용 도구. 이미 만들어진 팀원의 에셋은 덮어쓰지 않는다.</summary>
    public static class MvpHudBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/UI/MvpCommonHud.prefab";
        public const string ScenePath = "Assets/Scenes/Test/MvpHudTest.unity";
        public const string FontPath = "Assets/Art/Fonts/NotoSansKR/NotoSansKR SDF.asset";

        private static readonly Color Ink = new Color32(15, 22, 31, 255);
        private static readonly Color Panel = new Color32(25, 36, 48, 250);
        private static readonly Color Mint = new Color32(121, 229, 195, 255);
        private static readonly Color Paper = new Color32(233, 241, 244, 255);
        private static readonly Color Muted = new Color32(145, 169, 182, 255);
        private static TMP_FontAsset _font;

        [MenuItem("Game/UI/Create Missing MVP HUD Assets")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
            {
                AssetDatabase.importPackageCompleted -= HandleEssentialsImported;
                AssetDatabase.importPackageCompleted += HandleEssentialsImported;
                AssetDatabase.importPackageFailed -= HandleImportFailed;
                AssetDatabase.importPackageFailed += HandleImportFailed;
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly);
                AssetDatabase.ImportPackage(Path.Combine(package.resolvedPath,
                    "Package Resources/TMP Essential Resources.unitypackage"), false);
                return;
            }

            try
            {
                EnsureFont();
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) CreatePrefab();
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) CreateTestScene();
                AssetDatabase.SaveAssets();
                Debug.Log("[UI/MvpHudBuilder] HUD prefab and isolated test scene are ready.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        private static void HandleEssentialsImported(string packageName)
        {
            AssetDatabase.importPackageCompleted -= HandleEssentialsImported;
            AssetDatabase.importPackageFailed -= HandleImportFailed;
            EditorApplication.delayCall += Build;
        }

        private static void HandleImportFailed(string packageName, string error)
        {
            AssetDatabase.importPackageCompleted -= HandleEssentialsImported;
            AssetDatabase.importPackageFailed -= HandleImportFailed;
            Debug.LogError("[UI/MvpHudBuilder] TMP resource import failed: " + error);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }

        internal static void EnsureFont()
        {
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (_font != null) return;
            var source = AssetDatabase.LoadAssetAtPath<Font>(
                "Assets/Art/Fonts/NotoSansKR/NotoSansCJKkr-Regular.otf");
            if (source == null) throw new InvalidOperationException("Noto Sans Korean source font is missing.");
            _font = TMP_FontAsset.CreateFontAsset(source, 64, 8, GlyphRenderMode.SDFAA,
                2048, 2048, AtlasPopulationMode.Dynamic, true);
            _font.name = "NotoSansKR SDF";
            AssetDatabase.CreateAsset(_font, FontPath);
            foreach (var atlas in _font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, _font);
            AssetDatabase.AddObjectToAsset(_font.material, _font);
            const string copy = " !+,-./:0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
                "·마계관문골드웨이브진행상태건설전투준비보결과승리패배다시작계속하기" +
                "연결대기획득요청거절되었습니도세요공통테스트표확인용임데이터실제게임로직없음" +
                "다음처중복입력잠금아래버튼으로전달보상담시스템역할입니다";
            if (!_font.TryAddCharacters(copy, out var missing))
                throw new InvalidOperationException("Missing Korean font characters: " + missing);
            EditorUtility.SetDirty(_font);
            AssetDatabase.SaveAssets();
        }

        private static void CreatePrefab()
        {
            var root = CreateCanvas("MvpCommonHud", 10);
            root.SetActive(false);
            try
            {
                var ui = root.AddComponent<GameUIController>();
                var top = Box(root.transform, "TopBar", new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(24, -158), new Vector2(-24, -24), Panel);
                Box(top, "Accent", Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(4, 0), Mint);
                Label(top, "GameTitle", "D(eamon)nD(efense)", 28, Paper, 32, 69, 490, 47);
                Label(top, "Subtitle", "마계 관문", 20, Muted, 32, 27, 420, 32);
                var phase = Label(top, "PhaseValue", "연결 대기", 30, Mint, 0, 22, 210, 48);
                AnchorCentered(phase.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-95, -12));
                var phaseTitle = Label(top, "PhaseTitle", "진행 상태", 16, Muted, 0, 0, 210, 28);
                AnchorCentered(phaseTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-95, 30));
                var wave = Label(top, "WaveValue", "-- / --", 32, Paper, 0, 0, 160, 48);
                AnchorCentered(wave.rectTransform, new Vector2(0.69f, 0.5f), new Vector2(0, -12));
                var waveTitle = Label(top, "WaveTitle", "웨이브", 16, Muted, 0, 0, 160, 28);
                AnchorCentered(waveTitle.rectTransform, new Vector2(0.69f, 0.5f), new Vector2(0, 30));
                var gold = Label(top, "GoldValue", "--", 32, new Color32(247, 203, 115, 255), 0, 0, 310, 48);
                AnchorCentered(gold.rectTransform, new Vector2(1, 0.5f), new Vector2(-183, -12));
                gold.alignment = TextAlignmentOptions.Right;
                var goldTitle = Label(top, "GoldTitle", "골드", 16, Muted, 0, 0, 310, 28);
                AnchorCentered(goldTitle.rectTransform, new Vector2(1, 0.5f), new Vector2(-183, 30));
                goldTitle.alignment = TextAlignmentOptions.Right;

                var start = Button(root.transform, "WaveStartButton", "웨이브 시작", 300, 72);
                AnchorCentered((RectTransform)start.transform, new Vector2(1, 0), new Vector2(-190, 72));
                start.interactable = false;
                var hint = Label(root.transform, "InputHint", "", 18, Muted, 0, 0, 300, 24);
                hint.gameObject.SetActive(false);

                var reward = Modal(root.transform, "WaveRewardPanel", "웨이브 승리", out _,
                    out var rewardText, out var next, "계속하기");
                var result = Modal(root.transform, "RunResultPanel", "승리", out var resultTitle,
                    out var resultText, out var restart, "다시 시작");
                var message = Box(root.transform, "MessagePanel", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(-450, -225), new Vector2(450, -170), Panel);
                var messageText = Label(message, "MessageText", "", 22, Paper, 20, 0, 860, 55);
                messageText.alignment = TextAlignmentOptions.Center;
                message.GetComponent<Image>().raycastTarget = false;

                Assign(ui, "_goldText", gold, "_waveText", wave, "_phaseText", phase,
                    "_waveStartButton", start, "_waveRewardPanel", reward.gameObject,
                    "_waveRewardText", rewardText, "_continueButton", next,
                    "_runResultPanel", result.gameObject, "_runResultTitleText", resultTitle,
                    "_runRewardText", resultText, "_restartButton", restart,
                    "_messagePanel", message.gameObject, "_messageText", messageText);
                reward.gameObject.SetActive(false);
                result.gameObject.SetActive(false);
                message.gameObject.SetActive(false);
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void CreateTestScene()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var camera = new GameObject("HUD Test Camera", typeof(Camera)).GetComponent<Camera>();
                camera.tag = "MainCamera";
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Ink;
                camera.orthographic = true;
                camera.transform.position = new Vector3(0, 0, -10);
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var ui = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<GameUIController>();
                var demo = CreateCanvas("HUD Test Controls - NOT GAMEPLAY", 0);
                demo.SetActive(false);
                var panel = Box(demo.transform, "TestCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-490, -190), new Vector2(490, 160), Panel);
                Label(panel, "Eyebrow", "UI / MVP     ·     TEST SCENE", 18, Mint, 36, 288, 900, 28);
                Label(panel, "Title", "공통 HUD 테스트", 40, Paper, 36, 217, 900, 62);
                Label(panel, "Description", "표시 확인용 임시 데이터 · 실제 게임 로직 없음", 22, Muted, 36, 164, 910, 40);
                var status = Label(panel, "Status", "", 20, Paper, 36, 108, 910, 45);
                var add = Button(panel, "AddGold", "골드 +50", 205, 56);
                Place((RectTransform)add.transform, 36, 32, 205, 56);
                var win = Button(panel, "Win", "전투 승리", 205, 56);
                Place((RectTransform)win.transform, 271, 32, 205, 56);
                var lose = Button(panel, "Lose", "전투 패배", 205, 56);
                Place((RectTransform)lose.transform, 506, 32, 205, 56);
                var reject = Button(panel, "RejectNextStart", "다음 시작 거절", 205, 56);
                Place((RectTransform)reject.transform, 741, 32, 205, 56);
                var sample = demo.AddComponent<MvpHudSample>();
                Assign(sample, "_ui", ui, "_addGoldButton", add, "_winButton", win,
                    "_loseButton", lose, "_rejectButton", reject, "_statusText", status);
                demo.SetActive(true);
                EditorSceneManager.SaveScene(scene, ScenePath);
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

        private static RectTransform Modal(Transform parent, string name, string title,
            out TMP_Text titleText, out TMP_Text valueText, out Button action, string actionText)
        {
            var overlay = Box(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.015f, 0.025f, 0.035f, 0.86f));
            var card = Box(overlay, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-320, -210), new Vector2(320, 210), Panel);
            Box(card, "Accent", new Vector2(0, 1), Vector2.one, new Vector2(0, -4), Vector2.zero, Mint);
            titleText = Label(card, "Title", title, 44, Paper, 40, 268, 560, 76);
            titleText.alignment = TextAlignmentOptions.Center;
            valueText = Label(card, "Reward", "골드 +0", 30, Mint, 40, 175, 560, 55);
            valueText.alignment = TextAlignmentOptions.Center;
            action = Button(card, "ActionButton", actionText, 300, 68);
            Place((RectTransform)action.transform, 170, 56, 300, 68);
            return overlay;
        }

        internal static GameObject CreateCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return go;
        }

        internal static RectTransform Box(Transform parent, string name, Vector2 min, Vector2 max,
            Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            go.GetComponent<Image>().color = color;
            return rect;
        }

        internal static TextMeshProUGUI Label(Transform parent, string name, string text, float size,
            Color color, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = _font;
            label.fontSize = size;
            label.color = color;
            label.text = text;
            label.richText = false;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            Place(label.rectTransform, x, y, width, height);
            return label;
        }

        internal static Button Button(Transform parent, string name, string text, float width, float height)
        {
            var rect = Box(parent, name, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(width, height), Mint);
            var button = rect.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.88f, 1f, 0.95f);
            colors.pressedColor = new Color(0.65f, 0.85f, 0.75f);
            colors.disabledColor = new Color(0.3f, 0.4f, 0.4f, 0.5f);
            button.colors = colors;
            button.targetGraphic = rect.GetComponent<Image>();
            var label = Label(rect, "Label", text, 24, Ink, 10, 0, width - 20, height);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        internal static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void AnchorCentered(RectTransform rect, Vector2 anchor, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
        }

        internal static void Assign(UnityEngine.Object target, params object[] pairs)
        {
            var serialized = new SerializedObject(target);
            for (var i = 0; i < pairs.Length; i += 2)
            {
                var field = serialized.FindProperty((string)pairs[i]);
                if (field == null) throw new InvalidOperationException("Missing field: " + pairs[i]);
                field.objectReferenceValue = (UnityEngine.Object)pairs[i + 1];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
