using Cysharp.Threading.Tasks;

public interface ISceneEntry
{
    UniTask InitializeAsync(ISceneTransition transition);
    void OnPresented();
}
