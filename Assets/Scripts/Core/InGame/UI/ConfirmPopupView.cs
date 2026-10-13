using System;
using TMPro;
using UnityEngine;

namespace Game.UI.InGame
{
    // 확인할 문구와 동작만 받아 일시정지창 위에 표시한다.
    public sealed class ConfirmPopupView : MonoBehaviour
    {
        [SerializeField] private UIScreen _screen;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private UnityEngine.UI.Button _confirmButton;

        private Action _onConfirm;

        private void OnEnable() => _confirmButton.onClick.AddListener(Confirm);

        private void OnDisable()
        {
            _confirmButton.onClick.RemoveListener(Confirm);
            _onConfirm = null;
        }

        public void Show(string message, Action onConfirm)
        {
            _messageText.text = message;
            _onConfirm = onConfirm;
            _screen.Manager.OpenPopup(_screen.Id);
        }

        private void Confirm()
        {
            var onConfirm = _onConfirm;
            _onConfirm = null;
            _screen.Manager.ClosePopup(_screen.Id, UICloseReason.Completed);
            onConfirm?.Invoke();
        }
    }
}
