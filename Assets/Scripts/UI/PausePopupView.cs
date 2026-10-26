using System;
using Game.Core;
using Game.UI.InGame;
using TMPro;
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
        // 기존 프리팹의 버튼 참조를 유지하면서 표시와 동작을 변경합니다.
        var label = ReturnToMainButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "저장 후 게임 종료";
        ReturnToMainButton.onClick.AddListener(ConfirmSaveAndQuit);
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
        ReturnToMainButton.onClick.RemoveListener(ConfirmSaveAndQuit);
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

    private void ConfirmSaveAndQuit()
    {
        // 저장 실패 시에도 일시정지를 유지하도록 공통 확인 처리와 달리 Pause를 닫지 않습니다.
        _confirmPopup.Show("현재 진행을 저장하고 게임을 종료하시겠습니까?\n전투 중이라면 해당 전투의 준비 단계부터 이어집니다.", SaveAndQuit);
    }

    private void SaveAndQuit()
    {
        if (!_gameFlowController.TrySaveBeforeExit(out string error))
        {
            Debug.LogError("[PausePopup] 종료 전 저장 실패: " + error, this);
            _confirmPopup.Show("저장하지 못해 게임을 종료하지 않았습니다.\n" + error + "\n잠시 후 다시 시도해주세요.", null);
            return;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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
