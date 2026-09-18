using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

// 에셋 가져오기 완료 후 실행하며 중복 예약은 하나로 모음
[InitializeOnLoad]
public static class ExcelAutoImport
{
    static ExcelAutoImport()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        Schedule();
    }

    public static void Schedule()
    {
        EditorApplication.delayCall -= CheckForChanges;
        EditorApplication.delayCall += CheckForChanges;
    }

    private static void HandlePlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            Schedule();
        }
    }

    private static void CheckForChanges()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            Schedule();
            return;
        }
        ExcelImportSettings settings = ExcelImportSettings.instance;
        settings.Migrate();
        foreach (ExcelImportEntry entry in settings.Entries)
        {
            if (!entry.Automatic)
            {
                continue;
            }
            if (!TryGetHash(entry, out string hash, out string error))
            {
                Debug.LogWarning("[ExcelImport] " + error);
                continue;
            }
            if (hash == entry.LastImportedHash)
            {
                continue;
            }
            if (TryImport(entry, out string result))
            {
                Debug.Log("[ExcelImport] " + entry.SheetName + ": " + result);
            }
            else
            {
                Debug.LogError("[ExcelImport] 기존 S.O 유지: " + result);
            }
        }
    }
    public static bool TryImport(ExcelImportEntry entry, out string result)
    {
        if (!TryGetHash(entry, out string hash, out result))
        {
            return false;
        }
        bool succeeded;
        switch (entry.Type)
        {
            case ExcelImportType.WaveReward:
                succeeded = WaveRewardExcelImporter.TryImport(entry.ExcelPath, entry.SheetName,
                    entry.Target as WaveRewardTable, entry.Catalog, out result);
                break;
            case ExcelImportType.ArtifactReward:
                succeeded = ArtifactRewardExcelImporter.TryImport(entry.ExcelPath, entry.SheetName,
                    entry.Target as ArtifactRewardTable, out result);
                break;
            case ExcelImportType.ShopArtifactPrices:
                succeeded = ShopArtifactExcelImporter.TryImportPrices(entry.ExcelPath, entry.SheetName,
                    entry.Target as ShopArtifactTable, out result);
                break;
            case ExcelImportType.ShopArtifactExchanges:
                succeeded = ShopArtifactExcelImporter.TryImportExchanges(entry.ExcelPath, entry.SheetName,
                    entry.Target as ShopArtifactExchangeTable, out result);
                break;
            case ExcelImportType.Artifacts:
                succeeded = ArtifactExcelImporter.TryImport(entry.ExcelPath, entry.Target as ArtifactCatalog,
                    entry.OutputFolder, out result);
                break;
            default:
                result = "지원하지 않는 가져오기 종류입니다.";
                return false;
        }
        if (!succeeded)
        {
            return false;
        }
        entry.LastImportedHash = hash;
        ExcelImportSettings.instance.SaveSettings();
        return true;
    }

    private static bool TryGetHash(ExcelImportEntry entry, out string hash, out string error)
    {
        hash = "";
        error = "";
        string path = entry.ExcelPath;
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(path) || entry.Target == null || string.IsNullOrWhiteSpace(entry.SheetName))
        {
            error = "Tools/Excel Import에서 xlsx 파일, 시트, 대상 S.O를 연결하세요.";
            return false;
        }
        foreach (ExcelImportEntry other in ExcelImportSettings.instance.Entries)
        {
            if (!ReferenceEquals(other, entry) && other.TargetGuid == entry.TargetGuid)
            {
                error = "같은 대상 S.O를 여러 연결에 등록할 수 없습니다.";
                return false;
            }
        }
        try
        {
            // 해시 계산도 엑셀의 열린 파일과 함께 접근할 수 있도록 공유 허용
            using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (SHA256 algorithm = SHA256.Create())
            {
                hash = BitConverter.ToString(algorithm.ComputeHash(file)) + entry.Type + entry.SheetName + entry.TargetGuid + entry.CatalogGuid + entry.OutputFolder;
            }
            return true;
        }
        catch (Exception exception)
        {
            error = "엑셀 파일을 읽지 못했습니다. " + exception.Message;
            return false;
        }
    }
}

public class ExcelImportPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths)
    {
        // S.O 저장은 대상이 아니므로 자동 갱신 반복 방지
        foreach (string path in importedAssets)
        {
            if (path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ExcelAutoImport.Schedule();
                return;
            }
        }
        foreach (string path in movedAssets)
        {
            if (path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ExcelAutoImport.Schedule();
                return;
            }
        }
    }
}
