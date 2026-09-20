using UnityEditor;
using UnityEngine;

// 종류별 창을 만들지 않고 한 곳에서 연결 목록 관리
public class ExcelImportWindow : EditorWindow
{
    private Vector2 _scroll;
    private string _result;

    [MenuItem("Tools/Excel Import")]
    private static void Open()
    {
        GetWindow<ExcelImportWindow>("Excel Import");
    }

    private void OnGUI()
    {
        ExcelImportSettings settings = ExcelImportSettings.instance;
        settings.Migrate();
        EditorGUILayout.HelpBox("엑셀 파일, 시트와 대상 S.O를 연결하세요. 자동 갱신은 Play 종료 후에도 변경 여부를 확인합니다. 가져오기는 대상 데이터 목록을 교체합니다.", MessageType.Info);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            for (int i = 0; i < settings.Entries.Count; i++)
            {
                ExcelImportEntry entry = settings.Entries[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"연결 {i + 1}", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                ExcelImportType selectedType = (ExcelImportType)EditorGUILayout.EnumPopup("가져오기 종류", entry.Type);
                if (selectedType != entry.Type)
                {
                    entry.Type = selectedType;
                    switch (selectedType)
                    {
                        case ExcelImportType.ArtifactReward: entry.SheetName = "ArtifactRewards"; break;
                        case ExcelImportType.ShopArtifactPrices: entry.SheetName = "ArtifactPrices"; break;
                        case ExcelImportType.ShopArtifactExchanges: entry.SheetName = "ArtifactExchanges"; break;
                        case ExcelImportType.Artifacts: entry.SheetName = "Artifacts"; break;
                        default: entry.SheetName = "WaveRewards"; break;
                    }
                    entry.TargetGuid = "";
                    entry.CatalogGuid = "";
                }
                Object excel = EditorGUILayout.ObjectField("엑셀 파일", entry.Excel, typeof(DefaultAsset), false);
                if (entry.Type == ExcelImportType.Artifacts)
                {
                    EditorGUILayout.LabelField("시트", "Artifacts / UnitStatEffects / CurrencyEffects");
                    entry.OutputFolder = EditorGUILayout.TextField("신규 S.O 폴더", entry.OutputFolder);
                    EditorGUILayout.HelpBox("대상에는 ArtifactCatalog를 연결하세요. 동일 ID는 갱신하고 신규 ID는 생성 후 카탈로그에 등록합니다. 아이콘과 엑셀에 없는 아티팩트는 유지합니다.", MessageType.Info);
                }
                else
                {
                    entry.SheetName = EditorGUILayout.TextField("시트", entry.SheetName);
                }
                System.Type targetType = typeof(ScriptableObject);
                if (entry.Type == ExcelImportType.WaveReward)
                {
                    targetType = typeof(WaveRewardTable);
                }
                else if (entry.Type == ExcelImportType.ArtifactReward)
                {
                    targetType = typeof(ArtifactRewardTable);
                }
                else if (entry.Type == ExcelImportType.ShopArtifactPrices)
                {
                    targetType = typeof(ShopArtifactTable);
                }
                else if (entry.Type == ExcelImportType.ShopArtifactExchanges)
                {
                    targetType = typeof(ShopArtifactExchangeTable);
                }
                else if (entry.Type == ExcelImportType.Artifacts)
                {
                    targetType = typeof(ArtifactCatalog);
                }
                Object target = EditorGUILayout.ObjectField("대상 S.O", entry.Target, targetType, false);
                Object catalog = entry.Catalog;
                if (entry.Type == ExcelImportType.WaveReward)
                {
                    catalog = EditorGUILayout.ObjectField("재화 카탈로그", entry.Catalog, typeof(CurrencyCatalog), false);
                }
                entry.Automatic = EditorGUILayout.Toggle("자동 갱신", entry.Automatic);
                if (EditorGUI.EndChangeCheck())
                {
                    entry.ExcelGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(excel));
                    entry.TargetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target));
                    entry.CatalogGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(catalog));
                    entry.LastImportedHash = "";
                    settings.SaveSettings();
                    ExcelAutoImport.Schedule();
                }
                if (GUILayout.Button("검증 후 S.O 갱신"))
                {
                    ExcelAutoImport.TryImport(entry, out _result);
                }
                bool remove = GUILayout.Button("연결 삭제");
                EditorGUILayout.EndVertical();
                if (remove)
                {
                    settings.Entries.RemoveAt(i);
                    settings.SaveSettings();
                    break;
                }
            }
            if (GUILayout.Button("연결 추가"))
            {
                settings.Entries.Add(new ExcelImportEntry());
                settings.SaveSettings();
            }
        }
        EditorGUILayout.EndScrollView();
        if (!string.IsNullOrEmpty(_result))
        {
            EditorGUILayout.HelpBox(_result, MessageType.Info);
        }
    }
}
