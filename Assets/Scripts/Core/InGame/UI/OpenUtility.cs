using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.InGame
{
    [RequireComponent(typeof(Button))]
    public sealed class OpenUtility : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private InGameUIManager _manager;
        [SerializeField] private UIId _target;
        private void Awake() { if (_button == null) _button = GetComponent<Button>(); }
        private void OnEnable() { if (_button != null) _button.onClick.AddListener(Open); }
        private void OnDisable() { if (_button != null) _button.onClick.RemoveListener(Open); }
        public void Initialize(InGameUIManager manager) => _manager = manager;
        public void Open()
        {
            if (_manager == null) return;
            _manager.OpenPopup(_target);
        }
    }
}
