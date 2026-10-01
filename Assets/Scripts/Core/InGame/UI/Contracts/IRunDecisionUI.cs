using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.UI
{
    /// <summary>완료한 분기를 표시하고 종료 또는 계속 도전 의도를 반환한다.</summary>
    public interface IRunDecisionUI
    {
        UniTask<RunDecision> ChooseAsync(int quarter, CancellationToken token);
    }
}
