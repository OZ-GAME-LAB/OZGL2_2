using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;

// 혈석 저장과 정산 UI 확인이 모두 끝났을 때 true를 반환합니다.
public interface IRunSettlementRewards
{
    UniTask<bool> TryApplyReward(RunSummary summary, CancellationToken token = default);
}
