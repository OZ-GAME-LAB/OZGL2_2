using System;

// 특성 구매 결과와 차감된 혈석은 항상 같은 파일에 함께 저장합니다.
// 제단/토템의 이번 판 선택은 영구 성장 데이터에 포함하지 않습니다.
[Serializable]
public class PersistentSaveData
{
    public TraitSaveData Traits;
    public PersistentWalletSaveData Wallet;
}

[Serializable]
public class PersistentWalletSaveData //영구재화 저장 데이터
{
    public CurrencyType Type;
    public int Amount;

    public PersistentWalletSaveData(int amount)
    {
        Type = CurrencyType.Bloodstone;
        Amount = amount;
    }
}
