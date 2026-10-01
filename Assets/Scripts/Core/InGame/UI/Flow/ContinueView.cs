using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI.InGame
{
    /// <summary>안내 한 건을 표시하고 확인 입력만 반환한다.</summary>
    [DisallowMultipleComponent]
    public sealed class ContinueView : MonoBehaviour, IContinueUI
    {
        [SerializeField] private UIScreen _screen;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private UnityEngine.UI.Button _continueButton;

        private UniTaskCompletionSource _pending;
        public bool IsPending => _pending != null;

        public async UniTask ShowAsync(string message, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (_pending != null) throw new InvalidOperationException("A continue request is already in progress.");
            if (!isActiveAndEnabled || _screen == null || _screen.Manager == null ||
                _messageText == null || _continueButton == null)
                throw new InvalidOperationException("Continue UI is not available.");

            var completion = new UniTaskCompletionSource();
            _pending = completion;
            bool completed = false;
            UnityEngine.Events.UnityAction confirm = () => HandleContinue(completion);
            Action<UIScreen, UICloseReason> closed = (_, __) =>
            {
                if (ReferenceEquals(_pending, completion)) completion.TrySetCanceled();
            };
            try
            {
                _messageText.text = message ?? string.Empty;
                _continueButton.gameObject.SetActive(true);
                _continueButton.interactable = true;
                _continueButton.onClick.AddListener(confirm);
                _screen.Closed += closed;
                if (!_screen.Manager.ReplacePopup(_screen.Id) || !_screen.IsVisible)
                    throw new InvalidOperationException("Continue UI could not be opened.");
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(_continueButton.gameObject);
                using (token.Register(() => completion.TrySetCanceled(token)))
                    await completion.Task;
                completed = true;
            }
            finally
            {
                await UniTask.SwitchToMainThread();
                if (ReferenceEquals(_pending, completion))
                {
                    if (_continueButton != null)
                    {
                        _continueButton.onClick.RemoveListener(confirm);
                        _continueButton.interactable = false;
                    }
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

        private void HandleContinue(UniTaskCompletionSource completion)
        {
            if (!ReferenceEquals(_pending, completion) || !isActiveAndEnabled ||
                !_screen.IsVisible || !_continueButton.IsInteractable()) return;
            _continueButton.interactable = false;
            completion.TrySetResult();
        }

        private void OnDisable() => _pending?.TrySetCanceled();
        private void OnDestroy() => _pending?.TrySetCanceled();
    }
}
