using Cysharp.Threading.Tasks;

public interface ISceneTransition
{
    UniTask LoadAsync(string sceneName);
}
