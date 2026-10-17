using Cysharp.Threading.Tasks;
using UnityEngine;

public class MainSceneBootstrap : MonoBehaviour, ISceneEntry
{
    [SerializeField] private MainSceneManager _mainSceneManager;

    public UniTask InitializeAsync(ISceneTransition transition)
    {
        _mainSceneManager.Initialize(transition);
        return UniTask.CompletedTask;
    }

    public void OnPresented() { }
}
