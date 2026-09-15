/// <summary>Run 중 웨이브·생산·적 드랍 보상의 계산과 지급에 사용하는 인터페이스</summary>
public interface IRunCurrencyRewards
{
    /// <summary>현재 분기·웨이브의 테이블 보상을 계산하고 지급</summary>
    bool TryApplyWaveReward();

    /// <summary>생산 재화 종류와 보정 전 수량을 받아 보상을 계산하고 지급</summary>
    bool TryApplyProductionReward(CurrencyType type, int amount);

    /// <summary>드랍 재화 종류와 보정 전 수량을 받아 보상을 계산하고 지급</summary>
    // bool TryApplyEnemyDropReward(CurrencyType type, int amount);
}
