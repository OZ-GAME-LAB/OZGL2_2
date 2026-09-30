using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>가변 후보를 페이지로 표시하는 선택/확정/포기 UI. 실제 보상과 진행은 담당 시스템이 처리한다.</summary>
    [DisallowMultipleComponent]
    public sealed class ArtifactRewardPanel : MonoBehaviour
    {
        public event Action<ArtifactRewardRequest> ChoiceRequested
        {
            add { _choiceRequested += value; Refresh(); }
            remove { _choiceRequested -= value; Refresh(); }
        }

        public bool IsVisible => _panelRoot != null && _panelRoot.activeInHierarchy;
        public bool IsRequestPending => _pendingRequest != null;
        public int CurrentPageIndex => _pageIndex;
        public int PageCount => _data == null ? 0 : (_data.Candidates.Count - 1) / CardsPerPage + 1;
        public string SelectedArtifactId => _data != null && _selectedIndex >= 0
            ? _data.Candidates[_selectedIndex].ArtifactId : null;

        [Serializable]
        private sealed class Card
        {
            public Button Button;
            public TMP_Text Name;
            public TMP_Text Rarity;
            public TMP_Text Effect;
            public Image Icon;
            public GameObject MissingIcon;
            public GameObject Selection;
        }

        private const int CardsPerPage = 3;

        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private TMP_Text _rewardText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private Sprite _fallbackIcon;
        [SerializeField] private Card[] _cards;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private TMP_Text _confirmText;
        [SerializeField] private Button _clearButton;
        [SerializeField] private TMP_Text _instructionText;
        [SerializeField] private TMP_Text _pageText;
        [SerializeField] private Button _previousPageButton;
        [SerializeField] private Button _nextPageButton;

        [SerializeField] private bool _compactPresentation;
        [SerializeField] private bool _showCardEffects;
        [Serializable]
        private sealed class DisplayNameOverride
        {
            public string ArtifactId;
            public string DisplayName;
        }
        [SerializeField] private DisplayNameOverride[] _displayNames = Array.Empty<DisplayNameOverride>();

        private Action<ArtifactRewardRequest> _choiceRequested;
        private ArtifactRewardViewData _data;
        private ArtifactRewardRequest _pendingRequest;
        private GameObject _previousSelection;
        private bool _allowForfeit = true;
        private int _selectedIndex = -1;
        private int _pageIndex;
        private bool _showRequested;
        private bool _completed;
        private bool _listening;
        private string _message;
        private readonly Vector2[] _defaultCardPositions = new Vector2[CardsPerPage];
        private bool _cardPositionsCached;
        private Vector2 _defaultConfirmPosition;
        private Vector2 _defaultConfirmSize;
        private ColorBlock _defaultConfirmColors;
        private bool _actionPresentationCached;

        private void OnEnable()
        {
            if (HasView())
            {
                _cards[0].Button.onClick.AddListener(HandleFirstClicked);
                _cards[1].Button.onClick.AddListener(HandleSecondClicked);
                _cards[2].Button.onClick.AddListener(HandleThirdClicked);
                _confirmButton.onClick.AddListener(HandleConfirmClicked);
                _clearButton.onClick.AddListener(HandleClearClicked);
                _previousPageButton.onClick.AddListener(HandlePreviousPageClicked);
                _nextPageButton.onClick.AddListener(HandleNextPageClicked);
                _listening = true;
            }
            Refresh();
        }

        private void OnDisable()
        {
            if (_listening)
            {
                _cards[0].Button.onClick.RemoveListener(HandleFirstClicked);
                _cards[1].Button.onClick.RemoveListener(HandleSecondClicked);
                _cards[2].Button.onClick.RemoveListener(HandleThirdClicked);
                _confirmButton.onClick.RemoveListener(HandleConfirmClicked);
                _clearButton.onClick.RemoveListener(HandleClearClicked);
                _previousPageButton.onClick.RemoveListener(HandlePreviousPageClicked);
                _nextPageButton.onClick.RemoveListener(HandleNextPageClicked);
                _listening = false;
            }
            // 숨김은 실제 요청 취소가 아니다. 늦게 도착하는 응답도 동일 ID로 처리한다.
            HideView();
        }

        public void SetForfeitAllowed(bool allowed)
        {
            _allowForfeit = allowed;
            Refresh();
        }

        public void ShowReward(ArtifactRewardViewData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (!HasView()) throw new InvalidOperationException("Artifact reward view references are incomplete.");
            if (_data == null || _data.RewardId != data.RewardId)
            {
                if (IsRequestPending) throw new InvalidOperationException("Resolve or explicitly reset the pending reward first.");
                _data = data;
                _selectedIndex = -1;
                _pageIndex = 0;
                _completed = false;
                _message = null;
            }
            // 같은 보상 재오픈은 선택/대기/완료 상태와 최초 후보 스냅샷을 유지한다.
            _showRequested = !_completed;
            Refresh();
        }

        public void HideReward()
        {
            _showRequested = false;
            Refresh();
        }

        /// <summary>실제 소유자가 이전 플레이/보상 요청을 무효화한 후 호출한다. 게임 상태를 취소하지 않는다.</summary>
        public void ResetReward()
        {
            _allowForfeit = true;
            _data = null;
            _pendingRequest = null;
            _selectedIndex = -1;
            _pageIndex = 0;
            _completed = false;
            _showRequested = false;
            _message = null;
            Refresh();
        }

        public bool TryResolveRequest(Guid requestId, bool succeeded, string message = null)
        {
            if (_pendingRequest == null || _pendingRequest.RequestId != requestId) return false;
            _pendingRequest = null;
            _completed = succeeded;
            if (succeeded) _showRequested = false;
            _message = succeeded ? null : string.IsNullOrWhiteSpace(message)
                ? "선택이 처리되지 않았습니다. 다시 시도해주세요." : message;
            Refresh();
            return true;
        }

        private void HandleFirstClicked() => SelectCandidate(0);
        private void HandleSecondClicked() => SelectCandidate(1);
        private void HandleThirdClicked() => SelectCandidate(2);
        private void HandlePreviousPageClicked() => ChangePage(_pageIndex - 1);
        private void HandleNextPageClicked() => ChangePage(_pageIndex + 1);

        private void HandleClearClicked()
        {
            if (!CanInteract()) return;
            _selectedIndex = -1;
            _message = null;
            Refresh();
        }

        private void HandleConfirmClicked()
        {
            if (!CanInteract() || _choiceRequested == null || (!_allowForfeit && _selectedIndex < 0)) return;
            var request = new ArtifactRewardRequest(_data.RewardId, SelectedArtifactId);
            _pendingRequest = request;
            _message = null;
            Refresh(); // 동기 응답/재진입에도 중복 요청이 발생하지 않도록 먼저 잠근다.
            _choiceRequested.Invoke(request);
        }

        private void SelectCandidate(int index)
        {
            int candidateIndex = _pageIndex * CardsPerPage + index;
            if (!CanInteract() || index < 0 || index >= CardsPerPage || candidateIndex >= _data.Candidates.Count) return;
            _selectedIndex = candidateIndex;
            _message = null;
            Refresh();
        }

        private void ChangePage(int pageIndex)
        {
            if (!CanInteract() || pageIndex < 0 || pageIndex >= PageCount || pageIndex == _pageIndex) return;
            _pageIndex = pageIndex;
            Refresh(); // 선택은 전체 후보 기준으로 유지하며 페이지 변경은 요청을 전송하지 않는다.
        }

        private bool CanInteract() => isActiveAndEnabled && IsVisible && _data != null &&
            !_completed && !IsRequestPending;

        private bool HasView()
        {
            if (_panelRoot == null || _panelRoot == gameObject || !_panelRoot.transform.IsChildOf(transform) ||
                _rewardText == null || _statusText == null || _confirmButton == null ||
                _confirmText == null || _clearButton == null || _clearButton == _confirmButton ||
                _instructionText == null || _pageText == null || _previousPageButton == null || _nextPageButton == null ||
                _previousPageButton == _nextPageButton || _previousPageButton == _confirmButton ||
                _previousPageButton == _clearButton || _nextPageButton == _confirmButton || _nextPageButton == _clearButton ||
                _cards == null || _cards.Length != CardsPerPage) return false;
            for (int i = 0; i < _cards.Length; i++)
            {
                var card = _cards[i];
                if (card == null || card.Button == null || card.Name == null || card.Rarity == null ||
                    card.Effect == null || card.Icon == null || card.MissingIcon == null || card.Selection == null ||
                    card.Button == _confirmButton || card.Button == _clearButton ||
                    card.Button == _previousPageButton || card.Button == _nextPageButton) return false;
                for (int j = 0; j < i; j++) if (_cards[j].Button == card.Button) return false;
            }
            return true;
        }

        private void Refresh()
        {
            if (!HasView()) return;
            if (!isActiveAndEnabled || !_showRequested || _data == null || _completed)
            {
                HideView();
                return;
            }
            bool opening = !IsVisible;
            if (opening && EventSystem.current != null)
                _previousSelection = EventSystem.current.currentSelectedGameObject;
            _panelRoot.SetActive(true);
            _rewardText.text = BuildRewardSummary(_data.AwardedGold, _data.AwardedGems);
            _instructionText.text = _data.Candidates.Count == 1
                ? "유물을 확인하세요"
                : $"{_data.Candidates.Count}개의 유물 중 하나를 선택하세요";
            bool unlocked = !IsRequestPending;
            int offset = _pageIndex * CardsPerPage;
            int visibleCount = Math.Min(CardsPerPage, _data.Candidates.Count - offset);
            LayoutVisibleCards(visibleCount);
            for (int i = 0; i < _cards.Length; i++)
            {
                var card = _cards[i];
                card.Button.gameObject.SetActive(i < visibleCount);
                if (i >= visibleCount)
                {
                    card.Button.interactable = false;
                    card.Selection.SetActive(false);
                    continue;
                }
                var offer = _data.Candidates[offset + i];
                card.Name.text = GetDisplayName(offer);
                card.Rarity.text = offer.RarityName;
                card.Rarity.color = offer.RarityColor;
                card.Effect.text = offer.EffectDescription;
                var icon = offer.Icon != null ? offer.Icon : _fallbackIcon;
                card.Icon.sprite = icon;
                card.Icon.enabled = icon != null;
                card.MissingIcon.SetActive(icon == null);
                card.Selection.SetActive(offset + i == _selectedIndex);
                card.Button.interactable = unlocked;
            }
            _clearButton.gameObject.SetActive(_selectedIndex >= 0);
            _clearButton.interactable = unlocked && _selectedIndex >= 0;
            _confirmButton.interactable = unlocked && _choiceRequested != null && (_allowForfeit || _selectedIndex >= 0);
            _confirmText.text = IsRequestPending ? "처리 중…" : _selectedIndex >= 0 ? "선택하기" : _allowForfeit ? "건너뛰기" : "유물을 선택하세요";
            ConfigureActionPresentation(_selectedIndex >= 0);
            bool hasPages = PageCount > 1;
            _previousPageButton.gameObject.SetActive(hasPages);
            _nextPageButton.gameObject.SetActive(hasPages);
            _pageText.gameObject.SetActive(hasPages);
            _pageText.text = $"{_pageIndex + 1} / {PageCount}";
            _previousPageButton.interactable = unlocked && _pageIndex > 0;
            _nextPageButton.interactable = unlocked && _pageIndex < PageCount - 1;
            ConfigureNavigation(visibleCount);
            _statusText.text = IsRequestPending ? "선택을 적용하는 중…" : _message ?? (_choiceRequested == null
                ? "잠시만 기다려주세요" : _selectedIndex >= 0
                ? GetDisplayName(_data.Candidates[_selectedIndex]) + " 선택됨"
                : "");
            if (_compactPresentation && _showCardEffects && !IsRequestPending && _message == null && _selectedIndex >= 0)
                _statusText.text = _data.Candidates[_selectedIndex].EffectDescription;
            int focusIndex = _selectedIndex >= offset && _selectedIndex < offset + visibleCount ? _selectedIndex - offset : 0;
            if (opening && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_cards[focusIndex].Button.gameObject);
            else if (unlocked && EventSystem.current != null)
            {
                var current = EventSystem.current.currentSelectedGameObject;
                var selectable = current != null ? current.GetComponent<Selectable>() : null;
                if (current == null || (current.transform.IsChildOf(_panelRoot.transform) &&
                    (selectable == null || !selectable.isActiveAndEnabled || !selectable.IsInteractable())))
                    EventSystem.current.SetSelectedGameObject(_cards[focusIndex].Button.gameObject);
            }
        }

        private string GetDisplayName(ArtifactRewardOffer offer)
        {
            return ResolveDisplayName(offer.ArtifactId, offer.DisplayName);
        }

        internal string ResolveDisplayName(string artifactId, string fallback)
        {
            if (_compactPresentation)
                foreach (var item in _displayNames)
                    if (item != null && item.ArtifactId == artifactId && !string.IsNullOrWhiteSpace(item.DisplayName))
                        return item.DisplayName;
            return fallback;
        }

        private void LayoutVisibleCards(int visibleCount)
        {
            CacheDefaultCardPositions();
            if (!_cardPositionsCached || visibleCount <= 0) return;

            float spacing = _defaultCardPositions[1].x - _defaultCardPositions[0].x;
            float centerX = _defaultCardPositions[1].x;
            float startX = centerX - spacing * (visibleCount - 1) * .5f;
            for (int i = 0; i < CardsPerPage; i++)
            {
                var rect = (RectTransform)_cards[i].Button.transform;
                Vector2 position = _defaultCardPositions[i];
                if (i < visibleCount) position.x = startX + spacing * i;
                rect.anchoredPosition = position;
            }
        }

        private void CacheDefaultCardPositions()
        {
            if (_cardPositionsCached || _cards == null || _cards.Length != CardsPerPage) return;
            for (int i = 0; i < CardsPerPage; i++)
            {
                if (_cards[i]?.Button == null) return;
                _defaultCardPositions[i] = ((RectTransform)_cards[i].Button.transform).anchoredPosition;
            }
            _cardPositionsCached = true;
        }

        private void ConfigureActionPresentation(bool hasSelection)
        {
            CacheActionPresentation();
            if (!_actionPresentationCached) return;

            var rect = (RectTransform)_confirmButton.transform;
            Vector2 size = _defaultConfirmSize;
            Vector2 position = _defaultConfirmPosition;
            ColorBlock colors = _defaultConfirmColors;
            if (!hasSelection)
            {
                size.x = Mathf.Min(240, _defaultConfirmSize.x);
                position.x += (.5f - rect.pivot.x) * (_defaultConfirmSize.x - size.x);
                colors.normalColor = Color.Lerp(colors.normalColor, new Color32(55, 42, 53, 255), .82f);
                colors.highlightedColor = Color.Lerp(colors.highlightedColor, new Color32(82, 58, 72, 255), .72f);
                colors.selectedColor = colors.highlightedColor;
                colors.pressedColor = Color.Lerp(colors.pressedColor, new Color32(43, 32, 43, 255), .75f);
            }
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            _confirmButton.colors = colors;
            _confirmText.fontSize = hasSelection ? 24 : 18;
            _confirmText.fontSizeMax = _confirmText.fontSize;
        }

        private void CacheActionPresentation()
        {
            if (_actionPresentationCached || _confirmButton == null) return;
            var rect = (RectTransform)_confirmButton.transform;
            _defaultConfirmPosition = rect.anchoredPosition;
            _defaultConfirmSize = rect.sizeDelta;
            _defaultConfirmColors = _confirmButton.colors;
            _actionPresentationCached = true;
        }

        private void ConfigureNavigation(int visibleCount)
        {
            var first = _cards[0].Button;
            var last = _cards[visibleCount - 1].Button;
            for (int i = 0; i < visibleCount; i++)
            {
                _cards[i].Button.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = i > 0 ? _cards[i - 1].Button : _previousPageButton.interactable ? _previousPageButton : null,
                    selectOnRight = i < visibleCount - 1 ? _cards[i + 1].Button : _nextPageButton.interactable ? _nextPageButton : null,
                    selectOnDown = _confirmButton
                };
            }
            _previousPageButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnRight = first, selectOnDown = first };
            _nextPageButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnLeft = last, selectOnDown = last };
            _clearButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnRight = _confirmButton, selectOnUp = first };
            _confirmButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnLeft = _clearButton.gameObject.activeSelf && _clearButton.interactable ? _clearButton : null,
                selectOnUp = first };
        }

        private void HideView()
        {
            if (_panelRoot == null) return;
            var events = EventSystem.current;
            var current = events != null ? events.currentSelectedGameObject : null;
            // Selectable은 요청 잠금/비활성화 시 선택을 먼저 null로 지울 수 있다.
            // 다른 UI에 포커스가 있으면 건드리지 않되, 이 모달이 비운 포커스는 복원한다.
            bool ownsFocus = current != null ? current.transform.IsChildOf(_panelRoot.transform) : _panelRoot.activeSelf;
            _panelRoot.SetActive(false);
            if (ownsFocus && events != null)
            {
                var previous = _previousSelection != null ? _previousSelection.GetComponent<Selectable>() : null;
                events.SetSelectedGameObject(previous != null && previous.isActiveAndEnabled && previous.IsInteractable()
                    ? previous.gameObject : null);
            }
            _previousSelection = null;
        }

        private static string BuildRewardSummary(int? gold, int? gems)
        {
            var rewards = new List<string>(2);
            if (gold.GetValueOrDefault() > 0) rewards.Add("골드 +" + gold.Value);
            if (gems.GetValueOrDefault() > 0) rewards.Add("보석 +" + gems.Value);
            if (rewards.Count > 0) return string.Join("  ·  ", rewards);
            return gold.HasValue && gems.HasValue ? "추가 재화 없음" : "보상 집계 중";
        }
    }
}
