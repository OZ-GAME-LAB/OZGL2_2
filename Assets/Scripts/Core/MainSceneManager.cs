using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class MainSceneManager : MonoBehaviour
{
    [Header("Main Menu Buttons")]
    [SerializeField] private Button StartGame;
    [SerializeField] private Button OptionButton;
    [SerializeField] private Button ExitButton;

    [Header("UI")]
    [SerializeField] private GameObject OptionPanel;

    [Header("Managers")]
    [SerializeField] private SaveManager SaveManager;
    private ISceneTransition _transition;
    private bool _isStarting;

    public void Initialize(ISceneTransition transition)
    {
        if (SaveManager == null || StartGame == null || OptionButton == null || ExitButton == null)
        {
            throw new System.InvalidOperationException("[Main Scene Manager] 필수 참조가 존재하지 않습니다");
        }

        _transition = transition;

        StartGame.onClick.RemoveListener(HandleNewGame);
        StartGame.onClick.AddListener(HandleNewGame);

        OptionButton.onClick.RemoveListener(HandleOption);
        OptionButton.onClick.AddListener(HandleOption);

        ExitButton.onClick.RemoveListener(HandleExit);
        ExitButton.onClick.AddListener(HandleExit);
    }

    private void HandleNewGame() => HandleNewGameAsync().Forget();

    private async UniTask HandleNewGameAsync()
    {
        if (_isStarting || SaveManager == null) return;
        _isStarting = true;
        StartGame.interactable = false;
        //인게임 세이브파일이 없으면 새로 시작, 있으면 이어하기
        try
        {
            string scene = SaveManager.HasSaveFile(InGameSaveCoordinator.SaveKey) ? "Test_InGame" : "Test_OutGame";
            await _transition.LoadAsync(scene);
        }
        catch (System.Exception exception)
        {
            if (this != null)
            {
                _isStarting = false;
                if (StartGame != null) StartGame.interactable = true;
            }
            Debug.LogError("[Main Scene Manager] 시작 화면 이동에 실패했습니다. " + exception.Message, this);
        }
    }

    private void HandleOption()
    {
        if (OptionPanel != null)
        {
            OptionPanel.SetActive(!OptionPanel.activeSelf);
        }
    }

    private void HandleExit()
    {
        Application.Quit();
    }

    private void OnDestroy()
    {
        if (StartGame != null) StartGame.onClick.RemoveListener(HandleNewGame);
        if (OptionButton != null) OptionButton.onClick.RemoveListener(HandleOption);
        if (ExitButton != null) ExitButton.onClick.RemoveListener(HandleExit);
    }

}
