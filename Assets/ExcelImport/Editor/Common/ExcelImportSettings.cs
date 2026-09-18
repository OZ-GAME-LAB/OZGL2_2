using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public enum ExcelImportType
{
    WaveReward = 0,
    ArtifactReward = 1,
    ShopArtifactPrices = 2,
    ShopArtifactExchanges = 3,
    Artifacts = 4
}

[Serializable]
public class ExcelImportEntry
{
    public ExcelImportType Type;
    public string ExcelGuid;
    public string SheetName = "WaveRewards";
    public string TargetGuid;
    public string CatalogGuid;
    public bool Automatic;
    public string LastImportedHash;
    public string OutputFolder = "Assets/Data/Artifacts/Datas";

    public string ExcelPath => AssetDatabase.GUIDToAssetPath(ExcelGuid);
    public UnityEngine.Object Excel => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ExcelPath);
    public ScriptableObject Target => AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(TargetGuid));
    public CurrencyCatalog Catalog => AssetDatabase.LoadAssetAtPath<CurrencyCatalog>(AssetDatabase.GUIDToAssetPath(CatalogGuid));
}

// 기존 단일 연결 설정도 첫 번째 항목으로 이전
[FilePath("ProjectSettings/ExcelImportSettings.asset", FilePathAttribute.Location.ProjectFolder)]
public class ExcelImportSettings : ScriptableSingleton<ExcelImportSettings>
{
    public List<ExcelImportEntry> Entries = new List<ExcelImportEntry>();
    [SerializeField, HideInInspector] private string ExcelGuid;
    [SerializeField, HideInInspector] private string TableGuid;
    [SerializeField, HideInInspector] private string CatalogGuid;
    [SerializeField, HideInInspector] private bool Automatic;

    public void Migrate()
    {
        if (!string.IsNullOrEmpty(ExcelGuid))
        {
            Entries.Add(new ExcelImportEntry {
                ExcelGuid = ExcelGuid, TargetGuid = TableGuid, CatalogGuid = CatalogGuid,
                Automatic = Automatic
            });
            ExcelGuid = "";
            TableGuid = "";
            CatalogGuid = "";
            Automatic = false;
            SaveSettings();
        }
    }

    public void SaveSettings()
    {
        Save(true);
    }
}
