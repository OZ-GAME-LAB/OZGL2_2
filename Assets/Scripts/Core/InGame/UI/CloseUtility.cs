using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.InGame
{
    [RequireComponent(typeof(Button))]
    public sealed class CloseUtility : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private UIScreen _screen;
        private void Awake() { if (_button == null) _button = GetComponent<Button>(); }
        private void OnEnable() { if (_button != null) _button.onClick.AddListener(Close); }
        private void OnDisable() { if (_button != null) _button.onClick.RemoveListener(Close); }
        public void Close()
        {
            if (_screen != null && _screen.Manager != null)
                _screen.Manager.ClosePopup(_screen.Id, UICloseReason.UserCancel);
        }
    }
}
