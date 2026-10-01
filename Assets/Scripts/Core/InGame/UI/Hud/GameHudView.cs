using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.InGame
{
    public sealed class GameHudView : MonoBehaviour
    {
        public event Action WaveStartRequested;
        public event Action ContinueRequested;
        public event Action RestartRequested;

        [Header("HUD")]
        [SerializeField] private TMP_Text _goldText;
        [SerializeField] private TMP_Text _waveText;
        [SerializeField] private TMP_Text _phaseText;
        [SerializeField] private Button _waveStartButton;

        [Header("Wave Reward")]
        [SerializeField] private UIScreen _waveRewardScreen;
        [SerializeField] private TMP_Text _waveRewardText;
        [SerializeField] private Button _continueButton;

        [Header("Run Result")]
        [SerializeField] private UIScreen _runResultScreen;
        [SerializeField] private TMP_Text _runResultTitleText;
        [SerializeField] private TMP_Text _runRewardText;
        [SerializeField] private Button _restartButton;
        [SerializeField] private SettlementView _settlementPanel;

        [Header("Message")]
        [SerializeField] private UIScreen _messageScreen;
        [SerializeField] private TMP_Text _messageText;

        [Header("Display Copy")]
        [SerializeField] private string _waveRewardFormat = "골드 +{0:N0}";
        [SerializeField] private string _runRewardFormat = "획득 골드 {0:N0}";
        [SerializeField] private string _victoryTitle = "승리";
        [SerializeField] private string _defeatTitle = "패배";

        private bool _isWaveStartRequestPending;
        private bool _isContinueRequestPending;
        private bool _isRestartRequestPending;

        private void OnEnable()
        {
            AddButtonListeners();
        }

        private void OnDisable()
        {
            RemoveButtonListeners();
        }

        public void Initialize()
        {
            _isWaveStartRequestPending = false;
            _isContinueRequestPending = false;
            _isRestartRequestPending = false;
            SetTextIfAssigned(_goldText, "--");
            SetTextIfAssigned(_waveText, "-- / --");
            SetTextIfAssigned(_phaseText, "연결 대기");
            SetWaveStartInteractable(false);

            SetVisibleIfAssigned(_waveRewardScreen, false);
            SetVisibleIfAssigned(_runResultScreen, false);
            SetVisibleIfAssigned(_messageScreen, false);
        }

        public void SetGold(int gold)
        {
            if (gold < 0) throw new ArgumentOutOfRangeException(nameof(gold));
            SetTextIfAssigned(_goldText, $"{gold:N0}");
        }

        public void ClearGold()
        {
            SetTextIfAssigned(_goldText, "--");
        }

        public void ClearWaveProgress()
        {
            SetTextIfAssigned(_waveText, "-- / --");
        }

        public void SetWaveProgress(int currentWave, int totalWaves)
        {
            if (totalWaves < 1) throw new ArgumentOutOfRangeException(nameof(totalWaves));
            if (currentWave < 1 || currentWave > totalWaves)
                throw new ArgumentOutOfRangeException(nameof(currentWave));
            SetTextIfAssigned(_waveText, $"{currentWave} / {totalWaves}");
        }

        public void SetPhaseLabel(string phaseLabel)
        {
            SetTextIfAssigned(_phaseText, phaseLabel);
        }

        public void SetWaveStartInteractable(bool canStart)
        {
            _isWaveStartRequestPending = false;

            if (_waveStartButton != null)
            {
                _waveStartButton.interactable = canStart && WaveStartRequested != null;
                if (_waveStartButton.interactable) SelectButton(_waveStartButton);
            }
        }

        public void ShowWaveReward(int goldReward, bool hasNextWave)
        {
            if (goldReward < 0) throw new ArgumentOutOfRangeException(nameof(goldReward));
            ShowProgressPrompt(string.Format(_waveRewardFormat, goldReward), hasNextWave);
        }

        public void ShowProgressPrompt(string message, bool canContinue)
        {
            SetWaveStartInteractable(false);
            HideRunResult();
            _isContinueRequestPending = false;
            SetTextIfAssigned(_waveRewardText, message ?? string.Empty);
            SetVisibleIfAssigned(_waveRewardScreen, true);

            if (_continueButton != null)
            {
                _continueButton.gameObject.SetActive(canContinue);
                _continueButton.interactable = canContinue && ContinueRequested != null;
                if (_continueButton.interactable) SelectButton(_continueButton);
            }
        }

        public void HideWaveReward()
        {
            SetVisibleIfAssigned(_waveRewardScreen, false);
        }

        public void ShowRunResult(bool isVictory, int goldReward)
        {
            if (goldReward < 0) throw new ArgumentOutOfRangeException(nameof(goldReward));
            SetWaveStartInteractable(false);
            HideWaveReward();
            _isRestartRequestPending = false;
            SetTextIfAssigned(_runResultTitleText, isVictory ? _victoryTitle : _defeatTitle);
            SetTextIfAssigned(_runRewardText, string.Format(_runRewardFormat, goldReward));
            SetVisibleIfAssigned(_runResultScreen, true);
            if (_settlementPanel != null) _settlementPanel.ShowPending(isVictory);
            if (_restartButton != null)
            {
                _restartButton.interactable = RestartRequested != null;
                if (_restartButton.interactable) SelectButton(_restartButton);
            }
        }

        public void HideRunResult()
        {
            SetVisibleIfAssigned(_runResultScreen, false);
            if (_settlementPanel != null) _settlementPanel.Hide();
        }

        public void ShowMessage(string message)
        {
            SetTextIfAssigned(_messageText, message);
            SetVisibleIfAssigned(_messageScreen, !string.IsNullOrWhiteSpace(message));
        }

        public void HideMessage()
        {
            SetVisibleIfAssigned(_messageScreen, false);
        }

        private void HandleWaveStartButtonClicked()
        {
            if (!isActiveAndEnabled || _isWaveStartRequestPending || WaveStartRequested == null ||
                _waveStartButton == null || !_waveStartButton.IsInteractable())
            {
                return;
            }

            _isWaveStartRequestPending = true;

            if (_waveStartButton != null)
            {
                _waveStartButton.interactable = false;
            }

            WaveStartRequested.Invoke();
        }

        private void HandleContinueButtonClicked()
        {
            if (!isActiveAndEnabled || _isContinueRequestPending || ContinueRequested == null ||
                _continueButton == null || !_continueButton.IsInteractable() ||
                !_continueButton.gameObject.activeInHierarchy) return;
            _isContinueRequestPending = true;
            _continueButton.interactable = false;
            ContinueRequested.Invoke();
        }

        private void HandleRestartButtonClicked()
        {
            if (!isActiveAndEnabled || _isRestartRequestPending || RestartRequested == null ||
                _restartButton == null || !_restartButton.IsInteractable() ||
                !_restartButton.gameObject.activeInHierarchy) return;
            _isRestartRequestPending = true;
            _restartButton.interactable = false;
            RestartRequested.Invoke();
        }

        private void AddButtonListeners()
        {
            RemoveButtonListeners();
            if (_waveStartButton != null)
            {
                _waveStartButton.onClick.AddListener(HandleWaveStartButtonClicked);
            }

            if (_continueButton != null)
            {
                _continueButton.onClick.AddListener(HandleContinueButtonClicked);
            }

            if (_restartButton != null)
            {
                _restartButton.onClick.AddListener(HandleRestartButtonClicked);
            }
        }

        private void RemoveButtonListeners()
        {
            if (_waveStartButton != null)
            {
                _waveStartButton.onClick.RemoveListener(HandleWaveStartButtonClicked);
            }

            if (_continueButton != null)
            {
                _continueButton.onClick.RemoveListener(HandleContinueButtonClicked);
            }

            if (_restartButton != null)
            {
                _restartButton.onClick.RemoveListener(HandleRestartButtonClicked);
            }
        }

        private static void SetTextIfAssigned(TMP_Text text, string value)
        {
            if (text != null && text.text != value)
            {
                text.text = value;
            }
        }

        private static void SelectButton(Button button)
        {
            if (EventSystem.current != null && button.gameObject.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        private static void SetVisibleIfAssigned(UIScreen target, bool isActive)
        {
            if (target != null)
            {
                if (isActive) target.Show();
                else target.Hide();
            }
        }
    }
}
