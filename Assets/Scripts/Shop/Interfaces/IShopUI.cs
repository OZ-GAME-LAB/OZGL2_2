using System.Threading;
using Cysharp.Threading.Tasks;

// 상점 화면을 열고 나가기 버튼을 누를 때까지 기다리는 UI 계약.
public interface IShopUI
{
    UniTask OpenAndWaitForCloseAsync(ShopManager shop, CancellationToken token);
}
