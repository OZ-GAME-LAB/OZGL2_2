using System;
using Game.UI.Samples;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI.Editor
{
    /// <summary>사용자가 직접 실행할 때만 현재 UI 테스트 씬에 연결한다. 씬/프리팹을 열거나 저장하지 않는다.</summary>
    public static class MvpVictoryRewardSetup
    {
        public const string CatalogPath = "Assets/Data/Artifacts/Artifact_Catalog.asset";
        public const string ArtifactTablePath = "Assets/Data/Artifacts/Artifact_RewardTable.asset";

        [MenuItem("Game/UI/Connect Victory Rewards In Current UI Test Scene")]
        public static void ConnectCurrentScene()
        {
            bool changed = ConfigureScene(SceneManager.GetActiveScene(), true);
            Debug.Log(changed
                ? "[UI/MvpVictoryRewardSetup] Victory rewards connected. Review the scene and save manually; Undo is supported."
                : "[UI/MvpVictoryRewardSetup] Already connected. No scene changes.");
        }

        [MenuItem("Game/UI/Connect Victory Rewards In Current UI Test Scene", true)]
        private static bool CanConnectCurrentScene() =>
            !EditorApplication.isPlayingOrWillChangePlaymode &&
            MvpEconomyUiSetup.IsSupportedScene(SceneManager.GetActiveScene().path);

        internal static bool ConfigureScene(Scene scene, bool recordUndo)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || !scene.isLoaded ||
                !MvpEconomyUiSetup.IsSupportedScene(scene.path))
                throw new InvalidOperationException("Only the current UI runtime/building test scene in Edit Mode is supported.");

            // 참조 충돌과 누락을 먼저 검사한다. 기존 사용자 설정은 덮어쓰지 않는다.
            var sample = FindOne<MvpRuntimeHudSample>(scene, true);
            var currency = FindOne<RunCurrencyManager>(scene, true);
            var manager = FindOne<ArtifactManager>(scene, false);
            var effects = FindOne<EffectManager>(scene, false);
            var binding = FindOne<ArtifactRewardBinding>(scene, false);
            var panel = FindOne<ArtifactRewardPanel>(scene, false);
            if (FindOne<MvpArtifactRewardSample>(scene, false) != null)
                throw new InvalidOperationException("Remove the mock reward receiver before connecting real rewards.");
            var catalog = AssetDatabase.LoadAssetAtPath<ArtifactCatalog>(CatalogPath);
            var table = AssetDatabase.LoadAssetAtPath<ArtifactRewardTable>(ArtifactTablePath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MvpArtifactRewardBuilder.PrefabPath);
            if (catalog == null || catalog.Artifacts.Count == 0 || table == null || !table.IsValid ||
                prefab == null || prefab.GetComponent<ArtifactRewardPanel>() == null)
                throw new InvalidOperationException("Missing team artifact catalog/table or existing reward UI prefab.");
            var waveTable = MvpEconomyUiSetup.LoadRewardTable();
            if (new SerializedObject(sample).FindProperty("_currencyManager").objectReferenceValue != currency)
                throw new InvalidOperationException("The UI sample must already reference this scene's currency manager.");
            CheckReference(sample, "_artifactManager", manager);
            CheckReference(sample, "_effectManager", effects);
            CheckReference(sample, "_artifactRewards", binding);
            CheckReference(currency, "_waveRewardTable", waveTable);
            if (manager != null)
            {
                CheckReference(manager, "_artifactCatalog", catalog);
                CheckReference(manager, "_rewardTable", table);
            }
            if (binding != null) CheckReference(binding, "_panel", panel);

            int group = -1;
            if (recordUndo)
            {
                Undo.IncrementCurrentGroup();
                group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Connect UI victory rewards");
            }
            bool changed = false;
            try
            {
                if (manager == null) { manager = Add<ArtifactManager>(sample.gameObject, recordUndo); changed = true; }
                if (effects == null) { effects = Add<EffectManager>(sample.gameObject, recordUndo); changed = true; }
                if (binding == null) { binding = Add<ArtifactRewardBinding>(sample.gameObject, recordUndo); changed = true; }
                if (panel == null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    if (recordUndo) Undo.RegisterCreatedObjectUndo(instance, "Add existing victory reward UI");
                    panel = instance.GetComponent<ArtifactRewardPanel>();
                    changed = true;
                }
                changed |= SetReference(currency, "_waveRewardTable", waveTable, recordUndo);
                changed |= SetReference(manager, "_artifactCatalog", catalog, recordUndo);
                changed |= SetReference(manager, "_rewardTable", table, recordUndo);
                changed |= SetReference(binding, "_panel", panel, recordUndo);
                changed |= SetReference(sample, "_artifactManager", manager, recordUndo);
                changed |= SetReference(sample, "_effectManager", effects, recordUndo);
                changed |= SetReference(sample, "_artifactRewards", binding, recordUndo);
                if (changed) EditorSceneManager.MarkSceneDirty(scene);
                if (recordUndo) Undo.CollapseUndoOperations(group);
                return changed;
            }
            catch
            {
                if (recordUndo) Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        private static T Add<T>(GameObject owner, bool recordUndo) where T : Component =>
            recordUndo ? Undo.AddComponent<T>(owner) : owner.AddComponent<T>();

        private static T FindOne<T>(Scene scene, bool required) where T : Component
        {
            T found = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var item in root.GetComponentsInChildren<T>(true))
                {
                    if (found != null) throw new InvalidOperationException("Multiple " + typeof(T).Name + " components.");
                    found = item;
                }
            if (required && found == null) throw new InvalidOperationException("Missing " + typeof(T).Name);
            return found;
        }

        private static void CheckReference(UnityEngine.Object target, string name, UnityEngine.Object expected)
        {
            var property = new SerializedObject(target).FindProperty(name);
            if (property == null) throw new InvalidOperationException("Missing serialized field: " + name);
            if (property.objectReferenceValue != null && property.objectReferenceValue != expected)
                throw new InvalidOperationException("Custom reference will not be overwritten: " + target.name + "." + name);
        }

        private static bool SetReference(UnityEngine.Object target, string name, UnityEngine.Object value, bool recordUndo)
        {
            var fields = new SerializedObject(target);
            var property = fields.FindProperty(name);
            if (property.objectReferenceValue == value) return false;
            if (recordUndo) Undo.RecordObject(target, "Connect victory reward reference");
            property.objectReferenceValue = value;
            fields.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.IsPartOfPrefabInstance(target))
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            return true;
        }
    }
}
