using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Units.UnitDatas;

namespace Game.UI.InGame
{
    /// <summary>좌측 패널의 모드와 그 위에 열린 상세 화면을 연결한다.</summary>
    public sealed class BuildingUIPresenter : MonoBehaviour
    {
        [SerializeField] private UIScreen _panel;
        [SerializeField] private GameObject _catalogRoot;
        [SerializeField] private GameObject _managementRoot;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _walletText;
        [SerializeField] private TMP_Text _feedbackText;
        [SerializeField] private BuildingCatalogView _catalog;
        [SerializeField] private BuildingInfoView _info;
        [SerializeField] private UnityEngine.UI.Button _currentInfoButton;
        [SerializeField] private BuildingInfoView _comparison;
        [SerializeField] private GameObject _comparisonRoot;
        [SerializeField] private TMP_Text _upgradeHint;
        [SerializeField] private Transform _upgradeContent;
        [SerializeField] private BuildingCardView _upgradeTemplate;
        [SerializeField] private BuildingActionView _actions;

        [Header("상세 팝업")]
        [SerializeField] private UIScreen _detailScreen;
        [SerializeField] private BuildingInfoView _detailInfo;
        [SerializeField] private Transform _previewContent;
        [SerializeField] private BuildingCardView _previewTemplate;
        [SerializeField] private TMP_Text _previewHint;
        [SerializeField] private UnityEngine.UI.Button _detailBack;
        [SerializeField] private UnitPopupView _unitPopup;

        public event Action<string> CandidateSelected;
        public event Action<string> InfoRequested;
        public event Action PreviewBackRequested;
        public event Action PreviewClosed;
        public event Action<BuildingActionRequest> ActionRequested;
        public event Action Closed;

        public bool IsReady => _panel != null && _catalog != null && _info != null && _actions != null && _detailScreen != null;
        public bool IsCatalogVisible => _panel.IsVisible && _catalogRoot.activeSelf;

        private string _selectionId;
        private string _currentBuildingId;
        private bool _closing;
        private bool _requestPending;
        private readonly List<BuildingCatalogItem> _catalogItems = new List<BuildingCatalogItem>();
        private readonly List<BuildingCardView> _upgradeCards = new List<BuildingCardView>();
        private readonly List<BuildingCardView> _previewCards = new List<BuildingCardView>();

        private void OnEnable()
        {
            _catalog.ItemSelected += HandleBuild;
            _catalog.InfoRequested += HandleInfo;
            _actions.ActionRequested += HandleAction;
            _panel.Closed += HandlePanelClosed;
            _detailScreen.Closed += HandlePreviewClosed;
            _detailBack.onClick.AddListener(HandleBack);
            _currentInfoButton.onClick.AddListener(HandleCurrentInfo);
            _info.UnitSelected += HandleUnit;
            _comparison.UnitSelected += HandleUnit;
            _detailInfo.UnitSelected += HandleUnit;
        }

        private void OnDisable()
        {
            _catalog.ItemSelected -= HandleBuild;
            _catalog.InfoRequested -= HandleInfo;
            _actions.ActionRequested -= HandleAction;
            _panel.Closed -= HandlePanelClosed;
            _detailScreen.Closed -= HandlePreviewClosed;
            _detailBack.onClick.RemoveListener(HandleBack);
            _currentInfoButton.onClick.RemoveListener(HandleCurrentInfo);
            _info.UnitSelected -= HandleUnit;
            _comparison.UnitSelected -= HandleUnit;
            _detailInfo.UnitSelected -= HandleUnit;
            Hide();
            Closed?.Invoke();
        }

        public void SetWallet(int gold, int gems)
        {
            _walletText.text = $"골드 {gold:N0}   ·   보석 {gems:N0}";
        }

        public void ShowCatalog(string selectionId, IReadOnlyList<BuildingCatalogItem> items)
        {
            bool sameSelection = _selectionId == selectionId;
            _selectionId = selectionId;
            _titleText.text = "건물 건설";
            _managementRoot.SetActive(false);
            _catalogRoot.SetActive(true);
            _catalogItems.Clear();
            _catalogItems.AddRange(items);
            _catalog.SetItems(items, sameSelection);
            if (!_panel.IsVisible) _panel.Manager.OpenPopup(_panel.Id);
        }

