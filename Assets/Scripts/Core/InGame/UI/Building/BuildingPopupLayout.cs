using System;
using TMPro;
using UnityEngine;

namespace Game.UI.InGame
{
    /// <summary>건설 창의 자세히·접기와 행동 행 수에 따른 높이를 관리한다.</summary>
    public sealed class BuildingPopupLayout : MonoBehaviour
    {
        [SerializeField] private UIScreen _popup;
        [SerializeField] private RectTransform _window;
        [SerializeField] private UnityEngine.UI.Button _detailsButton;
        [SerializeField] private TMP_Text _detailsLabel;
        [SerializeField] private GameObject _detailsRoot;
        [SerializeField] private float _collapsedHeight = 300;
        [SerializeField] private float _expandedHeight = 440;
        [SerializeField] private RectTransform[] _rows = Array.Empty<RectTransform>();
        [SerializeField] private RectTransform _status;
        [SerializeField] private RectTransform _details;
        private int _visibleRows = -1;

        public bool IsExpanded { get; private set; }

        private void OnEnable()
        {
            if (_popup != null) _popup.Closed += HandleClosed;
            if (_detailsButton != null) _detailsButton.onClick.AddListener(HandleDetails);
            _visibleRows = -1;
            SetExpanded(false);
        }

        private void OnDisable()
        {
            if (_popup != null) _popup.Closed -= HandleClosed;
            if (_detailsButton != null) _detailsButton.onClick.RemoveListener(HandleDetails);
        }

        private void LateUpdate()
        {
            int count = 0;
            foreach (RectTransform row in _rows)
            {
                if (row == null || !row.gameObject.activeSelf) continue;
                row.anchoredPosition = new Vector2(row.anchoredPosition.x, -182 - count * 66);
                count++;
            }

            if (count == _visibleRows) return;
            _visibleRows = count;
            float height = count == 0 ? 290 : 320 + count * 66;
            SetContentHeights(height, height + 180);
            if (_status != null) _status.anchoredPosition = new Vector2(_status.anchoredPosition.x, -182 - count * 66);
            if (_details != null) _details.anchoredPosition = new Vector2(_details.anchoredPosition.x, -(height - 70 + 10));
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
        private void HandleClosed(UIScreen screen, UICloseReason reason) => SetExpanded(false);
    }
}
