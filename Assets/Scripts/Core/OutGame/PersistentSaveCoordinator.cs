using System.IO;
using UnityEngine;

// 영구재화와 관련된 저장 데이터를 모으고 저장/복원 순서를 관리합니다
// JSON 변환과 파일 입출력은 SaveManager에서 합니다
public class PersistentSaveCoordinator : MonoBehaviour, IPersistentSaveWriter
{
    // 기존 저장 테스트나 실제 영구재화 파일과 구분하는 키입니다.
    public static readonly SaveKey<PersistentSaveData> SaveKey =
        new SaveKey<PersistentSaveData>("outgame-test-persistent");

    public bool IsReady { get; private set; }
    public string LastError { get; private set; }
    public string SavePath => _saveManager == null ? string.Empty : _saveManager.GetFilePath(SaveKey);

    private SaveManager _saveManager;
    private OutGameTraitController _traits;
    private OutGameTestWallet _wallet;

    public void Initialize(SaveManager saveManager, OutGameTraitController traits, OutGameTestWallet wallet)
    {
        _saveManager = saveManager;
        _traits = traits;
        _wallet = wallet;
        IsReady = false;
        LastError = null;
    }

    // 최초 실행에만 기본 상태를 저장합니다. 손상된 기존 파일은 덮어쓰지 않습니다.
    public bool TryLoadOrCreate(out string error)
    {
        if (!CheckReferences(out error)) return false;
        if (File.Exists(SavePath)) return TryLoad(out error);

        PersistentSaveData data = CaptureSaveData();
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
    public bool TrySave(PersistentSaveData data, out string error)
    {
        if (!CheckReady(out error)) return false;
        return TryWrite(data, out error);
    }

    public bool TryLoad(out string error)
    {
        IsReady = false;
        if (!CheckReferences(out error)) return false;
        if (!_saveManager.TryLoad(SaveKey, out PersistentSaveData data, out error))
            return Fail(error, out error);
        if (!TryValidate(data, out error)) return false;

        // 모든 영역을 검증한 뒤 함께 복원합니다. 복원 중에는 구매나 저장을 호출하지 않습니다.
        IsReady = true;
        LastError = null;
        Apply(data);
        return true;
    }

    public PersistentSaveData CaptureSaveData()
    {
        if (!CheckReferences(out string error)) throw new System.InvalidOperationException(error);
        return new PersistentSaveData
        {
            Traits = _traits.CaptureSaveData(),
            Wallet = _wallet.CaptureSaveData()
        };
    }

    private bool TryWrite(PersistentSaveData data, out string error)
    {
        if (!TryValidate(data, out error)) return false;
        if (!_saveManager.TrySave(SaveKey, data, out error)) return Fail(error, out error);
        LastError = null;
        return true;
    }

    private bool TryValidate(PersistentSaveData data, out string error)
    {
        if (data == null) return Fail("영구 저장 데이터가 없습니다.", out error);
        if (!_traits.TryValidateSaveData(data.Traits, out error)) return Fail(error, out error);
        if (!_wallet.TryValidateSaveData(data.Wallet, out error)) return Fail(error, out error);
        return true;
    }

    private void Apply(PersistentSaveData data)
    {
        _wallet.RestoreSaveData(data.Wallet, false);
        _traits.RestoreSaveData(data.Traits, false);
        // 두 상태가 모두 적용된 다음 UI에 알립니다.
        _wallet.NotifyChanged();
        _traits.NotifyChanged();
    }

    private bool CheckReferences(out string error)
    {
        if (_saveManager == null || _traits == null || _wallet == null)
            return Fail("SaveManager, 특성 Selector, 혈석 지갑을 먼저 연결해주세요.", out error);
        if (!_traits.IsInitialized || !_wallet.IsInitialized)
            return Fail("특성 Selector와 혈석 지갑을 먼저 초기화해주세요.", out error);
        error = null;
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