        public void ShowManagement(BuildingInfoData info, BuildingActionViewData actions,
            IReadOnlyList<BuildingCatalogItem> upgrades, BuildingInfoData comparison, bool allowed, string reason)
        {
            _selectionId = info.SelectionId;
            _titleText.text = "건물 관리";
            _catalogRoot.SetActive(false);
            _managementRoot.SetActive(true);
            _info.ShowBuildingInfo(info);
            _currentBuildingId = info.BuildingId;
            _comparisonRoot.SetActive(comparison != null);
            if (comparison != null) _comparison.ShowBuildingInfo(comparison);
            _upgradeHint.text = upgrades.Count == 0 ? "현재 가능한 업그레이드가 없습니다." : "업그레이드할 건물을 선택하세요.";
            RenderCards(_upgradeCards, _upgradeTemplate, _upgradeContent, upgrades,
                actions.Upgrade?.OptionId, HandleCandidate);
            _actions.SetActionsAllowed(allowed, reason);
            _actions.ShowActions(actions);
            if (!_panel.IsVisible) _panel.Manager.OpenPopup(_panel.Id);
        }

        // 기존 샘플에서도 현재 건물 정보 표시를 사용할 수 있다.
        public void ShowDetails(BuildingInfoData info, BuildingActionViewData actions, bool allowed, string reason)
        {
            ShowManagement(info, actions, Array.Empty<BuildingCatalogItem>(), null, allowed, reason);
        }

        public void ShowBuildingPreview(BuildingInfoData info, IReadOnlyList<BuildingCatalogItem> upgrades, bool canGoBack)
        {
            _detailInfo.ShowBuildingInfo(info);
            _detailBack.gameObject.SetActive(canGoBack);
            _previewHint.text = upgrades.Count == 0 ? "최종 건물입니다." : "업그레이드 경로 · 버튼을 누르면 상세 정보를 확인합니다.";
            RenderCards(_previewCards, _previewTemplate, _previewContent, upgrades, null, HandleInfo);
            _detailScreen.Manager.OpenPopup(_detailScreen.Id);
        }

        private void RenderCards(List<BuildingCardView> cards, BuildingCardView template, Transform content,
            IReadOnlyList<BuildingCatalogItem> items, string selectedId, Action<string> onSelected)
        {
            while (cards.Count < items.Count) cards.Add(Instantiate(template, content));
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].gameObject.SetActive(i < items.Count);
                if (i < items.Count) cards[i].Bind(items[i], items[i].Id == selectedId, true, onSelected, HandleInfo);
            }
        }

        public void SetActionsAllowed(bool allowed, string reason) => _actions.SetActionsAllowed(allowed, reason);

        public void ResolveRequest(Guid requestId, bool succeeded, string message)
        {
            _requestPending = false;
            _actions.TryResolveRequest(requestId, succeeded, message);
        }

        public void ShowFeedback(string message)
        {
            _feedbackText.text = message ?? string.Empty;
        }

        public void Hide()
        {
            if (_closing) return;
            _closing = true;
            _selectionId = null;
            _catalogItems.Clear();
            _actions.HideActions();
            _feedbackText.text = string.Empty;
            // 부모를 닫으면 UIManager가 그 위의 유닛·스킬 팝업도 함께 닫는다.
            if (_panel.Manager != null) _panel.Manager.ClosePopup(_panel.Id, UICloseReason.ContextLost);
            _closing = false;
        }

        private void HandleBuild(string id)
        {
            if (_requestPending || _selectionId == null) return;
            foreach (BuildingCatalogItem item in _catalogItems)
            {
                if (item.Id != id || item.Offer == null || !item.Offer.CanExecute) continue;
                HandleAction(new BuildingActionRequest(_selectionId, item.Offer));
                return;
            }
        }

        private void HandleAction(BuildingActionRequest request)
        {
            if (_requestPending) return;
            _requestPending = true;
            try { ActionRequested?.Invoke(request); }
            finally { _requestPending = false; }
        }

        private void HandleCandidate(string id) => CandidateSelected?.Invoke(id);
        private void HandleInfo(string id) => InfoRequested?.Invoke(id);
        private void HandleBack() => PreviewBackRequested?.Invoke();
        private void HandleCurrentInfo() => InfoRequested?.Invoke(_currentBuildingId);
        private void HandleUnit(UnitData unit) => _unitPopup.ShowSingle(unit);
        private void HandlePreviewClosed(UIScreen screen, UICloseReason reason) => PreviewClosed?.Invoke();

        private void HandlePanelClosed(UIScreen screen, UICloseReason reason)
        {
            if (_closing) return;
            _selectionId = null;
            _catalogItems.Clear();
            _actions.HideActions();
            Closed?.Invoke();
        }
    }
}
