using System;
using Game.Core;
using OZGL.KDH;
using UnityEngine;

[Serializable]
public class InGameSaveData
{
    public GameFlowSaveData Flow;
    public RunCurrencySaveData RunCurrency;
    public ArtifactSaveData Artifact;
    public ShopSaveData Shop;
    public ConsumableItemSaveData ConsumableItem;
    public BuildingSaveData Building;
    public ArchiveSaveData Archive;
}

public class InGameSaveCoordinator : MonoBehaviour, IRunCheckpointWriter
{
    // 기존 저장 테스트나 실제 영구재화 파일과 구분하는 키입니다.
    // 특성·토템·제단은 아웃게임에서 복원하므로 해당 데이터를 함께 저장하던 구버전 파일과 구분합니다.
    public static readonly SaveKey<InGameSaveData> SaveKey =
        new SaveKey<InGameSaveData>("ingame-test", version: 4);

    public bool IsReady { get; private set; }
    public string LastError { get; private set; }
    public string SavePath => _saveManager == null ? string.Empty : _saveManager.GetFilePath(SaveKey);

    // 병합 보존: dev의 파트별 복원 선택 옵션입니다. 현재는 일부 파트만 초기 상태로
    // 진행하지 않고 복원 실패 시 진입을 중단하므로 선택 옵션을 사용하지 않습니다.
    // dev에서 활성화한 Building 복원은 아래 Apply에서 항상 실행합니다.
    // [SerializeField] private bool _restoreRunCurrency = true;
    // [SerializeField] private bool _restoreArtifact = true;
    // [SerializeField] private bool _restoreShop = true;
    // [SerializeField] private bool _restoreConsumableItem = true;
    // [SerializeField] private bool _restoreBuilding = true;
    // [SerializeField] private bool _restoreArchive = true;

    private SaveManager _saveManager;
    private Action _initializeArchive;
    private bool _archiveInitializationAttempted;
    private Exception _archiveInitializationFailure;
    private bool _restoreFailed;
    private bool _requiresBackupBeforeWrite;
    private bool _hasBackedUpOriginal;
    
    private ISaveDataProvider<GameFlowSaveData> _flowController;
    private ISaveDataProvider<RunCurrencySaveData> _runCurrency;
    private ISaveDataProvider<ArtifactSaveData> _artifact;
    private ISaveDataProvider<ShopSaveData> _shop;
    private ISaveDataProvider<ConsumableItemSaveData> _consumableItem;
    private ISaveDataProvider<BuildingSaveData> _building;
    private ISaveDataProvider<ArchiveSaveData> _archive;
    
    
    // 10.9 / 문규성 / 아웃게임에서 복원하는 특성·토템·제단 연결을 제거하고 인게임 저장 Provider만 받도록 정리했습니다.
    public void Initialize(SaveManager saveManager,
        ISaveDataProvider<GameFlowSaveData> gameFlowController,
        ISaveDataProvider<RunCurrencySaveData> runCurrency,
        ISaveDataProvider<ArtifactSaveData> artifact,
        ISaveDataProvider<ShopSaveData> shop,
        ISaveDataProvider<ConsumableItemSaveData> consumableItem,
        ISaveDataProvider<BuildingSaveData> building,
        ISaveDataProvider<ArchiveSaveData> archive,
        Action initializeArchive = null)
    {
        if (saveManager == null || gameFlowController == null || runCurrency == null || artifact == null ||
            shop == null || consumableItem == null || building == null || archive == null)
        {
            Debug.LogError("[InGameSaveCoordinator] 초기화에 필요한 참조가 없습니다. ");
            LastError = "초기화에 필요한 참조가 없습니다";
            IsReady = false;
            return;
        }
        _saveManager = saveManager;
        _flowController = gameFlowController;
        _runCurrency = runCurrency;
        _artifact = artifact;
        _shop = shop;
        _consumableItem = consumableItem;
        _building = building;
        _archive = archive;
        _initializeArchive = initializeArchive;
        _archiveInitializationAttempted = false;
        _archiveInitializationFailure = null;
        _restoreFailed = false;
        _requiresBackupBeforeWrite = false;
        _hasBackedUpOriginal = false;
        IsReady = false;
        LastError = null;
    }

