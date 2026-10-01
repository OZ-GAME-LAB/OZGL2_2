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
            SettlementView view = GetOrAdd<SettlementView>(screen.gameObject);
            Set(view, "_screen", screen);
            Set(view, "_root", screen.Root);
            Set(view, "_title", At<TMP_Text>(screen, "RunResult/Window/Title"));
            Set(view, "_totems", At<TMP_Text>(screen, "RunResult/Window/TotemColumn/ScrollableContent/Totems"));
            Set(view, "_progress", At<TMP_Text>(screen, "RunResult/Window/ProgressColumn/ScrollableContent/Progress"));
            Set(view, "_artifacts", At<TMP_Text>(screen, "RunResult/Window/ProgressColumn/ScrollableContent/Artifacts"));
            Set(view, "_score", At<TMP_Text>(screen, "RunResult/Window/ScoreColumn/Score"));
            Set(view, "_bloodstone", At<TMP_Text>(screen, "RunResult/Window/Bloodstones"));
            Set(view, "_gaugeText", At<TMP_Text>(screen, "RunResult/Window/BloodstoneGauge"));
            Set(view, "_gaugeFill", At<RectTransform>(screen, "RunResult/Window/BloodstoneTrack/Fill"));
            Set(view, "_notice", At<TMP_Text>(screen, "RunResult/Window/SettlementNotice"));
            Set(view, "_mainButton", At<Button>(screen, "RunResult/Window/MainMenu"));
            return view;
        }

        private static void ConnectBootstrap(BootStrap bootstrap, InGameUIManager manager)
        {
            GameObject root = manager.gameObject;
            Set(bootstrap, "_inGameUIManager", manager);
            Set(bootstrap, "_hudView", root.GetComponentInChildren<GameHudView>(true));
            Set(bootstrap, "_hudPresenter", root.GetComponent<HudPresenter>());
            Set(bootstrap, "_artifactSelectionUI", root.GetComponent<ArtifactRewardPresenter>());
            Set(bootstrap, "_continueUI", root.GetComponentInChildren<ContinueView>(true));
            Set(bootstrap, "_runDecisionUI", root.GetComponentInChildren<RunDecisionView>(true));
            Set(bootstrap, "_settlementUI", root.GetComponentInChildren<SettlementView>(true));
            Set(bootstrap, "_buildingUI", root.GetComponent<BuildingUIPresenter>());
            Set(bootstrap, "_buildingUIConnection", GetOrAdd<BuildingUIConnection>(bootstrap.gameObject));
            var data = new SerializedObject(bootstrap);
            var field = data.FindProperty("_buildingShortcuts");
            BuildingShortcut[] shortcuts = root.GetComponentsInChildren<BuildingShortcut>(true);
            field.arraySize = shortcuts.Length;
            for (int i = 0; i < shortcuts.Length; i++) field.GetArrayElementAtIndex(i).objectReferenceValue = shortcuts[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(bootstrap);
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
        }

        private static T GetOrAdd<T>(GameObject host) where T : Component => host.GetComponent<T>() ?? host.AddComponent<T>();
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
    }
}
#endif
