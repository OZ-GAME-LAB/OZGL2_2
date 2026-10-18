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
    public TutorialSaveData Tutorial;
}

[Serializable]
public class TutorialSaveData
{
    // 0은 기능 도입 이전 저장입니다. 신규 프로필 생성 시에만 1로 설정합니다.
    public int Version;
    public bool OutGameHandled;
    public bool InGameHandled;

    public TutorialSaveData Copy() => new TutorialSaveData
    {
        Version = Version, OutGameHandled = OutGameHandled, InGameHandled = InGameHandled
    };
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
