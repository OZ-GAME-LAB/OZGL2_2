using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game.UI.InGame
{
    /// <summary>건설 화면의 표시·입력·팝업 수명만 담당한다. 게임 상태는 연결부가 전달한다.</summary>
    [DisallowMultipleComponent]
    public sealed class BuildingUIPresenter : MonoBehaviour
    {
        [SerializeField] private BuildingCatalogView _catalog;
        [SerializeField] private BuildingInfoView _info;
        [SerializeField] private BuildingActionView _actions;
        [SerializeField] private TMP_Text _feedbackText;

        public event Action<string> CandidateSelected;
        public event Action<BuildingActionRequest> ActionRequested;
        public event Action Closed;
        public bool IsReady => _catalog != null && _catalog.Popup != null && _info != null &&
            _info.Popup != null && _actions != null;
        public bool IsCatalogVisible => _catalog != null && _catalog.Popup != null && _catalog.Popup.IsVisible;

        private string _selectionId;
        private bool _closing;

        private void OnEnable()
        {
            if (_catalog != null)
            {
                _catalog.ItemSelected += HandleCandidateSelected;
                if (_catalog.Popup != null) _catalog.Popup.Closed += HandleCatalogClosed;
            }
            if (_info != null && _info.Popup != null) _info.Popup.Closed += HandleDetailsClosed;
            if (_actions != null) _actions.ActionRequested += HandleActionRequested;
        }

        private void OnDisable()
        {
            if (_catalog != null)
            {
                _catalog.ItemSelected -= HandleCandidateSelected;
                if (_catalog.Popup != null) _catalog.Popup.Closed -= HandleCatalogClosed;
            }
            if (_info != null && _info.Popup != null) _info.Popup.Closed -= HandleDetailsClosed;
            if (_actions != null) _actions.ActionRequested -= HandleActionRequested;
            Hide();
            // 연결부가 Systems 아래에 있어 화면과 함께 꺼지지 않는 경우에도 선택을 끝낸다.
            Closed?.Invoke();
        }

        public void ShowCatalog(string selectionId, IReadOnlyList<BuildingCatalogItem> items)
        {
            bool sameSelection = _selectionId == selectionId;
            _selectionId = selectionId;
            _catalog.SetItems(items, preservePage: sameSelection);
            if (!_catalog.Popup.IsVisible) _catalog.Popup.Manager?.ReplacePopup(_catalog.Popup.Id);
        }

        public void ShowDetails(BuildingInfoData info, BuildingActionViewData actions, bool allowed, string reason)
        {
            _selectionId = info.SelectionId;
            _info.ShowBuildingInfo(info);
            _actions.SetActionsAllowed(allowed, reason);
            _actions.ShowActions(actions);
            UIScreen popup = _info.Popup;
            if (popup != null && !popup.IsVisible) popup.Manager?.ReplacePopup(popup.Id);
        }

        public void SetActionsAllowed(bool allowed, string reason) => _actions.SetActionsAllowed(allowed, reason);
        public void ResolveRequest(Guid requestId, bool succeeded, string message) =>
            _actions.TryResolveRequest(requestId, succeeded, message);

        public void ShowFeedback(string message)
        {
            if (_feedbackText != null) _feedbackText.text = message;
        }

        public void Hide()
        {
            if (_closing) return;
            _closing = true;
            try
            {
                ClearDisplay();
                if (_catalog != null && _catalog.Popup != null)
                    _catalog.Popup.Manager?.ClosePopup(_catalog.Popup.Id, UICloseReason.ContextLost);
                if (_info != null && _info.Popup != null)
                    _info.Popup.Manager?.ClosePopup(_info.Popup.Id, UICloseReason.ContextLost);
            }
            finally { _closing = false; }
        }

        private void ClearDisplay()
        {
            _selectionId = null;
            if (_info != null) _info.HideBuildingInfo();
            if (_actions != null) _actions.HideActions();
        }

        private void HandleCandidateSelected(string id) => CandidateSelected?.Invoke(id);
        private void HandleActionRequested(BuildingActionRequest request) => ActionRequested?.Invoke(request);

        private void HandleCatalogClosed(UIScreen screen, UICloseReason reason)
        {
            // 목록에서 정보로 교체하는 동안에는 선택 문맥을 유지한다.
            if (_closing || reason == UICloseReason.Replaced) return;
            ClearDisplay();
            Closed?.Invoke();
        }

        private void HandleDetailsClosed(UIScreen screen, UICloseReason reason)
        {
            if (_closing) return;
            ClearDisplay();
            Closed?.Invoke();
        }
    }
}
