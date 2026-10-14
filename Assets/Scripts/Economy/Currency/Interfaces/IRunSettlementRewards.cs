using Cysharp.Threading.Tasks;

// 혈석 정산 및 정산 UI 완료 대기 (UI 연결 예정)
public interface IRunSettlementRewards
{
    UniTask TryApplyReward();
}