    // 최초 실행에만 기본 상태를 저장합니다. 손상된 기존 파일은 덮어쓰지 않습니다.
    public bool TryLoadOrCreate(out string error)
    {
        if (!CheckReferences(out error)) return false;
        if (_saveManager.HasSaveFile(SaveKey)) return TryLoad(out error);
        return TryCreateSave(out error);
    }

    public bool TryCreateSave(out string error)
    {
        IsReady = false;
        if (!CheckReferences(out error)) return false;
        if (_restoreFailed) return Fail("복원에 실패한 런을 초기 저장으로 대체할 수 없습니다.", out error);
        if (_saveManager.HasSaveFile(SaveKey)) return Fail("기존 인게임 저장 파일을 먼저 불러와주세요.", out error);
        try
        {
            if (!TryWrite(CaptureSaveData(), out error)) return false;
            IsReady = true;
            return true;
        }
        catch (Exception exception) when (!(exception is OperationCanceledException))
        {
            return Fail("최초 인게임 저장 생성 실패: " + exception.Message, out error);
        }
    }

    public bool TrySave(out string error)
    {
        if (!CheckReady(out error))
        {
            return false;
        }
        try
        {
            return TryWrite(CaptureSaveData(), out error);
        }
        catch (Exception exception) when (!(exception is OperationCanceledException))
        {
            return Fail("인게임 저장 실패: " + exception.Message, out error);
        }
    }

    // 콘텐츠가 만든 데이터를 검증하고 파일에 저장만 합니다. 구매나 상태 변경은 하지 않습니다.
    public bool TrySave(InGameSaveData data, out string error)
    {
        if (!CheckReady(out error))
        {
            return false;
        }
        return TryWrite(data, out error);
    }

    public bool TryLoad(out string error)
    {
        if (!TryReadSaveData(out InGameSaveData data, out error)) return false;
        return TryApplySaveData(data, out error);
    }

    // 종료된 런은 상태를 복원하지 않고도 식별할 수 있도록 읽기와 적용을 분리합니다.
    public bool TryReadSaveData(out InGameSaveData data, out string error)
    {
        IsReady = false;
        data = null;
        if (!CheckReferences(out error)) return false;
        if (!_saveManager.TryLoad(SaveKey, out data, out error)) return Fail(error, out error);
        LastError = null;
        return true;
    }

    public bool TryApplySaveData(InGameSaveData data, out string error)
    {
        IsReady = false;
        if (!CheckReferences(out error)) return false;
        _restoreFailed = true;
        try
        {
            if (!TryValidate(data, out error)) return false;
            Apply(data);
            _restoreFailed = false;
            IsReady = true;
            LastError = null;
            return true;
        }
        catch (Exception exception) when (!(exception is OperationCanceledException))
        {
            return Fail("인게임 저장 복원 실패: " + exception.Message, out error);
        }
    }

    public bool TryDelete(out string error)
    {
        if (_saveManager == null) return Fail("SaveManager를 먼저 연결해주세요.", out error);
        if (!_saveManager.TryDelete(SaveKey, out error)) return Fail(error, out error);
        IsReady = false;
        _requiresBackupBeforeWrite = false;
        _hasBackedUpOriginal = false;
        LastError = null;
        return true;
    }

    public void RequireBackupBeforeNextWrite()
    {
        if (!_hasBackedUpOriginal) _requiresBackupBeforeWrite = true;
    }

