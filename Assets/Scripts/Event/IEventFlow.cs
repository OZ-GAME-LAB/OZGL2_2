using System.Threading;
using Cysharp.Threading.Tasks;

public interface IEventFlow
{
    UniTask OpenEventAsync(CancellationToken token);
}
