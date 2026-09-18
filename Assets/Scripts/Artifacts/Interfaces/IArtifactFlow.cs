using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;

// 게임 플로우에서 아티팩트 보상 선택 및 Run 종료 정리에 사용
public interface IArtifactFlow
{
    UniTask<bool> SelectAndApplyAsync(WaveBattleType battleType, CancellationToken token);
    bool TryEndRun();
}
