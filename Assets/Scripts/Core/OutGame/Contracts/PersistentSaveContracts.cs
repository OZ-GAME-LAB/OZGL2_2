// 전체 사본에는 선택 설정과 마지막 정산 Run ID도 포함해 저장 중에 보존합니다.
public interface IPersistentSaveWriter
{
    bool IsReady { get; }
    PersistentSaveData CaptureSaveData();
    bool TrySave(PersistentSaveData data, out string error);
}

// 실제 지갑으로 바꿀 때도 저장 후 상태 반영과 알림 순서를 유지합니다.
public interface IPersistentWallet : ICurrencySpender, ISaveDataProvider<PersistentWalletSaveData>
{
    bool IsInitialized { get; }
    void RestoreSaveData(PersistentWalletSaveData data, bool notifyChanged);
    void NotifyChanged();
}
