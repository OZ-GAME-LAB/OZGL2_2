using System.Threading;
using Cysharp.Threading.Tasks;

// 게임 플로우에서 상점 실행 및 Run 종료 정리에 사용
public interface IShopFlow
{
    UniTask OpenShopAsync(CancellationToken token);
    bool TryEndRun();
}
