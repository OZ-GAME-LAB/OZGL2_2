using System;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.InGame
{
    /// <summary>Core의 분기 종료 요청을 표시하고 종료·계속 입력을 전달한다.</summary>
    [DisallowMultipleComponent]
    public sealed class RunDecisionView : MonoBehaviour
    {
        public bool IsVisible => _screen != null && _screen.IsVisible;

        [SerializeField] private UIScreen _screen;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private Button _finishButton;
        [SerializeField] private Button _continueButton;

        private GameFlowController _flow;
        private WaveController _waves;
        private bool _isSubscribed;

        private void OnEnable() => Bind();
        private void OnDisable() => Unbind();

        public void Initialize(GameFlowController flow, WaveController waves)
        {
            if (flow == null) throw new ArgumentNullException(nameof(flow));
            if (waves == null) throw new ArgumentNullException(nameof(waves));
            if (!HasView()) throw new InvalidOperationException("Run decision view references are incomplete.");
            Unbind();
            _flow = flow;
            _waves = waves;
            if (isActiveAndEnabled) Bind();
        }

        public void Refresh()
        {
            bool canChoose = _isSubscribed && _flow != null && _waves != null &&
                _flow.CurPhase == GamePhase.QuarterComplete && _flow.CanChooseRunDecision;
            if (!canChoose)
            {
                Hide();
                return;
            }

            string text = $"{_waves.CurQuarter}분기 돌파!\n승리로 마무리하거나 다음 분기에 도전할 수 있습니다.";
            if (_descriptionText.text != text) _descriptionText.text = text;
            _finishButton.interactable = true;
            _continueButton.interactable = true;
            _screen.Show();
        }

        private void HandlePhaseChanged(GamePhase phase) => Refresh();

        private void HandleFinishClicked()
        {
            if (!CanSubmit(_finishButton)) return;
            _flow.ChooseFinishRun();
            Refresh();
        }

        private void HandleContinueClicked()
        {
            if (!CanSubmit(_continueButton)) return;
            _flow.ChooseContinueRun();
            Refresh();
        }

        private bool CanSubmit(Button button) =>
            isActiveAndEnabled && _isSubscribed && IsVisible && button != null && button.IsInteractable() &&
            _flow != null && _flow.CurPhase == GamePhase.QuarterComplete && _flow.CanChooseRunDecision;

        private bool HasView() => _screen != null && _descriptionText != null &&
            _finishButton != null && _continueButton != null && _finishButton != _continueButton;

        private void Bind()
        {
            if (_isSubscribed || _flow == null || _waves == null || !HasView()) return;
            _flow.PhaseChanged += HandlePhaseChanged;
            _flow.QuarterDecisionRequested += Refresh;
            _finishButton.onClick.AddListener(HandleFinishClicked);
            _continueButton.onClick.AddListener(HandleContinueClicked);
            _isSubscribed = true;
            Refresh();
        }

        private void Unbind()
        {
            if (_isSubscribed)
            {
                if (_flow != null)
                {
                    _flow.PhaseChanged -= HandlePhaseChanged;
                    _flow.QuarterDecisionRequested -= Refresh;
                }
                if (_finishButton != null) _finishButton.onClick.RemoveListener(HandleFinishClicked);
                if (_continueButton != null) _continueButton.onClick.RemoveListener(HandleContinueClicked);
            }
            _isSubscribed = false;
            Hide();
        }

        private void Hide()
        {
            if (_finishButton != null) _finishButton.interactable = false;
            if (_continueButton != null) _continueButton.interactable = false;
            if (_screen != null) _screen.Hide();
        }
    }
}
