/// <summary>Run 기록에 따른 혈석 정산 보상의 계산과 지급에 사용하는 인터페이스</summary>
public interface IRunSettlementRewards
{
    /// <summary>웨이브 클리어 수와 유닛·보스 처치 수를 받아 혈석을 계산하고 지급</summary>
    bool TryApplyReward(int totalWaveCleared, int totalUnitsKilled, int totalBossesKilled);
}
