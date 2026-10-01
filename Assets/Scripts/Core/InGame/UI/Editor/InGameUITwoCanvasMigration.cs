#if UNITY_EDITOR
namespace Game.UI.InGame.Editor
{
    /// <summary>이전 도구 진입점도 현재 UI 연결 이전만 수행합니다. 루트를 재생성하지 않습니다.</summary>
    public static class InGameUITwoCanvasMigration
    {
        public const string RootPath = "Assets/Prefabs/UI/Test/InGameUIRoot.prefab";
        public static string ConvertAndInstall() { InGameUIRequestMigration.Apply(); return ValidatePrefab(); }
        public static string ConvertPrefab() => ValidatePrefab();
        public static string InstallInTest() => ConvertAndInstall();
        public static string ValidatePrefab() => InGameUIRequestMigration.ValidatePrefab();
    }
}
#endif
