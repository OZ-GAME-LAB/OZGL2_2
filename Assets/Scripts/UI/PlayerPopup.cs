using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>선택형 정보 창의 표시/접기/포커스만 관리한다. 게임 작업을 취소하지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerPopup : MonoBehaviour
    {
        public event Action<PlayerPopup> Shown;
        public event Action<PlayerPopup> Closed;
        public bool IsVisible => _root != null && _root.activeInHierarchy;
        public bool IsExpanded { get; private set; }
        public RectTransform Window => _window;

        [SerializeField] private GameObject _root;
        [SerializeField] private RectTransform _window;
        [SerializeField] private Button[] _closeButtons = Array.Empty<Button>();
        [SerializeField] private Selectable _initialFocus;
        [SerializeField] private Button _detailsButton;
        [SerializeField] private TMP_Text _detailsLabel;
        [SerializeField] private GameObject _detailsRoot;
        [SerializeField] private float _collapsedHeight = 300;
        [SerializeField] private float _expandedHeight = 440;

        private GameObject _previousSelection;

        private void OnEnable()
        {
            foreach (var button in _closeButtons) if (button != null) button.onClick.AddListener(Hide);
            if (_detailsButton != null) _detailsButton.onClick.AddListener(HandleDetails);
        }

        private void OnDisable()
        {
            foreach (var button in _closeButtons) if (button != null) button.onClick.RemoveListener(Hide);
            if (_detailsButton != null) _detailsButton.onClick.RemoveListener(HandleDetails);
            Hide();
        }

        public void Show()
        {
            if (_root == null || !isActiveAndEnabled || IsVisible) return;
            var events = EventSystem.current;
            _previousSelection = events != null ? events.currentSelectedGameObject : null;
            SetExpanded(false);
            _root.SetActive(true);
            Shown?.Invoke(this);
            if (IsVisible && events != null && _initialFocus != null && _initialFocus.IsInteractable())
                events.SetSelectedGameObject(_initialFocus.gameObject);
        }

        public void Hide()
        {
            if (_root == null || !_root.activeSelf) return;
            var events = EventSystem.current;
            var selection = events != null ? events.currentSelectedGameObject : null;
            bool ownsFocus = selection == null || selection.transform.IsChildOf(_root.transform);
            _root.SetActive(false);
            SetExpanded(false);
            Closed?.Invoke(this);
            if (ownsFocus && events != null)
            {
                var previous = _previousSelection != null ? _previousSelection.GetComponent<Selectable>() : null;
                events.SetSelectedGameObject(previous != null && previous.isActiveAndEnabled && previous.IsInteractable()
                    ? previous.gameObject : null);
            }
            _previousSelection = null;
        }

        public void SetExpanded(bool expanded)
        {
            IsExpanded = expanded && _detailsRoot != null;
            if (_detailsRoot != null) _detailsRoot.SetActive(IsExpanded);
            if (_detailsLabel != null) _detailsLabel.text = IsExpanded ? "접기" : "자세히";
            if (_window != null)
                _window.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, IsExpanded ? _expandedHeight : _collapsedHeight);
        }

        public void SetContentHeights(float collapsed, float expanded)
        {
            if (collapsed <= 0 || expanded < collapsed) throw new ArgumentOutOfRangeException(nameof(collapsed));
            _collapsedHeight = collapsed;
            _expandedHeight = expanded;
            SetExpanded(IsExpanded);
        }

        private void HandleDetails() => SetExpanded(!IsExpanded);
    }
}
