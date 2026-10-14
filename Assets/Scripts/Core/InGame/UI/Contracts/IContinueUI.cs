using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.UI
{
    /// <summary>전달된 안내를 표시하고 확인 입력을 기다린다. 외부 종료는 취소로 반환한다.</summary>
    public interface IContinueUI
    {
        UniTask ShowAsync(string message, CancellationToken token);
    }
}
