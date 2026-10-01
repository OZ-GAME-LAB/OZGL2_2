using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.InGame
{
    /// <summary>건설 후보를 공용 슬롯에 표시하고, 선택한 후보의 ID만 전달한다.</summary>
    [DisallowMultipleComponent]
    public sealed class BuildingCatalogView : MonoBehaviour
    {
        public event Action<string> ItemSelected;
        public UIScreen Popup => _popup;
        public int PageIndex => _page;
        public int ItemCount => _items.Count;

        [Serializable]
        private sealed class Card
        {
            public UIItemSlot Slot;
            public TMP_Text Summary;
        }

        [SerializeField] private UIScreen _popup;
        [SerializeField] private Card[] _cards = Array.Empty<Card>();
        [SerializeField] private Button _previous;
        [SerializeField] private Button _next;
        [SerializeField] private TMP_Text _pageText;
        [SerializeField] private TMP_Text _emptyText;

        private readonly List<BuildingCatalogItem> _items = new List<BuildingCatalogItem>();
        private int _page;

        private void OnEnable()
        {
            if (_previous != null) _previous.onClick.AddListener(HandlePrevious);
            if (_next != null) _next.onClick.AddListener(HandleNext);
            Refresh();
        }

        private void OnDisable()
        {
            if (_previous != null) _previous.onClick.RemoveListener(HandlePrevious);
            if (_next != null) _next.onClick.RemoveListener(HandleNext);
            foreach (var card in _cards)
                if (card != null && card.Slot != null) card.Slot.Unbind();
        }

        public void SetItems(IReadOnlyList<BuildingCatalogItem> items, bool preservePage = false)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            var ids = new HashSet<string>();
            foreach (var item in items)
                if (item == null || !ids.Add(item.Id))
                    throw new ArgumentException("Catalog candidates need unique IDs.", nameof(items));

            _items.Clear();
            for (int i = 0; i < items.Count; i++) _items.Add(items[i]);
            _page = preservePage ? Mathf.Clamp(_page, 0, Mathf.Max(0, (_items.Count - 1) / Mathf.Max(1, _cards.Length))) : 0;
            Refresh();
        }

        private void HandlePrevious()
        {
            if (_page <= 0) return;
            _page--;
            Refresh();
        }

        private void HandleNext()
        {
            if ((_page + 1) * _cards.Length >= _items.Count) return;
            _page++;
            Refresh();
        }

        private void Select(string itemId)
        {
            if (!isActiveAndEnabled || !_popup.IsVisible) return;
            // 다음 화면을 여는 Presenter가 전환한다. 여기서 먼저 닫으면 취소로 처리될 수 있다.
            ItemSelected?.Invoke(itemId);
        }

        private void Refresh()
        {
            int pageSize = _cards.Length;
            if (pageSize == 0 || _previous == null || _next == null ||
                _pageText == null || _emptyText == null) return;

            for (int i = 0; i < pageSize; i++)
            {
                var card = _cards[i];
                if (card == null || card.Slot == null) continue;
                int index = _page * pageSize + i;
                bool visible = index < _items.Count;
                card.Slot.gameObject.SetActive(visible);
                if (!visible)
                {
                    card.Slot.Unbind();
                    continue;
                }

                var item = _items[index];
                string price = item.GoldCost.HasValue
                    ? BuildingCurrencyText.Format(item.GoldCost.Value, item.GemCost.Value)
                    : "가격 미정";
                card.Summary.text = item.Summary;
                card.Slot.Bind(null, item.Name, price, false, true, () => Select(item.Id));
            }

            _emptyText.gameObject.SetActive(_items.Count == 0);
            bool paging = _items.Count > pageSize;
            _previous.gameObject.SetActive(paging);
            _next.gameObject.SetActive(paging);
            _pageText.gameObject.SetActive(paging);
            _previous.interactable = _page > 0;
            _next.interactable = (_page + 1) * pageSize < _items.Count;
            int pageCount = Mathf.Max(1, (_items.Count + pageSize - 1) / pageSize);
            _pageText.text = (_page + 1) + " / " + pageCount;
        }
    }
}
