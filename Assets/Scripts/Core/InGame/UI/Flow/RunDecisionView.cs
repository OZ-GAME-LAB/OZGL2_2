using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI.InGame
{
    /// <summary>분기 안내를 표시하고 종료 또는 계속 도전 의도를 반환한다.</summary>
    [DisallowMultipleComponent]
    public sealed class RunDecisionView : MonoBehaviour, IRunDecisionUI
    {
        public bool IsVisible => _screen != null && _screen.IsVisible;
        public bool IsPending => _pending != null;

        [SerializeField] private UIScreen _screen;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private UnityEngine.UI.Button _finishButton;
        [SerializeField] private UnityEngine.UI.Button _continueButton;

        private UniTaskCompletionSource<RunDecision> _pending;

        public async UniTask<RunDecision> ChooseAsync(int quarter, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (quarter < 1) throw new ArgumentOutOfRangeException(nameof(quarter));
            if (_pending != null) throw new InvalidOperationException("A run decision is already in progress.");
            if (!isActiveAndEnabled || _screen == null || _screen.Manager == null ||
                _descriptionText == null || _finishButton == null || _continueButton == null ||
                _finishButton == _continueButton)
                throw new InvalidOperationException("Run decision UI is not available.");

            var completion = new UniTaskCompletionSource<RunDecision>();
            _pending = completion;
            bool completed = false;
            UnityEngine.Events.UnityAction finish = () => Complete(completion, RunDecision.Finish, _finishButton);
            UnityEngine.Events.UnityAction proceed = () => Complete(completion, RunDecision.Continue, _continueButton);
            Action<UIScreen, UICloseReason> closed = (_, __) =>
            {
                if (ReferenceEquals(_pending, completion)) completion.TrySetCanceled();
            };
            try
            {
                _descriptionText.text = $"{quarter}분기 돌파!\n승리로 마무리하거나 다음 분기에 도전할 수 있습니다.";
                SetButtons(true);
                _finishButton.onClick.AddListener(finish);
                _continueButton.onClick.AddListener(proceed);
                _screen.Closed += closed;
                if (!_screen.Manager.ReplacePopup(_screen.Id) || !_screen.IsVisible)
                    throw new InvalidOperationException("Run decision UI could not be opened.");
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(_continueButton.gameObject);
                RunDecision result;
                using (token.Register(() => completion.TrySetCanceled(token)))
                    result = await completion.Task;
                completed = true;
                return result;
            }
            finally
            {
                await UniTask.SwitchToMainThread();
                if (ReferenceEquals(_pending, completion))
                {
                    if (_finishButton != null) _finishButton.onClick.RemoveListener(finish);
                    if (_continueButton != null) _continueButton.onClick.RemoveListener(proceed);
                    SetButtons(false);
                    if (_screen != null)
                    {
                        _screen.Closed -= closed;
                        _screen.Manager?.ClosePopup(_screen.Id,
                            completed ? UICloseReason.Completed : UICloseReason.ContextLost);
                    }
                    _pending = null;
                }
            }
        }

        private void Complete(UniTaskCompletionSource<RunDecision> completion, RunDecision choice,
            UnityEngine.UI.Button button)
        {
            if (!ReferenceEquals(_pending, completion) || !isActiveAndEnabled || !IsVisible || !button.IsInteractable()) return;
            SetButtons(false);
            completion.TrySetResult(choice);
        }

        private void SetButtons(bool interactable)
        {
            if (_finishButton != null) _finishButton.interactable = interactable;
            if (_continueButton != null) _continueButton.interactable = interactable;
        }

        private void OnDisable() => _pending?.TrySetCanceled();
        private void OnDestroy() => _pending?.TrySetCanceled();
    }
}
