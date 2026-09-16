using System;
using Game.Core;
using Game.UI.Samples;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI.Editor
{
    /// <summary>명시적으로 실행할 때만 우리 테스트 씬의 재화 참조를 연결한다. 씬을 열거나 저장하지 않는다.</summary>
    public static class MvpEconomyUiSetup
    {
        public const string RewardTablePath = "Assets/Data/Economy/S.O/WaveRewardTable.asset";

        [MenuItem("Game/UI/Connect Economy In Current UI Test Scene")]
        public static void ConnectCurrentScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before wiring the UI test scene.");
            bool changed = ConfigureScene(SceneManager.GetActiveScene(), true);
            Debug.Log(changed
                ? "[UI/MvpEconomyUiSetup] Connected the team reward table in the current UI test scene. Review and save when ready; Undo is supported."
                : "[UI/MvpEconomyUiSetup] Reward table is already connected. No scene changes.");
        }

        internal static bool IsSupportedScene(string path) =>
            path == MvpRuntimeHudBuilder.ScenePath || path == MvpBuildingPhaseBuilder.ScenePath;

        [MenuItem("Game/UI/Connect Economy In Current UI Test Scene", true)]
        private static bool CanConnectCurrentScene() =>
            !EditorApplication.isPlayingOrWillChangePlaymode &&
            IsSupportedScene(SceneManager.GetActiveScene().path);

        internal static WaveRewardTable LoadRewardTable()
        {
            var table = AssetDatabase.LoadAssetAtPath<WaveRewardTable>(RewardTablePath);
            if (table == null) throw new InvalidOperationException("Missing team reward table: " + RewardTablePath);
            for (int quarter = 1; quarter <= WaveController.MAIN_QUARTERS; quarter++)
                for (int wave = 1; wave <= WaveController.MAX_WAVE; wave++)
                    if (!table.TryGetRewards(quarter, wave, out _))
                        throw new InvalidOperationException($"Missing or invalid team reward entry: {quarter}/{wave}");
            return table;
        }

        internal static bool ConfigureScene(Scene scene, bool recordUndo)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Cannot wire economy while Play Mode is active or pending.");
            if (!scene.IsValid() || !scene.isLoaded || !IsSupportedScene(scene.path))
                throw new InvalidOperationException("Only MvpRuntimeHudTest or MvpBuildingPhaseTest is supported.");

            var sample = FindOne<MvpRuntimeHudSample>(scene);
            var manager = FindOne<RunCurrencyManager>(scene);
            var sampleFields = new SerializedObject(sample);
            if (sampleFields.FindProperty("_currencyManager").objectReferenceValue != manager)
                throw new InvalidOperationException("The UI sample must reference this scene's currency manager.");
            var table = LoadRewardTable();
            var fields = new SerializedObject(manager);
            var property = fields.FindProperty("_waveRewardTable");
            if (property == null) throw new InvalidOperationException("Latest team Economy API is required.");
            if (property.objectReferenceValue == table) return false;
            if (property.objectReferenceValue != null)
                throw new InvalidOperationException("A custom reward table is already assigned; it will not be overwritten.");

            if (recordUndo) Undo.RecordObject(manager, "Connect UI test wave rewards");
            property.objectReferenceValue = table;
            fields.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.IsPartOfPrefabInstance(manager))
                PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }

        private static T FindOne<T>(Scene scene) where T : Component
        {
            T found = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var item in root.GetComponentsInChildren<T>(true))
                {
                    if (found != null) throw new InvalidOperationException("Multiple " + typeof(T).Name + " components.");
                    found = item;
                }
            return found != null ? found : throw new InvalidOperationException("Missing " + typeof(T).Name);
        }
    }
}
