// 콘텐츠는 저장 구현 대신 이 계약으로 사본을 가져오고 저장을 요청합니다.
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
