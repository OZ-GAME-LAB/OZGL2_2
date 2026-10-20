using Cysharp.Threading.Tasks;
using UnityEngine;

public class MainSceneBootstrap : MonoBehaviour, ISceneEntry
{
    [SerializeField] private MainSceneManager _mainSceneManager;
    [SerializeField] private SaveManager _saveManager;
    [SerializeField] private GameSettingsManager _gameSettingsManager;

    public UniTask InitializeAsync(ISceneTransition transition)
    {
        _gameSettingsManager.Initialize(_saveManager);
        _mainSceneManager.Initialize(transition);
        return UniTask.CompletedTask;
    }

    public void OnPresented() { }
}
