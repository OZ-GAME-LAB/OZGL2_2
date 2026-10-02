using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.UI
{
    /// <summary>아티팩트 시스템이 후보를 표시하고 플레이어의 선택을 기다릴 때 사용하는 UI 계약.</summary>
    public interface IArtifactSelectionUI
    {
        /// <summary>표시부터 닫기까지 한 요청을 소유한다. null은 허용된 포기이며 취소는 예외로 전달한다.</summary>
        UniTask<ArtifactData> SelectAsync(
            IReadOnlyList<ArtifactData> candidates,
            CancellationToken token,
            bool allowForfeit = false,
            string message = null);
    }
}
