using Game.Core;

// Run 종료 결과와 현재 웨이브 위치로 혈석 정산
public interface IRunSettlementRewards
{
    // 승리는 현재 웨이브 포함, 패배는 현재 웨이브 제외
    bool TryApplyReward(ResultType result);
}
