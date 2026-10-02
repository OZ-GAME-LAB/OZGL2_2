using System.IO;
using Game.Core;
using OZGL.KDH;
using UnityEngine;


public class InGameSaveData
{
    public GameFlowSaveData Flow;
}

public class InGameSaveCoordinator : MonoBehaviour
{
    // 기존 저장 테스트나 실제 영구재화 파일과 구분하는 키입니다.
    public static readonly SaveKey<InGameSaveData> SaveKey =
        new SaveKey<InGameSaveData>("ingame-test");

    public bool IsReady { get; private set; }
    public string LastError { get; private set; }
    public string SavePath => _saveManager == null ? string.Empty : _saveManager.GetFilePath(SaveKey);

    private SaveManager _saveManager;
    
    private ISaveDataProvider<GameFlowSaveData> _flowController;
    private ISaveDataProvider<RunCurrencySaveData> _runCurrency;
    private ISaveDataProvider<ArtifactSaveData> _artifact;
    private ISaveDataProvider<ShopSaveData> _shop;
    private ISaveDataProvider<ConsumableItemSaveData> _consumableItem;
    private ISaveDataProvider<BuildingSaveData> _building;
    private ISaveDataProvider<ArchiveSaveData> _archive;
    private ISaveDataProvider<PersistentSaveData> _persistent;
    
    
    public void Initialize(SaveManager saveManager, ISaveDataProvider<GameFlowSaveData> gameFlowController)
    {
        _saveManager = saveManager;
        _flowController = gameFlowController;
        IsReady = false;
        LastError = null;
    }

    // 최초 실행에만 기본 상태를 저장합니다. 손상된 기존 파일은 덮어쓰지 않습니다.
    public bool TryLoadOrCreate(out string error)
    {
        if (!CheckReferences(out error)) return false;
        if (File.Exists(SavePath)) return TryLoad(out error);

        InGameSaveData data = CaptureSaveData();
        if (!TryWrite(data, out error)) return false;
        IsReady = true;
        return true;
    }

    public bool TrySave(out string error)
    {
        if (!CheckReady(out error)) return false;
        return TryWrite(CaptureSaveData(), out error);
    }

    // 콘텐츠가 만든 데이터를 검증하고 파일에 저장만 합니다. 구매나 상태 변경은 하지 않습니다.
    public bool TrySave(InGameSaveData data, out string error)
    {
        if (!CheckReady(out error)) return false;
        return TryWrite(data, out error);
    }

    public bool TryLoad(out string error)
    {
        try
        {
            IsReady = false;
            if (!CheckReferences(out error)) return false;
            if (!_saveManager.TryLoad(SaveKey, out InGameSaveData data, out error))
                return Fail(error, out error);
            if (!TryValidate(data, out error)) return false;

            // 모든 영역을 검증한 뒤 함께 복원합니다. 복원 중에는 구매나 저장을 호출하지 않습니다.
            Apply(data);
            
            IsReady = true;
            LastError = null;
            return true;
        }
        catch (System.ArgumentException exception)
        {
            IsReady = false;
            return Fail($"저장 데이터 복원 실패: {exception.Message}", out error);
        }
    }

    public InGameSaveData CaptureSaveData()
    {
        if (!CheckReferences(out string error)) throw new System.InvalidOperationException(error);
        return new InGameSaveData
        {
            Flow = _flowController.CaptureSaveData(),
        };
    }

    private bool TryWrite(InGameSaveData data, out string error)
    {
        if (!TryValidate(data, out error)) return false;
        if (!_saveManager.TrySave(SaveKey, data, out error)) return Fail(error, out error);
        LastError = null;
        return true;
    }

    private bool TryValidate(InGameSaveData data, out string error)
    {
        if (data == null) return Fail("영구 저장 데이터가 없습니다.", out error);
        error = "s";
        return true;
    }

    private void Apply(InGameSaveData data)
    {
        _flowController.RestoreSaveData(data.Flow);
    }

    private bool CheckReferences(out string error)
    {
        error = null;
        if (_saveManager == null || _flowController == null)
        {
            return Fail("SaveManager, 노드 컨트롤러를 먼저 연결해주세요", out error);
        }
        return true;
    }

    private bool CheckReady(out string error)
    {
        if (!CheckReferences(out error)) return false;
        if (!IsReady) return Fail("영구 데이터를 먼저 불러오거나 최초 데이터를 생성해주세요.", out error);
        return true;
    }

    private bool Fail(string message, out string error)
    {
        error = message;
        LastError = message;
        return false;
    }

    [ContextMenu("Persistent Save/Save Traits And Bloodstone")]
    private void SaveFromInspector()
    {
        if (TrySave(out string error)) Debug.Log("[PersistentSave] 저장 완료: " + SavePath, this);
        else Debug.LogWarning("[PersistentSave] 저장 실패: " + error, this);
    }

    [ContextMenu("Persistent Save/Load Traits And Bloodstone")]
    private void LoadFromInspector()
    {
        if (TryLoad(out string error)) Debug.Log("[PersistentSave] 복원 완료: " + SavePath, this);
        else Debug.LogWarning("[PersistentSave] 복원 실패: " + error, this);
    }
}
