using System;
using System.IO;
using System.Linq;
using Game.Core;
using Game.UI.Samples;
using OZGL.KDH;
using TMPro;
using Units;
using Units.UnitDatas;
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
    /// <summary>새 UI 프리뷰 씬만 생성한다. 기존 씬/프리팹/팀 데이터/빌드 설정은 저장하지 않는다.</summary>
    public static partial class PlayerUiBuilder
    {
        public const string ScenePath = "Assets/Scenes/UI/PlayerUI.unity";
        public const string FontPath = "Assets/Data/UI/Player/PlayerUIFont.asset";
        public const string UnitPath = "Assets/Data/UI/Player/PreviewWarrior.asset";
        private const string CoreFixtureDataPath = "Assets/Tests/KDH/Building/Test_Building_core1.asset";
        private static readonly Color Ink = new Color32(19, 24, 28, 255);
        private static readonly Color Panel = new Color32(29, 36, 40, 255);
        private static readonly Color Tile = new Color32(39, 48, 51, 255);
        private static readonly Color Paper = new Color32(235, 230, 211, 255);
        private static readonly Color Muted = new Color32(156, 171, 166, 255);
        private static readonly Color Gold = new Color32(222, 184, 105, 255);
        private static readonly Color Mint = new Color32(117, 203, 174, 255);
        private static TMP_FontAsset _font;

        [MenuItem("Game/UI/Create Missing Player UI Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (File.Exists(ScenePath)) { Debug.Log("[UI/PlayerUiBuilder] Existing player scene preserved."); return; }
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                Folder("Assets/Scenes/UI"); Folder("Assets/Data/UI/Player"); Folder("Assets/Prefabs/UI/Player");
                _font = GetFont();
                var camera = new GameObject("Player UI Camera", typeof(Camera)).GetComponent<Camera>();
                camera.tag = "MainCamera"; camera.orthographic = true;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Ink;
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule))
                    .GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                var systemRoot = new GameObject("UI Preview Systems - test combat only");
                systemRoot.SetActive(false);
                var currency = systemRoot.AddComponent<RunCurrencyManager>();
                var flow = systemRoot.AddComponent<GameFlowController>();
                var waves = systemRoot.AddComponent<WaveController>();
                var gate = systemRoot.AddComponent<TestWaitingScript>();
                var effects = systemRoot.AddComponent<EffectManager>();
                var artifacts = systemRoot.AddComponent<ArtifactManager>();
                var coreProgress = systemRoot.AddComponent<BuildingCoreProgress>();
                var runtimeCombat = systemRoot.AddComponent<MvpRuntimeCombatFixture>();
                CreateCoreFixture(systemRoot.transform, coreProgress, runtimeCombat);
                var artifactBinding = systemRoot.AddComponent<ArtifactRewardBinding>();
                var catalogAsset = AssetDatabase.LoadAssetAtPath<CurrencyCatalog>("Assets/Data/Economy/S.O/CurrencyCatalog.asset");
                if (catalogAsset == null || !catalogAsset.TryGetByType(CurrencyType.Gold, out var gold))
                    throw new InvalidOperationException("Registered Gold is required.");
                Assign(currency, "_currencyCatalog", catalogAsset, "_waveRewardTable", MvpEconomyUiSetup.LoadRewardTable());
                var sf = new SerializedObject(currency); var starting = sf.FindProperty("_baseStartingCurrencies");
                starting.arraySize = 1;
                starting.GetArrayElementAtIndex(0).FindPropertyRelative("_currency").objectReferenceValue = gold;
                starting.GetArrayElementAtIndex(0).FindPropertyRelative("_amount").intValue = 100;
                sf.ApplyModifiedPropertiesWithoutUndo();
                Assign(waves, "_waveCatalog", AssetDatabase.LoadAssetAtPath<WaveSODictionary>(MvpCoreIntegrationBuilder.CatalogPath));
                Assign(artifacts, "_artifactCatalog", AssetDatabase.LoadAssetAtPath<ArtifactCatalog>(MvpVictoryRewardSetup.CatalogPath),
                    "_rewardTable", AssetDatabase.LoadAssetAtPath<ArtifactRewardTable>(MvpVictoryRewardSetup.ArtifactTablePath));

                var hud = CreateHud(flow, waves, currency, out var quarter, out var hint, out var buildButton,
                    out var counts, out var resultRoot, out var decisionRoot);
                var board = CreateBoard(out var plots, out var campButton, out var unitButton);
                var catalog = CreateCatalog();
                var building = CreateBuilding(out var actions);
                var unit = CreateUnitInfo();
                var reward = CreateReward();
                Assign(artifactBinding, "_panel", reward);
                var navigation = hud.gameObject.AddComponent<PlayerUiNavigation>();
                Assign(navigation, "_flow", flow, "_catalog", catalog);
                Array(navigation, "_popups", new UnityEngine.Object[] { catalog.Popup, Ref<PlayerPopup>(building, "_playerPopup"), Ref<PlayerPopup>(unit, "_playerPopup") });
                Array(navigation, "_catalogButtons", plots.Cast<UnityEngine.Object>().Concat(new UnityEngine.Object[] { buildButton }).ToArray());
                Array(navigation, "_blockingPanels", new UnityEngine.Object[] { Ref<GameObject>(reward, "_panelRoot"), resultRoot, decisionRoot,
                    Ref<GameObject>(hud, "_waveRewardPanel"), Ref<GameObject>(hud, "_messagePanel") });

                var unitObject = new GameObject("Preview Warrior - selection only"); unitObject.SetActive(false);
                var runtime = unitObject.AddComponent<Unit_RuntimeStatus>();
                var data = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
                if (data == null)
                {
                    data = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/UI/Tests/RuntimeSelectionAlly.asset"));
                    data.name = "PreviewWarrior";
                    var uf = new SerializedObject(data); uf.FindProperty("_unitName").stringValue = "전사"; uf.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.CreateAsset(data, UnitPath);
                }
                Assign(runtime, "_unitData", data);
                unitObject.AddComponent<Unit_Life>();
                var core = unitObject.AddComponent<Unit_Core>();
                var source = unitObject.AddComponent<RuntimeUnitInfoSource>();
                Value(source, "_roleLabel", "전열 · 근거리");
                Value(source, "_description", "공격과 방어가 균형 잡힌 마계의 전사입니다.");
                var unitBinding = unit.gameObject.AddComponent<RuntimeUnitInfoBinding>(); Assign(unitBinding, "_panel", unit);
                var preview = systemRoot.AddComponent<PlayerUiPreviewBindings>();
                Assign(preview, "_catalog", catalog, "_buildingInfo", building, "_actions", actions, "_unitInfo", unit,
                    "_unitBinding", unitBinding, "_unitSource", source, "_unit", core, "_counts", counts,
                    "_unitButton", unitButton, "_campButton", campButton, "_hint", hint, "_flow", flow);
                // Test inputs remain available to automated checks but are never shown to the player.
                var hidden = CanvasRoot("DeveloperOnly", -100); hidden.transform.SetParent(systemRoot.transform, false);
                var sample = systemRoot.AddComponent<MvpRuntimeHudSample>();
                Assign(sample, "_ui", hud, "_currencyManager", currency, "_gold", gold, "_flow", flow, "_waves", waves,
                    "_buildingCoreProgress", coreProgress, "_runtimeCombat", runtimeCombat,
                    "_rewardGate", gate, "_coreBinding", hud.GetComponent<CoreHudBinding>(), "_goldBinding", hud.GetComponent<RunGoldHudBinding>(),
                    "_runDecisionBinding", quarter, "_artifactRewards", artifactBinding, "_artifactManager", artifacts, "_effectManager", effects,
                    "_statusText", Text(hidden.transform, "DiagnosticStatus", "", 0, 0, 800, 40));
                foreach (var key in new[] { "_addGoldButton", "_spendGoldButton", "_rejectSpendButton", "_winButton", "_rewardButton", "_loseButton",
                    "_resetButton", "_toggleHudButton", "_lastWaveButton", "_lastQuarterButton" })
                    Assign(sample, key, Btn(hidden.transform, key, key, 0, 0, 120, 40));
                hidden.SetActive(false);
                unitObject.SetActive(true); systemRoot.SetActive(true);
                ApplyWireframe(scene);
                UiCoreCameraRigSetup.Ensure(scene, flow, camera);
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var label in root.GetComponentsInChildren<TMP_Text>(true)) label.font = _font;
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Player UI scene save failed.");
                AssetDatabase.SaveAssetIfDirty(_font);
                Debug.Log("[UI/PlayerUiBuilder] Created " + ScenePath + "; existing scenes and team assets preserved.");
            }
            finally
            {
                if (!Application.isBatchMode && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static Building CreateCoreFixture(Transform parent, BuildingCoreProgress coreProgress,
            MvpRuntimeCombatFixture runtimeCombat)
        {
            var coreData = AssetDatabase.LoadAssetAtPath<BuildingData>(CoreFixtureDataPath);
            if (coreData == null)
                throw new InvalidOperationException("UI core fixture data is required: " + CoreFixtureDataPath);

            var coreObject = new GameObject("UI Core Level Fixture");
            coreObject.transform.SetParent(parent, false);
            var coreFixture = coreObject.AddComponent<Building>();
            Assign(coreFixture, "data", coreData);
            coreFixture.enabled = false;
            Assign(runtimeCombat, "_coreProgress", coreProgress, "_coreFixture", coreFixture);
            return coreFixture;
        }

        private static TMP_FontAsset GetFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) return font;
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/NotoSansKR/NotoSansCJKkr-Regular.otf");
            font = TMP_FontAsset.CreateFontAsset(source, 64, 8, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            font.name = "PlayerUIFont"; AssetDatabase.CreateAsset(font, FontPath);
            foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
            AssetDatabase.AddObjectToAsset(font.material, font);
            return font;
        }

        private static void SaveView(GameObject root, string filename)
        {
            string path = "Assets/Prefabs/UI/Player/" + filename + ".prefab";
            bool isolatedValidation = Application.isBatchMode && Path.GetFullPath(Application.dataPath).Replace('\\', '/').Contains("/UnityUIValidation/");
            if (!File.Exists(path) || isolatedValidation) PrefabUtility.SaveAsPrefabAsset(root, path);
        }

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = path.Substring(0, path.LastIndexOf('/')); Folder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }

        private static GameObject CanvasRoot(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.SetActive(false);
            var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            return go;
        }

        private static RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; Top(rt, x, y, w, h); go.GetComponent<Image>().color = color;
            return rt;
        }

        private static void Top(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static TMP_Text Text(Transform parent, string name, string copy, float x, float y, float w, float h,
            float size = 22, Color? color = null, bool wrap = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var label = go.GetComponent<TMP_Text>(); label.font = _font; label.text = copy; label.fontSize = Mathf.Min(size, h / 1.5f);
            label.color = color ?? Paper; label.raycastTarget = false; label.richText = false;
            label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis; label.alignment = TextAlignmentOptions.MidlineLeft;
            Top((RectTransform)go.transform, x, y, w, h); return label;
        }

        private static Button Btn(Transform parent, string name, string copy, float x, float y, float w, float h, bool primary = false)
        {
            var rect = Box(parent, name, x, y, w, h, primary ? Gold : Tile);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            var colors = button.colors; colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f); colors.selectedColor = new Color(1.16f, 1.16f, 1.16f);
            colors.pressedColor = new Color(.8f, .8f, .8f); colors.disabledColor = new Color(.55f, .55f, .55f); button.colors = colors;
            Text(rect, "Label", copy, 10, 0, w - 20, h, 22, primary ? Ink : Paper).alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static PlayerUiIcon Icon(Transform parent, string name, PlayerUiIcon.Symbol symbol, float x, float y, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(PlayerUiIcon)); go.transform.SetParent(parent, false);
            Top((RectTransform)go.transform, x, y, size, size);
            var icon = go.GetComponent<PlayerUiIcon>(); icon.Kind = symbol; icon.color = color; icon.raycastTarget = false; return icon;
        }

        private static RectTransform Overlay(GameObject owner, string name, float w, float h, out GameObject root)
        {
            var shade = Box(owner.transform, name, 0, 0, 0, 0, new Color(0.015f, .022f, .025f, .62f)); Stretch(shade);
            root = shade.gameObject;
            var card = Box(shade, "Window", 0, 0, w, h, Panel);
            card.anchorMin = card.anchorMax = new Vector2(.5f, .5f); card.pivot = new Vector2(.5f, .5f); card.anchoredPosition = Vector2.zero;
            Box(card, "TopAccent", 0, 0, w, 3, Gold).GetComponent<Image>().raycastTarget = false;
            return card;
        }

        private static PlayerPopup Popup(GameObject owner, string title, float w, float h, out RectTransform card)
        {
            card = Overlay(owner, "PopupOverlay", w, h, out var root);
            var backdrop = root.AddComponent<Button>(); backdrop.transition = Selectable.Transition.None;
            var close = Btn(card, "Close", "", w - 60, 16, 40, 40);
            Icon(close.transform, "CloseIcon", PlayerUiIcon.Symbol.Close, 6, 6, 28, Paper);
            Text(card, "PopupTitle", title, 24, 18, w - 96, 38, 27);
            var popup = owner.AddComponent<PlayerPopup>();
            Assign(popup, "_root", root, "_window", card, "_initialFocus", close);
            Array(popup, "_closeButtons", new UnityEngine.Object[] { close, backdrop });
            Value(popup, "_collapsedHeight", h); Value(popup, "_expandedHeight", h);
            root.SetActive(false); return popup;
        }

        internal static T Ref<T>(UnityEngine.Object target, string field) where T : UnityEngine.Object =>
            new SerializedObject(target).FindProperty(field).objectReferenceValue as T;
        private static void Assign(UnityEngine.Object target, params object[] pairs) => MvpHudBuilder.Assign(target, pairs);
        private static void Array(UnityEngine.Object target, string field, UnityEngine.Object[] values)
        {
            var so = new SerializedObject(target); var p = so.FindProperty(field); p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Value(UnityEngine.Object target, string field, object value)
        {
            var so = new SerializedObject(target); var p = so.FindProperty(field);
            if (value is bool b) p.boolValue = b; else if (value is float f) p.floatValue = f; else p.stringValue = (string)value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
