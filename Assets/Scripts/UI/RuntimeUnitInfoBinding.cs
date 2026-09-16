using System;
using UnityEngine;

namespace Game.UI
{
    /// <summary>선택 입력과 UnitInfoPanel 사이의 연결. 선택/전투 판정 자체는 소유하지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class RuntimeUnitInfoBinding : MonoBehaviour
    {
        public string SelectionId => _selectionId;

        [SerializeField] private UnitInfoPanel _panel;

        private RuntimeUnitInfoSource _source;
        private string _selectionId;
        private bool _isSubscribed;

        private void OnEnable()
        {
            if (_panel == null) _panel = GetComponent<UnitInfoPanel>();
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void OnDestroy()
        {
            ClearSelection();
        }

        public void Initialize(UnitInfoPanel panel)
        {
            if (panel == null) throw new ArgumentNullException(nameof(panel));
            Unbind();
            ClearSelection();
            _panel = panel;
            if (isActiveAndEnabled) Bind();
        }

        /// <summary>스폰 주체의 Unit_Core.Initialize 이후 호출. 유효하지 않은 선택은 기존 화면을 유지한다.</summary>
        public bool TrySelect(RuntimeUnitInfoSource source)
        {
            if (!isActiveAndEnabled || _panel == null || !_panel.isActiveAndEnabled ||
                source == null || !source.TryGetInfo(out var data)) return false;
            _panel.ShowUnitInfo(data);
            UnsubscribeSource();
            _source = source;
            _selectionId = data.SelectionId;
            SubscribeSource();
            return true;
        }

        public void ClearSelection()
        {
            string previousId = _selectionId;
            UnsubscribeSource();
            _source = null;
            _selectionId = null;
            if (_panel != null) _panel.TryHideUnitInfo(previousId);
        }

        private void HandleInfoChanged(RuntimeUnitInfoSource source)
        {
            if (source == _source) RefreshSelection();
        }

        private void HandleUnavailable(RuntimeUnitInfoSource source)
        {
            if (source == _source) ClearSelection();
        }

        private void HandlePanelClosed(string selectionId)
        {
            if (selectionId == _selectionId) ClearSelection();
        }

        private void Bind()
        {
            if (_panel == null) return;
            _panel.InfoPanelClosed -= HandlePanelClosed;
            _panel.InfoPanelClosed += HandlePanelClosed;
            SubscribeSource();
            RefreshSelection();
        }

        private void Unbind()
        {
            if (_panel != null) _panel.InfoPanelClosed -= HandlePanelClosed;
            UnsubscribeSource();
        }

        private void SubscribeSource()
        {
            if (_isSubscribed || _source == null) return;
            _source.InfoChanged += HandleInfoChanged;
            _source.Unavailable += HandleUnavailable;
            _isSubscribed = true;
        }

        private void UnsubscribeSource()
        {
            if (_isSubscribed && _source != null)
            {
                _source.InfoChanged -= HandleInfoChanged;
                _source.Unavailable -= HandleUnavailable;
            }
            _isSubscribed = false;
        }

        private void RefreshSelection()
        {
            if (_selectionId == null) return;
            if (_panel == null || _panel.SelectionId != _selectionId || _source == null ||
                !_source.TryGetInfo(out var data) || data.SelectionId != _selectionId)
            {
                ClearSelection();
                return;
            }
            _panel.ShowUnitInfo(data);
        }
    }
}
