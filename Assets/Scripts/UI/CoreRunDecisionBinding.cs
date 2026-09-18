using System;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>코어가 요청한 분기 종료 선택만 표시·전달한다. 보상/승패/자동 진행을 결정하지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class CoreRunDecisionBinding : MonoBehaviour
    {
        public bool IsVisible => _panelRoot != null && _panelRoot.activeInHierarchy;

        [SerializeField] private GameFlowController _flow;
        [SerializeField] private WaveController _waves;
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private Button _finishButton;
        [SerializeField] private Button _continueButton;

        private GameObject _previousSelection;
        private bool _isSubscribed;

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

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

        /// <summary>숨김 중 도착한 코어 요청도 재활성화할 때 현재 상태로 복원한다.</summary>
        public void Refresh()
        {
            bool canChoose = _isSubscribed && _flow != null && _waves != null &&
                _flow.CurPhase == GamePhase.QuarterComplete && _flow.CanChooseRunDecision;
            if (!canChoose)
            {
                Hide();
                return;
            }

            bool wasVisible = IsVisible;
            string text = $"{_waves.CurQuarter}분기 돌파!\n승리로 마무리하거나 다음 분기에 도전할 수 있습니다.";
            if (_descriptionText.text != text) _descriptionText.text = text;
            if (!wasVisible && EventSystem.current != null)
                _previousSelection = EventSystem.current.currentSelectedGameObject;
            _panelRoot.SetActive(true);
            _finishButton.interactable = true;
            _continueButton.interactable = true;
            if (!wasVisible && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_finishButton.gameObject);
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            Refresh();
        }

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

        private bool HasView() => _panelRoot != null && _panelRoot != gameObject &&
            _panelRoot.transform.IsChildOf(transform) && _descriptionText != null &&
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
            if (_panelRoot == null) return;
            var events = EventSystem.current;
            var selected = events != null ? events.currentSelectedGameObject : null;
            bool ownsFocus = selected != null && selected.transform.IsChildOf(_panelRoot.transform);
            _panelRoot.SetActive(false);
            if (ownsFocus)
            {
                var previousButton = _previousSelection != null ? _previousSelection.GetComponent<Selectable>() : null;
                events.SetSelectedGameObject(previousButton != null && previousButton.isActiveAndEnabled &&
                    previousButton.IsInteractable() ? previousButton.gameObject : null);
            }
            _previousSelection = null;
        }
    }
}