    // 10.9 / 문규성 / 아웃게임에 보관하는 특성·토템·제단은 중복 저장하지 않고 이번 런의 진행 상태만 수집합니다.
    public InGameSaveData CaptureSaveData()
    {
        if (!CheckReferences(out string error)) throw new InvalidOperationException(error);
        EnsureArchiveInitialized();
        return new InGameSaveData
        {
            Flow = _flowController.CaptureSaveData(),
            RunCurrency = _runCurrency.CaptureSaveData(),
            Artifact = _artifact.CaptureSaveData(),
            Shop = _shop.CaptureSaveData(),
            ConsumableItem = _consumableItem.CaptureSaveData(),
            Building = _building.CaptureSaveData(),
            Archive = _archive.CaptureSaveData()
        };
    }

    private bool TryWrite(InGameSaveData data, out string error)
    {
        if (!TryValidate(data, out error)) return false;
        if (_requiresBackupBeforeWrite)
        {
            if (!_saveManager.TryBackup(SaveKey, out string backupPath, out error))
                return Fail("원본 저장 백업 실패: " + error, out error);
            _requiresBackupBeforeWrite = false;
            _hasBackedUpOriginal = backupPath != null;
        }
        if (!_saveManager.TrySave(SaveKey, data, out error)) return Fail(error, out error);
        LastError = null;
        return true;
    }

    private bool TryValidate(InGameSaveData data, out string error)
    {
        if (data == null) return Fail("인게임 저장 데이터가 없습니다.", out error);
        error = null;
        return true;
    }

    // 10.9 / 문규성 / 아웃게임에서 먼저 등록한 효과는 유지하고 기존 순서대로 인게임 상태만 복원합니다.
    private void Apply(InGameSaveData data)
    {
        _flowController.RestoreSaveData(data.Flow);
        _building.RestoreSaveData(data.Building);
        _runCurrency.RestoreSaveData(data.RunCurrency);
        _artifact.RestoreSaveData(data.Artifact);
        EnsureArchiveInitialized();
        // 유물 효과가 슬롯 수에 영향을 주므로 최종 슬롯 상태는 그 뒤에 복원합니다.
        _consumableItem.RestoreSaveData(data.ConsumableItem);
        _shop.RestoreSaveData(data.Shop);
        _archive.RestoreSaveData(data.Archive);
    }

    private void EnsureArchiveInitialized()
    {
        if (_archiveInitializationAttempted)
        {
            if (_archiveInitializationFailure != null) throw _archiveInitializationFailure;
            return;
        }
        // 실패한 콜백을 다시 호출하여 이벤트를 중복 구독하지 않습니다.
        _archiveInitializationAttempted = true;
        try
        {
            _initializeArchive?.Invoke();
        }
        catch (Exception exception)
        {
            _archiveInitializationFailure = exception;
            throw;
        }
    }

    // 10.9 / 문규성 / 인게임 코디네이터가 직접 저장하고 복원하는 Provider만 연결 여부를 확인하도록 정리했습니다.
    private bool CheckReferences(out string error)
    {
        error = null;
        if (_saveManager == null || _flowController == null || _runCurrency == null || _artifact == null ||
            _shop == null || _consumableItem == null || _building == null || _archive == null)
        {
            return Fail("SaveManager와 모든 인게임 저장 Provider를 먼저 연결해주세요.", out error);
        }
        return true;
    }

    private bool CheckReady(out string error)
    {
        if (!CheckReferences(out error)) return false;
        if (!IsReady) return Fail("인게임 데이터를 먼저 불러오거나 최초 데이터를 생성해주세요.", out error);
        return true;
    }

    private bool Fail(string message, out string error)
    {
        error = message;
        LastError = message;
        return false;
    }

    [ContextMenu("InGame Save/Save")]
    private void SaveFromInspector()
    {
        if (TrySave(out string error)) Debug.Log("[InGameSave] 저장 완료: " + SavePath, this);
        else Debug.LogWarning("[InGameSave] 저장 실패: " + error, this);
    }

    [ContextMenu("InGame Save/Load")]
    private void LoadFromInspector()
    {
        if (TryLoad(out string error)) Debug.Log("[InGameSave] 복원 완료: " + SavePath, this);
        else Debug.LogWarning("[InGameSave] 복원 실패: " + error, this);
    }
}
