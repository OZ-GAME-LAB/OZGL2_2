using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainSceneManager : MonoBehaviour
{
    [SerializeField] private Button StartGame;
    [SerializeField] private SaveManager SaveManager;
    private bool _isStarting;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (SaveManager == null || StartGame == null) 
        {
            Debug.LogWarning("[Main Scene Manager] 필수 참조가 존재하지 않습니다");
            return;
        }
        StartGame.onClick.AddListener(HandleNewGame);
    }

    private void HandleNewGame()
    {
        if (_isStarting || SaveManager == null) return;
        _isStarting = true;
        StartGame.interactable = false;
        //인게임 세이브파일이 없으면 새로 시작, 있으면 이어하기
        try
        {
            string scene = SaveManager.HasSaveFile(InGameSaveCoordinator.SaveKey) ? "Test" : "OutGameSetupTest";
            SceneManager.LoadScene(scene);
        }
        catch (System.Exception exception)
        {
            _isStarting = false;
            StartGame.interactable = true;
            Debug.LogError("[Main Scene Manager] 시작 화면 이동에 실패했습니다. " + exception.Message, this);
        }
    }

    private void OnDestroy()
    {
        if (StartGame != null) StartGame.onClick.RemoveListener(HandleNewGame);
    }

}
