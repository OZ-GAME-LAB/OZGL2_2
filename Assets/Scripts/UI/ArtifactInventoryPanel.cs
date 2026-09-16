using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>보유 유물만 조회한다. 획득·중첩·효과 적용은 ArtifactManager가 소유한다.</summary>
    public sealed class ArtifactInventoryPanel : MonoBehaviour
    {
        public int ItemCount => _entries.Count;
        public bool IsDetailVisible => _detailRoot != null && _detailRoot.activeInHierarchy;
        public PlayerPopup Popup => _popup;

        [Serializable]
        private sealed class Slot
        {
            public Button Button;
            public Image Icon;
            public GameObject MissingIcon;
            public TMP_Text Count;
        }

        [SerializeField] private ArtifactManager _manager;
        [SerializeField] private ArtifactRewardPanel _rewardPresentation;
        [SerializeField] private PlayerPopup _popup;
        [SerializeField] private Button _openButton;
        [SerializeField] private Slot[] _slots = Array.Empty<Slot>();
        [SerializeField] private Button _previous;
        [SerializeField] private Button _next;
        [SerializeField] private TMP_Text _page;
        [SerializeField] private TMP_Text _empty;
        [SerializeField] private GameObject _detailRoot;
        [SerializeField] private Button _closeDetail;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _rarity;
        [SerializeField] private TMP_Text _description;
        [SerializeField] private ScrollRect _descriptionScroll;

        private readonly List<ArtifactInstance> _entries = new List<ArtifactInstance>();
        private UnityAction[] _clicks;
        private string _selectedId;
        private int _pageIndex;
        private GameObject _detailOpener;

        private void OnEnable()
        {
            _clicks = new UnityAction[_slots.Length];
            for (int i = 0; i < _slots.Length; i++)
            {
                int index = i;
                _clicks[i] = () => HandleItem(index);
                _slots[i].Button.onClick.AddListener(_clicks[i]);
            }
            _openButton.onClick.AddListener(Show);
            _previous.onClick.AddListener(HandlePrevious);
            _next.onClick.AddListener(HandleNext);
            _closeDetail.onClick.AddListener(HandleCloseDetail);
            _popup.Closed += HandleClosed;
            if (_manager != null)
            {
                _manager.StackChanged += HandleStackChanged;
                _manager.Cleared += HandleCleared;
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < _slots.Length; i++) _slots[i].Button.onClick.RemoveListener(_clicks[i]);
            _openButton.onClick.RemoveListener(Show);
            _previous.onClick.RemoveListener(HandlePrevious);
            _next.onClick.RemoveListener(HandleNext);
            _closeDetail.onClick.RemoveListener(HandleCloseDetail);
            _popup.Closed -= HandleClosed;
            if (_manager != null)
            {
                _manager.StackChanged -= HandleStackChanged;
                _manager.Cleared -= HandleCleared;
            }
            _popup.Hide();
            HandleClosed(_popup);
        }

        public void Show()
        {
            if (!isActiveAndEnabled) return;
            _pageIndex = 0;
            Refresh();
            _popup.Show();
        }

        public void Refresh()
        {
            _entries.Clear();
            if (_manager != null && _manager.IsInitialized)
                foreach (var entry in _manager.Instances)
                    if (entry != null && entry.Data != null) _entries.Add(entry);
            Render();
        }

        public bool TryCloseDetail()
        {
            if (!IsDetailVisible) return false;
            _detailRoot.SetActive(false);
            _selectedId = null;
            var events = EventSystem.current;
            if (events != null && _detailOpener != null && _detailOpener.activeInHierarchy)
                events.SetSelectedGameObject(_detailOpener);
            _detailOpener = null;
            return true;
        }

        private void HandleItem(int slot)
        {
            int index = _pageIndex * _slots.Length + slot;
            if (!_popup.IsVisible || IsDetailVisible || index >= _entries.Count) return;
            _selectedId = _entries[index].Data.Id;
            _detailOpener = _slots[slot].Button.gameObject;
            _detailRoot.SetActive(true);
            ShowDetail(_entries[index]);
            _descriptionScroll.verticalNormalizedPosition = 1;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_closeDetail.gameObject);
        }

        private void HandlePrevious() => ChangePage(-1);
        private void HandleNext() => ChangePage(1);
        private void HandleCloseDetail() => TryCloseDetail();
        private void HandleStackChanged(ArtifactInstance instance, int previous, int current) => Refresh();

        private void HandleCleared(IReadOnlyList<ArtifactInstance> removed)
        {
            // The manager may still be inside its clear notification; never re-query the old run here.
            _entries.Clear();
            Render();
        }

        private void HandleClosed(PlayerPopup popup)
        {
            _detailRoot.SetActive(false);
            _selectedId = null;
            _detailOpener = null;
        }

        private void ChangePage(int direction)
        {
            if (!_popup.IsVisible || IsDetailVisible) return;
            _pageIndex += direction;
            Render();
        }

        private void Render()
        {
            int pages = Math.Max(1, (_entries.Count + _slots.Length - 1) / Math.Max(1, _slots.Length));
            _pageIndex = Mathf.Clamp(_pageIndex, 0, pages - 1);
            for (int i = 0; i < _slots.Length; i++)
            {
                int index = _pageIndex * _slots.Length + i;
                var slot = _slots[i];
                slot.Button.gameObject.SetActive(index < _entries.Count);
                if (index >= _entries.Count) continue;
                var entry = _entries[index];
                slot.Icon.sprite = entry.Data.Icon;
                slot.Icon.enabled = entry.Data.Icon != null;
                slot.MissingIcon.SetActive(entry.Data.Icon == null);
                slot.Count.text = $"×{entry.StackCount}";
            }
            _empty.gameObject.SetActive(_entries.Count == 0);
            _empty.text = _manager != null && _manager.IsInitialized ? "아직 획득한 유물이 없습니다" : "유물 목록 연결 대기";
            _page.text = $"{_pageIndex + 1} / {pages}";
            _previous.gameObject.SetActive(pages > 1);
            _next.gameObject.SetActive(pages > 1);
            _page.gameObject.SetActive(pages > 1);
            _previous.interactable = _pageIndex > 0;
            _next.interactable = _pageIndex < pages - 1;
            if (_selectedId == null) return;
            var selected = _entries.Find(e => e.Data.Id == _selectedId);
            if (selected == null) TryCloseDetail(); else ShowDetail(selected);
        }

        private void ShowDetail(ArtifactInstance entry)
        {
            var data = entry.Data;
            _name.text = _rewardPresentation != null
                ? _rewardPresentation.ResolveDisplayName(data.Id, data.DisplayName) : data.DisplayName;
            _rarity.text = $"{ArtifactRewardBinding.RarityName(data.Rarity)} · 보유 {entry.StackCount}";
            _rarity.color = ArtifactRewardBinding.RarityColor(data.Rarity);
            _description.text = string.IsNullOrWhiteSpace(data.Description) ? "효과 설명 미등록" : data.Description.Replace("\\n", "\n");
        }
    }
}
