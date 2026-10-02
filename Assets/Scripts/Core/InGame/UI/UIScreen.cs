using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI.InGame
{
    /// <summary>화면 하나의 표시와 입력만 담당한다. 순서와 닫기 정책은 매니저가 관리한다.</summary>
    public sealed class UIScreen : MonoBehaviour
    {
        [SerializeField] private UIId _id;
        [SerializeField] private bool _isHud;
        [SerializeField] private bool _canCloseByUser = true;
        [SerializeField] private bool _blocksHudInput;
        [SerializeField] private GameObject _root;
        [SerializeField] private CanvasGroup _inputGroup;

        public InGameUIManager Manager { get; private set; }
        public UIId Id => _id;
        public bool IsHud => _isHud;
        public bool CanCloseByUser => _canCloseByUser;
        public bool BlocksHudInput => _blocksHudInput;
        public GameObject Root => _root;
        public bool IsVisible => _root != null && _root.activeInHierarchy;
        public event Action<UIScreen, UICloseReason> Closed;

        internal bool Attach(InGameUIManager manager)
        {
            if (_root == null || _inputGroup == null)
            {
                Debug.LogError($"[InGameUI] {_id} 화면의 Root 또는 CanvasGroup 참조가 없습니다.", this);
                return false;
            }
            Manager = manager;
            foreach (OpenUtility utility in GetComponentsInChildren<OpenUtility>(true)) utility.Initialize(manager);
            return true;
        }

        internal void Display() => _root.SetActive(true);

        internal void Conceal(UICloseReason reason, bool notify)
        {
            SetInputEnabled(false);
            if (_root != null) _root.SetActive(false);
            if (notify) Closed?.Invoke(this, reason);
        }

        internal void SetInputEnabled(bool enabled)
        {
            if (_inputGroup != null)
            {
                _inputGroup.interactable = enabled;
                _inputGroup.blocksRaycasts = enabled;
            }
            if (enabled || _root == null || EventSystem.current == null) return;
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(_root.transform))
                EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
