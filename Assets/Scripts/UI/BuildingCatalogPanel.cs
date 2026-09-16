using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>담당자가 제공한 건설 후보의 표시 정보. null 가격은 미정이며 무료를 뜻하지 않는다.</summary>
    public sealed class BuildingCatalogItem
    {
        public string Id { get; }
        public string Name { get; }
        public string Summary { get; }
        public int? GoldCost { get; }
        public int? GemCost { get; }
        public BuildingCatalogItem(string id, string name, string summary, int? goldCost)
            : this(id, name, summary, goldCost, goldCost.HasValue ? 0 : (int?)null) { }

        public BuildingCatalogItem(string id, string name, string summary, int? goldCost, int? gemCost)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Catalog ID and name are required.");
            if (goldCost < 0) throw new ArgumentOutOfRangeException(nameof(goldCost));
            if (gemCost < 0) throw new ArgumentOutOfRangeException(nameof(gemCost));
            if (goldCost.HasValue != gemCost.HasValue)
                throw new ArgumentException("Both currency amounts must be known, or both must be unspecified.");
            Id = id;
            Name = name;
            Summary = summary ?? "";
            GoldCost = goldCost;
            GemCost = gemCost;
        }
    }

    [DisallowMultipleComponent]
    public sealed class BuildingCatalogPanel : MonoBehaviour
    {
        public event Action<string> ItemSelected;
        public PlayerPopup Popup => _popup;
        public int PageIndex => _page;
        public int ItemCount => _items.Count;

        [Serializable]
        private sealed class Card
        {
            public Button Button;
            public TMP_Text Name;
            public TMP_Text Summary;
            public TMP_Text Price;
        }

        [SerializeField] private PlayerPopup _popup;
        [SerializeField] private Card[] _cards;
        [SerializeField] private Button _previous;
        [SerializeField] private Button _next;
        [SerializeField] private TMP_Text _pageText;
        [SerializeField] private TMP_Text _emptyText;

        private readonly List<BuildingCatalogItem> _items = new List<BuildingCatalogItem>();
        private int _page;

        private void OnEnable()
        {
            _cards[0].Button.onClick.AddListener(HandleFirst);
            _cards[1].Button.onClick.AddListener(HandleSecond);
            _cards[2].Button.onClick.AddListener(HandleThird);
            _cards[3].Button.onClick.AddListener(HandleFourth);
            _previous.onClick.AddListener(HandlePrevious);
            _next.onClick.AddListener(HandleNext);
            Refresh();
        }

        private void OnDisable()
        {
            _cards[0].Button.onClick.RemoveListener(HandleFirst);
            _cards[1].Button.onClick.RemoveListener(HandleSecond);
            _cards[2].Button.onClick.RemoveListener(HandleThird);
            _cards[3].Button.onClick.RemoveListener(HandleFourth);
            _previous.onClick.RemoveListener(HandlePrevious);
            _next.onClick.RemoveListener(HandleNext);
        }

        public void SetItems(IReadOnlyList<BuildingCatalogItem> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            var ids = new HashSet<string>();
            foreach (var item in items)
                if (item == null || !ids.Add(item.Id)) throw new ArgumentException("Catalog candidates need unique IDs.");
            _items.Clear();
            for (int i = 0; i < items.Count; i++) _items.Add(items[i]);
            _page = 0;
            Refresh();
        }

        public void Show()
        {
            Refresh();
            _popup.Show();
        }

        private void HandleFirst() => Select(0);
        private void HandleSecond() => Select(1);
        private void HandleThird() => Select(2);
        private void HandleFourth() => Select(3);
        private void HandlePrevious() { if (_page > 0) { _page--; Refresh(); } }
        private void HandleNext() { if ((_page + 1) * 4 < _items.Count) { _page++; Refresh(); } }

        private void Select(int index)
        {
            int itemIndex = _page * 4 + index;
            if (!isActiveAndEnabled || !_popup.IsVisible || itemIndex >= _items.Count) return;
            string id = _items[itemIndex].Id;
            _popup.Hide();
            ItemSelected?.Invoke(id);
        }

        private void Refresh()
        {
            if (_cards == null || _cards.Length != 4) return;
            for (int i = 0; i < 4; i++)
            {
                int index = _page * 4 + i;
                bool visible = index < _items.Count;
                var card = _cards[i];
                card.Button.gameObject.SetActive(visible);
                if (!visible) continue;
                var item = _items[index];
                card.Name.text = item.Name;
                card.Summary.text = item.Summary;
                card.Price.text = item.GoldCost.HasValue
                    ? BuildingCurrencyText.Format(item.GoldCost.Value, item.GemCost.Value) : "가격 미정";
            }
            _emptyText.gameObject.SetActive(_items.Count == 0);
            bool paging = _items.Count > 4;
            _previous.gameObject.SetActive(paging);
            _next.gameObject.SetActive(paging);
            _pageText.gameObject.SetActive(paging);
            _previous.interactable = _page > 0;
            _next.interactable = (_page + 1) * 4 < _items.Count;
            _pageText.text = (_page + 1) + " / " + Mathf.Max(1, (_items.Count + 3) / 4);
        }
    }
}
