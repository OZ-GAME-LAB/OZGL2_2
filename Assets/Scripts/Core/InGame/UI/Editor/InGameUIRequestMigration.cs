#if UNITY_EDITOR
using System;
using System.Linq;
using Game.Core;
using OZGL.KDH;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.UI.InGame.Editor
{
    /// <summary>배치된 화면을 유지하고 요청 View와 Bootstrap 연결만 이전합니다.</summary>
    public static class InGameUIRequestMigration
    {
        private const string Folder = "Assets/Prefabs/UI/Test/";
        private const string SettlementFrameName = "RunSettlementFrame";
        private const string SettlementContent = "Window/SummaryBox/ProgressColumn/ArtifactScrollView/Viewport/Content";

        [MenuItem("Game/UI/InGame/Connect Request UI")]
        public static void Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != InGameUIMigration.ScenePath || scene.isDirty)
                throw new InvalidOperationException("저장된 Test 씬의 Edit Mode에서 실행하세요.");

            EditPrefab("WaveReward.prefab", root => ConfigureContinue(root.GetComponent<UIScreen>()));
            EditPrefab("Message.prefab", root => ConfigureMessage(root.GetComponent<UIScreen>()));
            EditPrefab("RunResult.prefab", root => ConfigureSettlement(root.GetComponent<UIScreen>()));
            EditPrefab("InGameHUD.prefab", root =>
            {
                foreach (SettlementView old in root.GetComponentsInChildren<SettlementView>(true)) Object.DestroyImmediate(old);
            });
            EditPrefab("InGameUIRoot.prefab", ConfigureRoot);

            InGameUIManager manager = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<InGameUIManager>(true)).Single();
            ConfigureRoot(manager.gameObject);
            BootStrap bootstrap = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<BootStrap>(true)).Single();
            ConnectBootstrap(bootstrap, manager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Request UI] 기존 프리팹 및 Test 인스턴스 연결을 이전했습니다. UI 루트를 재생성하지 않았습니다.");
        }

        public static string ValidatePrefab()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "InGameUIRoot.prefab");
            ValidateRoot(root);
            return "PASS: two canvases, registered screens, request views and references.";
        }

        private static void EditPrefab(string name, Action<GameObject> edit)
        {
            string path = Folder + name;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ConfigureRoot(GameObject root)
        {
            UIScreen[] screens = root.GetComponentsInChildren<UIScreen>(true);
            ConfigureContinue(screens.Single(screen => screen.Id == UIId.WaveReward));
            MessageView message = ConfigureMessage(screens.Single(screen => screen.Id == UIId.Message));
            SettlementView settlement = ConfigureSettlement(screens.Single(screen => screen.Id == UIId.RunResult));
            foreach (SettlementView old in root.GetComponentsInChildren<SettlementView>(true))
                if (old != settlement) Object.DestroyImmediate(old);
            foreach (MonoBehaviour old in root.GetComponents<MonoBehaviour>())
                if (old != null && old.GetType().Name == "RunFlowPresenter") Object.DestroyImmediate(old);
            Set(root.GetComponent<HudPresenter>(), "_messages", message);
            ValidateRoot(root);
        }

        private static ContinueView ConfigureContinue(UIScreen screen)
        {
            ContinueView view = GetOrAdd<ContinueView>(screen.gameObject);
            Set(view, "_screen", screen);
            Set(view, "_messageText", At<TMP_Text>(screen, "WaveReward/Window/Reward"));
            Set(view, "_continueButton", At<Button>(screen, "WaveReward/Window/Continue"));
            return view;
        }

        private static MessageView ConfigureMessage(UIScreen screen)
        {
            MessageView view = GetOrAdd<MessageView>(screen.gameObject);
            Set(view, "_screen", screen);
            Set(view, "_messageText", At<TMP_Text>(screen, "Message/Window/Message"));
            return view;
        }

        private static SettlementView ConfigureSettlement(UIScreen screen)
        {
            if (screen == null) throw new InvalidOperationException("RunResult 화면이 없습니다.");
            GameObject frame = screen.transform.Find(SettlementFrameName)?.gameObject;
            bool hostWasActive = screen.gameObject.activeSelf;
            screen.gameObject.SetActive(false);
            try
            {
                // 활성 프리팹의 TMP가 원본 폰트에 글리프를 추가하기 전에 Host부터 비활성화한다.
                if (frame == null)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + SettlementFrameName + ".prefab");
                    if (asset == null) throw new InvalidOperationException("저장된 결산 프레임 프리팹이 없습니다.");
                    frame = (GameObject)PrefabUtility.InstantiatePrefab(asset, screen.transform);
                    frame.name = SettlementFrameName;
                }
                frame.SetActive(false);

                // 기존 표시 자식과 원본 에셋을 보존하며, 활성 Host 아래 표시 루트만 교체한다.
                if (screen.Root != null && screen.Root != screen.gameObject && screen.Root != frame)
                {
                    screen.Root.SetActive(false);
                    RecordOverride(screen.Root);
                }
            }
            finally
            {
                if (frame != null) frame.SetActive(false);
                screen.gameObject.SetActive(hostWasActive);
            }
            screen.gameObject.SetActive(true);
            CanvasGroup input = GetOrAdd<CanvasGroup>(frame);
            input.alpha = 1;
            input.interactable = false;
            input.blocksRaycasts = false;
            Set(screen, "_root", frame);
            Set(screen, "_inputGroup", input);
            SetBool(screen, "_canCloseByUser", false);
            SetBool(screen, "_blocksHudInput", true);

            SettlementView view = GetOrAdd<SettlementView>(screen.gameObject);
            view.enabled = true;
            Set(view, "_screen", screen);
            Set(view, "_progress", At<TMP_Text>(screen, SettlementFrameName + "/Window/SummaryBox/ProgressColumn/ProgressValue"));
            Set(view, "_artifacts", ConfigureArtifactCounts(frame));
            Set(view, "_bloodstone", At<TMP_Text>(screen, SettlementFrameName + "/Window/SummaryBox/BloodstoneReward/Value"));
            Set(view, "_mainButton", At<UnityEngine.UI.Button>(screen, SettlementFrameName + "/Window/SummaryBox/Footer/MainMenuButton"));
            RecordOverride(screen.gameObject);
            RecordOverride(frame);
            RecordOverride(input);
            RecordOverride(view);
            return view;
        }

        private static TMP_Text ConfigureArtifactCounts(GameObject frame)
        {
            Transform content = frame.transform.Find(SettlementContent) ??
                throw new InvalidOperationException("결산 유물 스크롤 Content가 없습니다.");
            Transform existing = content.Find("ArtifactCounts");
            if (existing != null)
                return existing.GetComponent<TMP_Text>() ??
                    throw new InvalidOperationException("ArtifactCounts에 TMP 텍스트가 없습니다.");

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Data/UI/Player/PlayerUIFont.asset");
            if (font == null) throw new InvalidOperationException("기존 PlayerUIFont를 찾을 수 없습니다.");
            var label = new GameObject("ArtifactCounts", typeof(RectTransform));
            label.SetActive(false);
            label.transform.SetParent(content, false);
            TMP_Text text = label.AddComponent<TextMeshProUGUI>();
            var layout = label.AddComponent<UnityEngine.UI.LayoutElement>();
            var rect = label.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0, 120);
            text.font = font;
            text.fontSize = 18;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.color = new Color32(48, 48, 48, 255);
            text.raycastTarget = false;
            text.lineSpacing = 8;
            text.text = "일반 0개\n희귀 0개\n전설 0개\n신화 0개";
            layout.preferredHeight = 120;
            layout.minHeight = 120;
            layout.flexibleWidth = 1;
            label.SetActive(true); // 표시 프레임은 계속 비활성이므로 원본 폰트의 글리프를 생성하지 않는다.
            return text;
        }

        private static void ConnectBootstrap(BootStrap bootstrap, InGameUIManager manager)
        {
            GameObject root = manager.gameObject;
            InGameUIStartup startup = GetOrAdd<InGameUIStartup>(bootstrap.gameObject);
            Set(startup, "_uiManager", manager);
            Set(startup, "_hudView", root.GetComponentInChildren<GameHudView>(true));
            Set(startup, "_hudPresenter", root.GetComponent<HudPresenter>());
            Set(startup, "_artifactSelectionUI", root.GetComponent<ArtifactRewardPresenter>());
            Set(startup, "_continueUI", root.GetComponentInChildren<ContinueView>(true));
            Set(startup, "_runDecisionUI", root.GetComponentInChildren<RunDecisionView>(true));
            Set(startup, "_settlementUI", root.GetComponentInChildren<SettlementView>(true));
            Set(startup, "_shopView", root.GetComponentInChildren<ShopView>(true));
            Set(startup, "_buildingUI", root.GetComponent<BuildingUIPresenter>());
            Set(startup, "_buildingUIConnection", GetOrAdd<BuildingUIConnection>(bootstrap.gameObject));
            Set(bootstrap, "_uiStartup", startup);
            var data = new SerializedObject(startup);
            var field = data.FindProperty("_buildingShortcuts");
            BuildingShortcut[] shortcuts = root.GetComponentsInChildren<BuildingShortcut>(true);
            field.arraySize = shortcuts.Length;
            for (int i = 0; i < shortcuts.Length; i++) field.GetArrayElementAtIndex(i).objectReferenceValue = shortcuts[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            RecordOverride(startup);
        }

        private static void ValidateRoot(GameObject root)
        {
            if (root == null || root.GetComponentsInChildren<Canvas>(true).Length != 2 ||
                root.GetComponentsInChildren<GraphicRaycaster>(true).Length != 2)
                throw new InvalidOperationException("HUD와 Popup Canvas 두 개 및 각각의 Raycaster가 필요합니다.");
            var data = new SerializedObject(root.GetComponent<InGameUIManager>());
            SerializedProperty registered = data.FindProperty("_screenInstances");
            UIScreen[] screens = root.GetComponentsInChildren<UIScreen>(true);
            if (registered.arraySize != screens.Length || screens.Select(screen => screen.Id).Distinct().Count() != screens.Length)
                throw new InvalidOperationException("화면 등록 개수 또는 ID가 올바르지 않습니다.");
            for (int i = 0; i < registered.arraySize; i++)
                if (!(registered.GetArrayElementAtIndex(i).objectReferenceValue is UIScreen screen) || !screens.Contains(screen))
                    throw new InvalidOperationException("화면 등록 참조가 끊겼습니다.");
            foreach (UIScreen screen in screens)
                if (screen.Root == null || screen.Root == screen.gameObject || screen.GetComponentsInChildren<Canvas>(true).Length != 0)
                    throw new InvalidOperationException("화면 표시 루트 또는 Canvas 구조가 올바르지 않습니다.");
            foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (component == null) throw new InvalidOperationException("Missing Script가 있습니다.");
            UIScreen settlement = screens.Single(screen => screen.Id == UIId.RunResult);
            if (settlement.Root.name != SettlementFrameName || !settlement.gameObject.activeSelf ||
                settlement.CanCloseByUser || !settlement.BlocksHudInput)
                throw new InvalidOperationException("결산 프레임 또는 필수 확인 화면 설정이 올바르지 않습니다.");
            var settlementData = new SerializedObject(settlement.GetComponent<SettlementView>());
            foreach (string field in new[] { "_screen", "_progress", "_artifacts", "_bloodstone", "_mainButton" })
                if (settlementData.FindProperty(field)?.objectReferenceValue == null)
                    throw new InvalidOperationException("결산 표시 참조가 없습니다: " + field);
        }

        private static T GetOrAdd<T>(GameObject host) where T : Component
        {
            T component = host.GetComponent<T>();
            return component != null ? component : host.AddComponent<T>();
        }
        private static T At<T>(UIScreen screen, string path) where T : Component =>
            screen.transform.Find(path)?.GetComponent<T>() ?? throw new InvalidOperationException("표시 참조가 없습니다: " + path);
        private static void Set(Object target, string field, Object value)
        {
            if (target == null || value == null) throw new InvalidOperationException("연결 참조가 없습니다: " + field);
            var data = new SerializedObject(target);
            var property = data.FindProperty(field) ?? throw new InvalidOperationException("필드가 없습니다: " + field);
            property.objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        private static void SetBool(Object target, string field, bool value)
        {
            var data = new SerializedObject(target);
            var property = data.FindProperty(field) ?? throw new InvalidOperationException("필드가 없습니다: " + field);
            property.boolValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        private static void RecordOverride(Object target)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }
}
#endif
