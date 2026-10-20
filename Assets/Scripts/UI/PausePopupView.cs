using System;
using Game.Core;
using Game.UI.InGame;
using UnityEngine;
using UnityEngine.UI;

public class PausePopupView : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button OptionButton;
    [SerializeField] private Button AbandonButton;
    [SerializeField] private Button ReturnToMainButton;

    [Header("UI")]
    [SerializeField] private GameObject OptionUIPanel;

    private GameFlowController _gameFlowController;
    private InGameUIManager _uiManager;
    private ConfirmPopupView _confirmPopup;
    private float _timeScale;

    private void Start()
    {
        OptionButton.onClick.AddListener(OpenOptionPanel);
        AbandonButton.onClick.AddListener(AbandonRun);
        ReturnToMainButton.onClick.AddListener(ReturnToMain);
    }

    private void OnEnable()
    {
        _timeScale = Time.timeScale;
        Time.timeScale = 0f;
    }

    private void OnDisable()
    {
        Time.timeScale = _timeScale;
    }

    private void OnDestroy()
    {
        OptionButton.onClick.RemoveListener(OpenOptionPanel);
        AbandonButton.onClick.RemoveListener(AbandonRun);
        ReturnToMainButton.onClick.RemoveListener(ReturnToMain);
    }

    public void Initialize(GameFlowController controller, InGameUIManager uiManager, ConfirmPopupView confirmPopup)
    {
        _gameFlowController = controller;
        _uiManager = uiManager;
        _confirmPopup = confirmPopup;
    }

    private void OpenOptionPanel()
    {
        OptionUIPanel.SetActive(true);
    }

    private void ReturnToMain()
    {
        ShowConfirmation("메인화면으로 이동하시겠습니까?\n전투 진행사항은 저장되지 않습니다", _gameFlowController.QuitRun);
    }

    private void AbandonRun()
    {
        ShowConfirmation("현재까지 진행된 게임을 포기합니다.\n<color=#FF654F>게임 패배로 처리되며 메인화면으로 돌아갑니다</color>",
            _gameFlowController.AbandonRun);
    }

    private void ShowConfirmation(string message, Action onConfirm)
    {
        _confirmPopup.Show(message, () =>
        {
            // 배속을 복원하고 창 닫기를 마친 뒤, 정산 등 다음 화면을 열 수 있게 한다.
            _uiManager.ClosePopup(UIId.Pause, UICloseReason.Completed);
            onConfirm();
        });
    }
}
