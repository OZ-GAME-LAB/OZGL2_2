using System;
using System.IO;
using System.Linq;
using Game.Core;
using OZGL.KDH;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI.Editor
{
    /// <summary>팀 씬을 새 GUID로 복사하고 플레이어 UI만 연결한다. 원본 씬/에셋은 저장하지 않는다.</summary>
    public static class TeamBuildingUiSetup
    {
        public const string SourceScenePath = "Assets/Scenes/Test/Test_Building.unity";
        public const string ScenePath = "Assets/Scenes/UI/PlayerTeamBuildingIntegration.unity";

        [MenuItem("Game/UI/Create Team Building Integration Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (File.Exists(ScenePath)) { Debug.Log("[UI] Existing team UI scene preserved."); return; }
            if (!File.Exists(SourceScenePath) || !File.Exists(RuntimeBuildingWorldUiSetup.ScenePath))
                throw new InvalidOperationException("Team scene dependencies and existing player UI scene are required.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var loaded = SceneManager.GetSceneAt(i);
                if (loaded.isDirty) throw new InvalidOperationException("Save pending scene edits yourself first.");
                if (loaded.path == RuntimeBuildingWorldUiSetup.ScenePath)
                    throw new InvalidOperationException("Close the UI template scene before generating a new copy.");
            }
            var previous = SceneManager.GetActiveScene();
            if (!AssetDatabase.CopyAsset(SourceScenePath, ScenePath)) throw new IOException("Team scene copy failed.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var template = default(Scene);
            try
            {
                SceneManager.SetActiveScene(scene);
                var teamRoots = scene.GetRootGameObjects();
                T Team<T>() where T : Component => teamRoots.SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();
                var flow = Team<GameFlowController>();
                var waves = Team<WaveController>();
                var wallet = Team<RunCurrencyManager>();
                var controller = Team<BuildingBuildController>();
                var database = Ref<BuildingDatabase>(controller, "database");
                if (database == null) throw new InvalidOperationException("Team building database is missing.");
                var camera = Team<Camera>();
                var slots = teamRoots.SelectMany(r => r.GetComponentsInChildren<BuildingSlot>(true))
                    .Where(slot =>
                    {
                        var preplaced = slot.GetComponentInChildren<Building>(true);
                        return preplaced == null || preplaced.Data == null || !preplaced.Data.IsCore;
                    })
                    .OrderBy(slot => slot.name).ToArray();
                if (slots.Length == 0) throw new InvalidOperationException("No team slots.");
                // 테스트 진행 로직은 보존한다. 복사본에서 테스트 화면/레이캐스트만 숨긴다.
                foreach (var canvas in teamRoots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true))) canvas.enabled = false;
                foreach (var raycaster in teamRoots.SelectMany(r => r.GetComponentsInChildren<GraphicRaycaster>(true))) raycaster.enabled = false;
                foreach (var panel in teamRoots.SelectMany(r => r.GetComponentsInChildren<CurrencyTestPanel>(true))) panel.enabled = false;
                Team<EventSystem>().SetSelectedGameObject(null);
                camera.tag = "MainCamera";
                if (camera.GetComponent<Physics2DRaycaster>() == null) camera.gameObject.AddComponent<Physics2DRaycaster>();
                Assign(controller, "worldCamera", camera, "wallet", wallet, "gameFlow", flow);
                SetInt(controller, "slotMask", 0); // 원본 임시 클릭 메뉴와 플레이어 팝업의 이중 입력 방지.

                template = EditorSceneManager.OpenScene(RuntimeBuildingWorldUiSetup.ScenePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var selected = template.GetRootGameObjects().Where(r => r.name == "Player HUD" ||
                    r.name == "Player Building Catalog" || r.name == "Player Building Info").ToArray();
                if (selected.Length != 3) throw new InvalidOperationException("Player UI template roots changed.");
                var owner = new GameObject("Team Building Player UI"); owner.SetActive(false);
                // 이동한 메모리 객체만 사용한다. 템플릿 씬은 저장하지 않고 닫는다.
                foreach (var root in selected)
                {
                    SceneManager.MoveGameObjectToScene(root, scene);
                    root.transform.SetParent(owner.transform, false);
                }
                T UI<T>() where T : Component => owner.GetComponentsInChildren<T>(true).Single();
                var hud = UI<GameUIController>();
                var catalog = UI<BuildingCatalogPanel>();
                var info = UI<BuildingInfoPanel>();
                var actions = UI<BuildingActionPanel>();
                // 실제 업그레이드 레벨 표시는 아직 연결하지 않았다. 고정 레벨 1을 실제 정보처럼 표시하지 않는다.
                Ref<TMP_Text>(info, "_levelText").gameObject.SetActive(false);
                var gold = UI<RunGoldHudBinding>();
                var core = UI<CoreHudBinding>();
                Assign(gold, "_ui", hud, "_currencyManager", wallet);
                Assign(core, "_ui", hud, "_flow", flow, "_waves", waves);
                UnityEngine.Object.DestroyImmediate(UI<CoreRunDecisionBinding>());
                UnityEngine.Object.DestroyImmediate(UI<RuntimeUnitCountHud>());
                hud.transform.Find("QuarterDecision").gameObject.SetActive(false);
                hud.transform.Find("UnitCounts").gameObject.SetActive(false);
                hud.transform.Find("BottomBar/WaveStart").gameObject.SetActive(false);
                // 실제 전투 스포너/결과 계약 전에는 테스트 전투를 플레이어 버튼으로 시작하지 않는다.
                var hint = hud.transform.Find("BottomBar/Hint").GetComponent<TMP_Text>();
                hint.text = "건설할 위치를 선택하세요";
                var binding = owner.AddComponent<RuntimeBuildingUiBinding>();
                Assign(binding, "_catalog", catalog, "_info", info, "_actions", actions, "_controller", controller,
                    "_database", database, "_wallet", wallet, "_flow", flow, "_feedbackText", hint);
                SetArray(binding, "_slots", slots);
                foreach (var slot in slots)
                {
                    var target = slot.gameObject.AddComponent<RuntimeBuildingSelectionTarget>();
                    Assign(target, "_slot", slot, "_binding", binding, "_flow", flow);
                }
                var launch = hud.transform.Find("BottomBar/Build").GetComponent<RuntimeBuildingSlotButton>();
                Assign(launch, "_binding", binding, "_flow", flow);
                var nav = UI<PlayerUiNavigation>();
                Assign(nav, "_flow", flow, "_catalog", catalog);
                SetArray(nav, "_popups", new[] { catalog.Popup, Ref<PlayerPopup>(info, "_playerPopup") });
                SetArray(nav, "_catalogButtons", Array.Empty<Button>());
                SetArray(nav, "_blockingPanels", new[] { Ref<GameObject>(hud, "_waveRewardPanel"),
                    Ref<GameObject>(hud, "_runResultPanel"), Ref<GameObject>(hud, "_messagePanel") });
                var gemText = UnityEngine.Object.Instantiate(hud.transform.Find("TopBar/Gold").GetComponent<TMP_Text>(), hud.transform.Find("TopBar"));
                gemText.name = "Gems"; gemText.text = "보석 --"; gemText.fontSize = 22;
                gemText.color = new Color32(117, 203, 174, 255);
                Layout(hud, gemText);
                var startup = owner.AddComponent<TeamBuildingUiStartup>();
                Assign(startup, "_ui", hud, "_wallet", wallet, "_waves", waves, "_flow", flow,
                    "_gold", gold, "_core", core, "_gemText", gemText, "_hint", hint);
                var effects = teamRoots.SelectMany(r => r.GetComponentsInChildren<EffectManager>(true)).SingleOrDefault();
                if (effects != null) Assign(startup, "_effects", effects);
                EnsureArtifactBackend(scene, owner);
                owner.SetActive(true);
                UiCoreCameraRigSetup.Ensure(scene, flow, camera);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("UI integration scene save failed.");
                Debug.Log($"[UI/TeamBuilding] Created {ScenePath}; slots={slots.Length}, original refund={controller.RefundRate}. Source assets unchanged.");
            }
            finally
            {
                if (template.IsValid() && template.isLoaded) EditorSceneManager.CloseScene(template, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        public static void ConnectExistingArtifactBackend()
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run UI scene migration only in a separate batch Editor.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("A loaded scene has unsaved changes.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("Missing UI-owned team building scene: " + ScenePath);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var owner = scene.GetRootGameObjects().Single(root => root.name == "Team Building Player UI");
            if (!EnsureArtifactBackend(scene, owner)) return;
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Failed to save UI artifact backend: " + ScenePath);
            Debug.Log("[UI/TeamBuilding] Connected artifact backend in UI-owned scene.");
        }

        private static bool EnsureArtifactBackend(Scene scene, GameObject owner)
        {
            var roots = scene.GetRootGameObjects();
            var bootstrap = roots.SelectMany(root => root.GetComponentsInChildren<BootStrap>(true)).Single();
            var startup = owner.GetComponent<TeamBuildingUiStartup>();
            if (startup == null) throw new InvalidOperationException("Missing TeamBuildingUiStartup.");
            var catalog = AssetDatabase.LoadAssetAtPath<ArtifactCatalog>(MvpVictoryRewardSetup.CatalogPath);
            var table = AssetDatabase.LoadAssetAtPath<ArtifactRewardTable>(MvpVictoryRewardSetup.ArtifactTablePath);
            if (catalog == null || table == null || !table.IsValid)
                throw new InvalidOperationException("Team artifact catalog or reward table is unavailable.");

            bool changed = false;
            var manager = roots.SelectMany(root => root.GetComponentsInChildren<ArtifactManager>(true)).SingleOrDefault();
            if (manager == null) { manager = owner.AddComponent<ArtifactManager>(); changed = true; }
            var effects = roots.SelectMany(root => root.GetComponentsInChildren<EffectManager>(true)).SingleOrDefault();
            if (effects == null) { effects = owner.AddComponent<EffectManager>(); changed = true; }
            changed |= SetReferenceIfEmpty(manager, "_artifactCatalog", catalog);
            changed |= SetReferenceIfEmpty(manager, "_rewardTable", table);
            changed |= SetReferenceIfEmpty(bootstrap, "_artifactManager", manager);
            changed |= SetReferenceIfEmpty(bootstrap, "_effectManager", effects);
            changed |= SetReferenceIfEmpty(startup, "_effects", effects);
            return changed;
        }

        private static bool SetReferenceIfEmpty(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            var fields = new SerializedObject(target);
            var property = fields.FindProperty(name);
            if (property == null) throw new InvalidOperationException("Missing serialized field: " + name);
            if (property.objectReferenceValue == value) return false;
            if (property.objectReferenceValue != null)
                throw new InvalidOperationException("Custom reference will not be overwritten: " + target.name + "." + name);
            property.objectReferenceValue = value;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static void Layout(GameUIController hud, TMP_Text gems)
        {
            var top = (RectTransform)hud.transform.Find("TopBar");
            top.anchorMin = new Vector2(0, 1); top.anchorMax = Vector2.one;
            top.sizeDelta = new Vector2(-64, 82); top.anchoredPosition = new Vector2(32, -28);
            var bottom = (RectTransform)hud.transform.Find("BottomBar");
            bottom.anchorMin = Vector2.zero; bottom.anchorMax = new Vector2(1, 0); bottom.pivot = Vector2.zero;
            bottom.sizeDelta = new Vector2(-64, 104); bottom.anchoredPosition = new Vector2(32, 30);
            Right(top.Find("Gold"), -166, -18); Right(top.Find("GoldIcon"), -212, -25);
            Right(gems.transform, -354, -18);
            Right(bottom.Find("Build"), -186, -25);
            foreach (string name in new[] { "Quarter", "Wave" })
            {
                var rect = (RectTransform)top.Find(name);
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, 1);
                rect.anchoredPosition = new Vector2(0, rect.anchoredPosition.y);
            }
        }

        private static void Right(Transform transform, float x, float y)
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, y);
        }

        private static T Ref<T>(UnityEngine.Object obj, string field) where T : UnityEngine.Object =>
            new SerializedObject(obj).FindProperty(field).objectReferenceValue as T;
        private static void Assign(UnityEngine.Object obj, params object[] pairs) => MvpHudBuilder.Assign(obj, pairs);
        private static void SetInt(UnityEngine.Object obj, string field, int value)
        {
            var so = new SerializedObject(obj); so.FindProperty(field).intValue = value; so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void SetArray(UnityEngine.Object obj, string field, UnityEngine.Object[] values)
        {
            var so = new SerializedObject(obj); var array = so.FindProperty(field); array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
