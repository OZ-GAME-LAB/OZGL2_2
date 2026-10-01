using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.UI
{
    /// <summary>준비된 정산 결과를 표시하고 확인을 기다린다. 지급과 씬 이동은 호출자 책임이다.</summary>
    public interface IRunSettlementUI
    {
        UniTask ShowAndWaitAsync(RunSettlementViewData data, CancellationToken token);
    }
}
