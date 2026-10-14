using UnityEditor;
using UnityEngine;

namespace Game.UI.InGame.Editor
{
    /// <summary>현재 새 UI를 두 Canvas 구조로 이전한다. 이전 MainUI는 복제하지 않는다.</summary>
    public static class InGameUIMigration
    {
        public const string ScenePath = "Assets/Scenes/Test/Test.unity";
        public const string FolderPath = "Assets/Prefabs/UI/Test";
        public const string RootPrefabPath = FolderPath + "/InGameUIRoot.prefab";

        [MenuItem("Game/UI/InGame/Convert Current UI To Two Canvases")]
        private static void ConvertFromMenu() => Debug.Log(InGameUITwoCanvasMigration.ConvertAndInstall());

        [MenuItem("Game/UI/InGame/Validate Current UI Prefab")]
        private static void ValidateFromMenu() => Debug.Log(InGameUITwoCanvasMigration.ValidatePrefab());

        public static string CreatePrefab() => InGameUITwoCanvasMigration.ConvertPrefab();
        public static string InstallInTest() => InGameUITwoCanvasMigration.InstallInTest();
    }
}
