using System;
using System.Collections.Generic;
using OZGL.KDH;
using TMPro;
using UnityEngine;

namespace Game.UI.InGame
{
    /// <summary>카테고리별 건설 목록을 표시하고 건설 클릭과 정보 클릭을 구분한다.</summary>
    [DisallowMultipleComponent]
    public sealed class BuildingCatalogView : MonoBehaviour
    {
        public event Action<string> ItemSelected;
        public event Action<string> InfoRequested;
        public UIScreen Popup => _popup;
        public int ItemCount => _items.Count;

        [SerializeField] private UIScreen _popup;
        [SerializeField] private BuildingCardView _cardTemplate;
        [SerializeField] private RectTransform _content;
        [SerializeField] private UnityEngine.UI.ScrollRect _scroll;
        [SerializeField] private UnityEngine.UI.Button[] _tabs;
        [SerializeField] private TMP_Text _emptyText;

        private readonly List<BuildingCatalogItem> _items = new List<BuildingCatalogItem>();
        private readonly List<BuildingCardView> _cards = new List<BuildingCardView>();
        private readonly BuildingType[] _categories =
        {
            BuildingType.Barracks, BuildingType.Producer, BuildingType.Support
        };
        private readonly float[] _scrollPositions = { 1f, 1f, 1f };
        private int _tabIndex;

        private void Awake()
        {
            _cardTemplate.gameObject.SetActive(false);
            for (int i = 0; i < _tabs.Length; i++)
            {
                int tabIndex = i;
                _tabs[i].onClick.AddListener(() => SelectTab(tabIndex));
            }
        }

        public void SetItems(IReadOnlyList<BuildingCatalogItem> items, bool preservePage = false)
        {
            _items.Clear();
            for (int i = 0; i < items.Count; i++) _items.Add(items[i]);

            if (preservePage)
            {
                _scrollPositions[_tabIndex] = _scroll.verticalNormalizedPosition;
            }
            else
            {
                _tabIndex = 0;
                for (int i = 0; i < _scrollPositions.Length; i++) _scrollPositions[i] = 1f;
            }

            Refresh();
        }

        private void SelectTab(int tabIndex)
        {
            _scrollPositions[_tabIndex] = _scroll.verticalNormalizedPosition;
            _tabIndex = tabIndex;
            Refresh();
        }

        private void Refresh()
        {
            int visibleCount = 0;
            foreach (var item in _items)
            {
                if (item.Category != _categories[_tabIndex]) continue;

                if (visibleCount == _cards.Count)
                    _cards.Add(Instantiate(_cardTemplate, _content));

                var card = _cards[visibleCount];
                bool canBuild = item.Offer != null && item.Offer.CanExecute;
                card.Bind(item, false, canBuild, Select, ShowInfo);
                card.gameObject.SetActive(true);
                visibleCount++;
            }

            for (int i = visibleCount; i < _cards.Count; i++)
            {
                _cards[i].Unbind();
                _cards[i].gameObject.SetActive(false);
            }

            for (int i = 0; i < _tabs.Length; i++) _tabs[i].interactable = i != _tabIndex;
            _emptyText.text = "건설 가능한 건물이 없습니다.";
            _emptyText.gameObject.SetActive(visibleCount == 0);

            // 카드 수가 바뀐 뒤 높이를 먼저 갱신해야 이전 탭의 스크롤 위치가 섞이지 않는다.
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _scroll.StopMovement();
            _scroll.verticalNormalizedPosition = _scrollPositions[_tabIndex];
        }

        private void Select(string itemId)
        {
            if (isActiveAndEnabled && _popup.IsVisible) ItemSelected?.Invoke(itemId);
        }

        private void ShowInfo(string itemId)
        {
            if (isActiveAndEnabled && _popup.IsVisible) InfoRequested?.Invoke(itemId);
        }
    }
}
