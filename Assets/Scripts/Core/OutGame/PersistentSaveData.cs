using System;

// 아웃게임의 특성, 혈석과 시작을 확정한 제단/토템 선택을 함께 저장합니다.
[Serializable]
public class PersistentSaveData
{
    public TraitSaveData Traits;
    public PersistentWalletSaveData Wallet;
    public AltarRunSaveData Altar;
    public TotemRunSaveData Totem;
    public string LastSettledRunId;
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
