using System.Threading;
using Cysharp.Threading.Tasks;

public interface IEventUI
{
    UniTask<int> ChooseAsync(EventData data, bool[] available, string[] reasons, string feedback, CancellationToken token);
    UniTask ShowMessageAsync(EventData data, string message, string buttonText, CancellationToken token);
    void Close();
}
