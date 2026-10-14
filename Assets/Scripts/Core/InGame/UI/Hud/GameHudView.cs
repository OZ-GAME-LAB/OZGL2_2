using System;
using Game.Cameras;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.InGame
{
    /// <summary>HUD 수치와 웨이브 시작 입력만 표시한다.</summary>
    public sealed class GameHudView : MonoBehaviour
    {
        public event Action WaveStartRequested;
        
        [SerializeField] private TMP_Text _goldText;
        [SerializeField] private TMP_Text _waveText;
        [SerializeField] private TMP_Text _phaseText;
        [SerializeField] private TMP_Text _changeViewText;
        [SerializeField] private Button _waveStartButton;
        [SerializeField] private Button _changeViewButton;

        private InGameCameraController _cameraController;
        private bool _isWaveStartRequestPending;

        private void OnEnable()
        {
            if (_waveStartButton != null) _waveStartButton.onClick.AddListener(HandleWaveStartButtonClicked);
            if(_changeViewButton != null) _changeViewButton.onClick.AddListener(HandleChangeViewButtonClicked);
        }

        private void OnDisable()
        {
            if (_waveStartButton != null) _waveStartButton.onClick.RemoveListener(HandleWaveStartButtonClicked);
            if (_changeViewButton != null) _changeViewButton.onClick.RemoveListener(HandleChangeViewButtonClicked);
        }

        public void Initialize(InGameCameraController cameraController)
        {
            _cameraController = cameraController;
            _cameraController.FocusChanged += SetChangeViewText;
            SetChangeViewText(_cameraController.FocusField);
            _isWaveStartRequestPending = false;
            SetTextIfAssigned(_goldText, "--");
            SetTextIfAssigned(_waveText, "-- / --");
            SetTextIfAssigned(_phaseText, "연결 대기");
            SetWaveStartInteractable(false);
        }

        public void SetGold(int gold)
        {
            if (gold < 0) throw new ArgumentOutOfRangeException(nameof(gold));
            SetTextIfAssigned(_goldText, $"{gold:N0}");
        }

        public void ClearGold() => SetTextIfAssigned(_goldText, "--");
        public void ClearWaveProgress() => SetTextIfAssigned(_waveText, "-- / --");

        public void SetWaveProgress(int currentWave, int totalWaves)
        {
            if (totalWaves < 1) throw new ArgumentOutOfRangeException(nameof(totalWaves));
            if (currentWave < 1 || currentWave > totalWaves)
                throw new ArgumentOutOfRangeException(nameof(currentWave));
            SetTextIfAssigned(_waveText, $"{currentWave} / {totalWaves}");
        }

        public void SetPhaseLabel(string phaseLabel) => SetTextIfAssigned(_phaseText, phaseLabel);

        public void SetWaveStartInteractable(bool canStart)
        {
            _isWaveStartRequestPending = false;
            if (_waveStartButton == null) return;
            _waveStartButton.interactable = canStart && WaveStartRequested != null;
            if (_waveStartButton.interactable && EventSystem.current != null &&
                _waveStartButton.gameObject.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(_waveStartButton.gameObject);
        }

        private void HandleWaveStartButtonClicked()
        {
            if (!isActiveAndEnabled || _isWaveStartRequestPending || WaveStartRequested == null ||
                _waveStartButton == null || !_waveStartButton.IsInteractable()) return;
            _isWaveStartRequestPending = true;
            _waveStartButton.interactable = false;
            WaveStartRequested.Invoke();
        }

        private static void SetTextIfAssigned(TMP_Text text, string value)
        {
            if (text != null && text.text != value) text.text = value;
        }

        private void HandleChangeViewButtonClicked()
        {
            if (_cameraController == null) return;
            
            _cameraController.Toggle();
        }
        private void SetChangeViewText(FocusField field)
        {
            if (field == FocusField.Battlefield)
            {
                SetTextIfAssigned(_changeViewText, "기지로 이동");
                return;
            }
            SetTextIfAssigned(_changeViewText,"전장으로 이동");
        }
    }
}
