using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Samples
{
    /// <summary>HUD 확인 전용 임시 데이터. 실제 게임 씬에는 배치하지 않는다.</summary>
    public sealed class MvpHudSample : MonoBehaviour
    {
        [SerializeField] private GameUIController _ui;
        [SerializeField] private Button _addGoldButton;
        [SerializeField] private Button _winButton;
        [SerializeField] private Button _loseButton;
        [SerializeField] private Button _rejectButton;
        [SerializeField] private TMP_Text _statusText;

        private Coroutine _preparingRoutine;
        private int _gold;
        private int _wave;
        private int _earnedGold;
        private bool _isBattle;
        private bool _rejectNextStart;

        private void OnEnable()
        {
            _ui.WaveStartRequested += HandleWaveStartRequested;
            _ui.ContinueRequested += HandleContinueRequested;
            _ui.RestartRequested += HandleRestartRequested;
            _addGoldButton.onClick.AddListener(HandleAddGoldClicked);
            _winButton.onClick.AddListener(HandleWinClicked);
            _loseButton.onClick.AddListener(HandleLoseClicked);
            _rejectButton.onClick.AddListener(HandleRejectClicked);
        }

        private void Start()
        {
            ResetSample();
        }

        private void OnDisable()
        {
            _ui.WaveStartRequested -= HandleWaveStartRequested;
            _ui.ContinueRequested -= HandleContinueRequested;
            _ui.RestartRequested -= HandleRestartRequested;
            _addGoldButton.onClick.RemoveListener(HandleAddGoldClicked);
            _winButton.onClick.RemoveListener(HandleWinClicked);
            _loseButton.onClick.RemoveListener(HandleLoseClicked);
            _rejectButton.onClick.RemoveListener(HandleRejectClicked);
            if (_preparingRoutine != null) StopCoroutine(_preparingRoutine);
            _preparingRoutine = null;
        }

        private void HandleWaveStartRequested()
        {
            if (_rejectNextStart)
            {
                _rejectNextStart = false;
                _ui.ShowMessage("테스트: 시작 요청이 거절되었습니다. 다시 시도하세요.");
                _ui.SetWaveStartInteractable(true);
                _statusText.text = "거절 응답 완료 · 다시 시작할 수 있습니다";
                return;
            }

            _ui.HideMessage();
            _ui.SetWaveStartInteractable(false);
            _ui.SetPhaseLabel("전투 준비");
            _rejectButton.interactable = false;
            _statusText.text = "시작 요청 처리 중 · 중복 입력 잠금";
            _preparingRoutine = StartCoroutine(PrepareBattle());
        }

        private void HandleContinueRequested()
        {
            _wave++;
            _ui.HideWaveReward();
            EnterPreparation();
        }

        private void HandleRestartRequested()
        {
            ResetSample();
        }

        private void HandleAddGoldClicked()
        {
            _gold += 50;
            _ui.SetGold(_gold);
        }

        private void HandleWinClicked()
        {
            if (!_isBattle) return;
            EndBattle();
            _gold += 30;
            _earnedGold += 30;
            _ui.SetGold(_gold);
            if (_wave < 3)
            {
                _ui.SetPhaseLabel("보상");
                _ui.ShowWaveReward(30, true);
            }
            else
            {
                _ui.SetPhaseLabel("결과");
                _ui.ShowRunResult(true, _earnedGold);
            }
        }

        private void HandleLoseClicked()
        {
            if (!_isBattle) return;
            EndBattle();
            _ui.SetPhaseLabel("결과");
            _ui.ShowRunResult(false, _earnedGold);
        }

        private void HandleRejectClicked()
        {
            _rejectNextStart = true;
            _statusText.text = "다음 시작 요청은 거절됩니다 · 웨이브 시작을 눌러보세요";
        }

        private IEnumerator PrepareBattle()
        {
            yield return new WaitForSecondsRealtime(0.6f);
            _preparingRoutine = null;
            _isBattle = true;
            _ui.SetPhaseLabel("전투");
            _winButton.interactable = true;
            _loseButton.interactable = true;
            _statusText.text = "전투 표시 중 · 아래 버튼으로 결과 데이터를 전달하세요";
        }

        private void ResetSample()
        {
            if (_preparingRoutine != null) StopCoroutine(_preparingRoutine);
            _preparingRoutine = null;
            _gold = 100;
            _wave = 1;
            _earnedGold = 0;
            _rejectNextStart = false;
            _ui.Initialize();
            _ui.SetGold(_gold);
            EnterPreparation();
        }

        private void EnterPreparation()
        {
            _isBattle = false;
            _ui.SetWaveProgress(_wave, 3);
            _ui.SetPhaseLabel("건설");
            _ui.SetWaveStartInteractable(true);
            _winButton.interactable = false;
            _loseButton.interactable = false;
            _rejectButton.interactable = true;
            _statusText.text = "건설 표시 중 · 웨이브 시작으로 진행하세요";
        }

        private void EndBattle()
        {
            _isBattle = false;
            _winButton.interactable = false;
            _loseButton.interactable = false;
            _statusText.text = "결과 표시 중 · 실제 판정 및 보상 계산은 담당 시스템의 역할입니다";
        }
    }
}
