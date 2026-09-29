using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.UI
{
    /// <summary>아티팩트 시스템이 후보를 표시하고 플레이어의 선택을 기다릴 때 사용하는 UI 계약.</summary>
    public interface IArtifactSelectionUI
    {
        void Open();

        /// <summary>후보 중 하나를 반환한다. null은 플레이어가 획득을 포기한 경우다.</summary>
        UniTask<ArtifactData> SelectAsync(
            IReadOnlyList<ArtifactData> candidates,
            CancellationToken token);

        void Close();
    }
}
