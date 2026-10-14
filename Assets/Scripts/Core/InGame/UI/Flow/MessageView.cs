using TMPro;
using UnityEngine;

namespace Game.UI.InGame
{
    /// <summary>게임 규칙 없이 전달된 안내 문구만 표시한다.</summary>
    public sealed class MessageView : MonoBehaviour
    {
        [SerializeField] private UIScreen _screen;
        [SerializeField] private TMP_Text _messageText;

        public void Show(string message)
        {
            if (_messageText != null) _messageText.text = message ?? string.Empty;
            if (string.IsNullOrWhiteSpace(message)) Hide();
            else if (_screen != null && !_screen.IsVisible) _screen.Manager?.ReplacePopup(_screen.Id);
        }

        public void Hide()
        {
            if (_screen != null) _screen.Manager?.ClosePopup(_screen.Id, UICloseReason.ContextLost);
        }
    }
}
