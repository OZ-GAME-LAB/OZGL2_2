#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI.InGame.Editor
{
    /// <summary>기존 프리팹을 유지하면서 건물 UI와 단일 유닛 팝업의 참조를 연결한다.</summary>
    public static class BuildingUiInstall
    {
        private const string Folder = "Assets/Prefabs/UI/InGame/";

        [MenuItem("Game/UI/InGame/Apply Building Panel")]
        public static void Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != InGameUIMigration.ScenePath)
                throw new InvalidOperationException("Test 씬의 Edit Mode에서 실행하세요.");

            EditPrefab("SkillPopup.prefab", root => UnitSkillPopupSetup.ConfigureSkill(root.GetComponent<UIScreen>()));
            EditPrefab("UnitPopup.prefab", root => UnitSkillPopupSetup.ConfigureUnit(root.GetComponent<UIScreen>(), null));
            EditPrefab("InGameUIRoot.prefab", ConfigureRoot);

            GameObject sceneRoot = scene.GetRootGameObjects().Single(root => root.name == "InGameUIRoot");
            bool active = sceneRoot.activeSelf;
            sceneRoot.SetActive(false);
            ConfigureRoot(sceneRoot);
            sceneRoot.SetActive(active);
            RecordOverrides(sceneRoot);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("건물 건설·관리 패널과 상세 팝업 연결을 저장했습니다.");
        }

        private static void ConfigureRoot(GameObject root)
        {
            UnitSkillPopupSetup.Configure(root);
            Game.UI.Editor.BuildingPanelSetup.Configure(root);
        }

        private static void EditPrefab(string name, Action<GameObject> configure)
        {
            string path = Folder + name;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool active = root.activeSelf;
                root.SetActive(false);
                configure(root);
                if (name == "InGameUIRoot.prefab")
                {
                    // 단독으로 사용하는 원본 팝업에도 새 표시 참조를 저장한다.
                    // 행동 View의 다른 팝업 참조는 통합 루트에서만 연결할 수 있다.
                    var actions = root.GetComponentInChildren<BuildingActionView>(true);
                    PrefabUtility.RevertObjectOverride(actions, InteractionMode.AutomatedAction);
                    foreach (UIScreen screen in root.GetComponentsInChildren<UIScreen>(true))
                    {
                        if (screen.Id == UIId.BuildingCatalog || screen.Id == UIId.BuildingInfo)
                            PrefabUtility.ApplyPrefabInstance(screen.gameObject, InteractionMode.AutomatedAction);
                    }
                    configure(root);
                }
                root.SetActive(active);
                RecordOverrides(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void RecordOverrides(GameObject root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(child)) continue;
                PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
                foreach (Component component in child.GetComponents<Component>())
                    if (component != null) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }
    }
}
#endif
