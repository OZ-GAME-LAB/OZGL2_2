using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI.InGame
{
    /// <summary>가변 후보를 페이지로 표시하는 선택/확정/포기 UI. 실제 보상과 진행은 담당 시스템이 처리한다.</summary>
    [DisallowMultipleComponent]
    public sealed class ArtifactRewardView : MonoBehaviour
    {
        public event Action<ArtifactData> ChoiceRequested
        {
            add { _choiceRequested += value; Refresh(); }
            remove { _choiceRequested -= value; Refresh(); }
        }

        public UIScreen Screen => _screen;
        public bool IsVisible => _screen != null && _screen.IsVisible;
        public bool IsRequestPending => _isRequestPending;
        public IReadOnlyList<ArtifactData> Candidates => _candidates;
        public int CurrentPageIndex => _pageIndex;
        public int PageCount => _candidates.Count == 0 ? 0 : (_candidates.Count - 1) / CardsPerPage + 1;
        public string SelectedArtifactId => _selectedIndex >= 0 && _selectedIndex < _candidates.Count
            ? _candidates[_selectedIndex].Id : null;

        [Serializable]
        private sealed class Card
        {
            public UIItemSlot Slot;
            public UnityEngine.UI.Button Button;
            public TMP_Text Name;
            public TMP_Text Rarity;
            public TMP_Text Effect;
            public UnityEngine.UI.Image Icon;
            public GameObject MissingIcon;
            public GameObject Selection;
        }

        private const int CardsPerPage = 3;

        [SerializeField] private UIScreen _screen;
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private TMP_Text _rewardText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private Sprite _fallbackIcon;
        [SerializeField] private Card[] _cards;
        [SerializeField] private UnityEngine.UI.Button _confirmButton;
        [SerializeField] private TMP_Text _confirmText;
        [SerializeField] private UnityEngine.UI.Button _clearButton;
        [SerializeField] private TMP_Text _instructionText;
        [SerializeField] private TMP_Text _pageText;
        [SerializeField] private UnityEngine.UI.Button _previousPageButton;
        [SerializeField] private UnityEngine.UI.Button _nextPageButton;

        [SerializeField] private bool _compactPresentation;
        [SerializeField] private bool _showCardEffects;
        [Serializable]
        private sealed class DisplayNameOverride
        {
            public string ArtifactId;
            public string DisplayName;
        }
        [SerializeField] private DisplayNameOverride[] _displayNames = Array.Empty<DisplayNameOverride>();

        private Action<ArtifactData> _choiceRequested;
        private IReadOnlyList<ArtifactData> _candidates = Array.Empty<ArtifactData>();
        private bool _isRequestPending;
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
        private UnityEngine.UI.ColorBlock _defaultConfirmColors;
        private bool _actionPresentationCached;

        private void OnEnable()
        {
            if (HasView())
            {
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
                _confirmButton.onClick.RemoveListener(HandleConfirmClicked);
                _clearButton.onClick.RemoveListener(HandleClearClicked);
                _previousPageButton.onClick.RemoveListener(HandlePreviousPageClicked);
                _nextPageButton.onClick.RemoveListener(HandleNextPageClicked);
                _listening = false;
            }
            if (_cards != null)
                foreach (var card in _cards)
                    if (card != null && card.Slot != null) card.Slot.Unbind();
            // 화면 종료 통지를 받은 Presenter가 현재 요청의 취소를 처리한다.
            HideView();
        }

        public void SetForfeitAllowed(bool allowed)
        {
            _allowForfeit = allowed;
            Refresh();
        }

        /// <summary>실패 후 같은 후보를 다시 요청하면 페이지와 선택을 보존한다.</summary>
        public void ShowSelection(IReadOnlyList<ArtifactData> candidates, bool allowForfeit, string message)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (candidates.Count == 0)
                throw new ArgumentException("At least one artifact candidate is required.", nameof(candidates));
            if (!HasView()) throw new InvalidOperationException("Artifact reward view references are incomplete.");
            if (IsRequestPending)
                throw new InvalidOperationException("Resolve or explicitly end the pending selection first.");

            var snapshot = new ArtifactData[candidates.Count];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < candidates.Count; i++)
            {
                ArtifactData candidate = candidates[i];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.Id) || !ids.Add(candidate.Id))
                    throw new ArgumentException("Candidates must be non-null with distinct IDs.", nameof(candidates));
                snapshot[i] = candidate;
            }

            bool preserveSelection = !string.IsNullOrWhiteSpace(message) && SameCandidates(snapshot);
            if (!preserveSelection)
            {
                _selectedIndex = -1;
                _pageIndex = 0;
            }
            _candidates = Array.AsReadOnly(snapshot);
            _isRequestPending = false;
            _completed = false;
            _allowForfeit = allowForfeit;
            _message = message;
            _showRequested = true;
            Refresh();
        }

        public void EndSelection()
        {
            _isRequestPending = false;
            _showRequested = false;
            Refresh();
        }

        private bool SameCandidates(IReadOnlyList<ArtifactData> candidates)
        {
            if (_candidates.Count != candidates.Count) return false;
            for (int i = 0; i < candidates.Count; i++)
                if (!ReferenceEquals(_candidates[i], candidates[i])) return false;
            return true;
        }

        /// <summary>실제 소유자가 이전 플레이/보상 요청을 무효화한 후 호출한다. 게임 상태를 취소하지 않는다.</summary>
        public void ResetReward()
        {
            _allowForfeit = true;
            _candidates = Array.Empty<ArtifactData>();
            _isRequestPending = false;
            _selectedIndex = -1;
            _pageIndex = 0;
            _completed = false;
            _showRequested = false;
            _message = null;
            Refresh();
        }

        /// <summary>Presenter가 ChoiceRequested 콜백 안에서 동기적으로 선택을 승인/거부한다. 게임 적용의 비동기 완료 응답에는 사용하지 않는다.</summary>
        public bool TryResolveSelection(bool succeeded, string message = null)
        {
            if (!IsRequestPending) return false;
            _isRequestPending = false;
            _completed = succeeded;
            if (succeeded) _showRequested = false;
            _message = succeeded ? null : string.IsNullOrWhiteSpace(message)
                ? "선택이 처리되지 않았습니다. 다시 시도해주세요." : message;
            Refresh();
            return true;
        }

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
            ArtifactData selected = _selectedIndex >= 0 ? _candidates[_selectedIndex] : null;
            Action<ArtifactData> choiceRequested = _choiceRequested;
            _isRequestPending = true;
            _message = null;
            Refresh(); // 동기 응답/재진입에도 중복 요청이 발생하지 않도록 먼저 잠근다.
            choiceRequested.Invoke(selected);
        }

        private void SelectCandidate(int index)
        {
            int candidateIndex = _pageIndex * CardsPerPage + index;
            if (!CanInteract() || index < 0 || index >= CardsPerPage || candidateIndex >= _candidates.Count) return;
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

        private bool CanInteract() => isActiveAndEnabled && IsVisible && _candidates.Count > 0 &&
            !_completed && !IsRequestPending;

        private bool HasView()
        {
            if (_screen == null || _panelRoot == null || _panelRoot == gameObject || !_panelRoot.transform.IsChildOf(transform) ||
                _rewardText == null || _statusText == null || _confirmButton == null ||
                _confirmText == null || _clearButton == null || _clearButton == _confirmButton ||
                _instructionText == null || _pageText == null || _previousPageButton == null || _nextPageButton == null ||
                _previousPageButton == _nextPageButton || _previousPageButton == _confirmButton ||
                _previousPageButton == _clearButton || _nextPageButton == _confirmButton || _nextPageButton == _clearButton ||
                _cards == null || _cards.Length != CardsPerPage) return false;
            for (int i = 0; i < _cards.Length; i++)
            {
                var card = _cards[i];
                if (card == null || card.Slot == null || card.Button == null || card.Name == null || card.Rarity == null ||
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
            if (!isActiveAndEnabled || !_showRequested || _candidates.Count == 0 || _completed)
            {
                HideView();
                return;
            }
            bool opening = !IsVisible;
            _rewardText.text = "유물 보상";
            _instructionText.text = _candidates.Count == 1
                ? "유물을 확인하세요"
                : $"{_candidates.Count}개의 유물 중 하나를 선택하세요";
            bool unlocked = !IsRequestPending;
            int offset = _pageIndex * CardsPerPage;
            int visibleCount = Math.Min(CardsPerPage, _candidates.Count - offset);
            LayoutVisibleCards(visibleCount);
            for (int i = 0; i < _cards.Length; i++)
            {
                var card = _cards[i];
                card.Button.gameObject.SetActive(i < visibleCount);
                if (i >= visibleCount)
                {
                    card.Slot.Unbind();
                    card.Button.interactable = false;
                    card.Selection.SetActive(false);
                    continue;
                }
                ArtifactData candidate = _candidates[offset + i];
                var icon = candidate.Icon != null ? candidate.Icon : _fallbackIcon;
                int cardIndex = i;
                card.Slot.Bind(icon, GetDisplayName(candidate), RarityName(candidate.Rarity),
                    offset + i == _selectedIndex, unlocked, () => SelectCandidate(cardIndex));
                card.Icon.enabled = icon != null;
                card.Rarity.color = RarityColor(candidate.Rarity);
                card.Effect.text = GetDescription(candidate);
                card.MissingIcon.SetActive(icon == null);
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
                ? GetDisplayName(_candidates[_selectedIndex]) + " 선택됨"
                : "");
            if (_compactPresentation && _showCardEffects && !IsRequestPending && _message == null && _selectedIndex >= 0)
                _statusText.text = GetDescription(_candidates[_selectedIndex]);

            // 내용 준비를 끝낸 뒤 기존 보상 화면을 교체한다.
            if (opening) _screen.Manager?.ReplacePopup(_screen.Id);
            if (!IsVisible) return;
            int focusIndex = _selectedIndex >= offset && _selectedIndex < offset + visibleCount ? _selectedIndex - offset : 0;
            if (opening && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_cards[focusIndex].Button.gameObject);
            else if (unlocked && EventSystem.current != null)
            {
                var current = EventSystem.current.currentSelectedGameObject;
                var selectable = current != null ? current.GetComponent<UnityEngine.UI.Selectable>() : null;
                if (current == null || (current.transform.IsChildOf(_panelRoot.transform) &&
                    (selectable == null || !selectable.isActiveAndEnabled || !selectable.IsInteractable())))
                    EventSystem.current.SetSelectedGameObject(_cards[focusIndex].Button.gameObject);
            }
        }

        private string GetDisplayName(ArtifactData data)
        {
            return ResolveDisplayName(data.Id, data.DisplayName);
        }

        private static string GetDescription(ArtifactData data) => string.IsNullOrWhiteSpace(data.Description)
            ? "효과 설명 미등록" : data.Description.Replace("\\n", "\n");

        internal static string RarityName(ArtifactRarity rarity) => rarity switch
        {
            ArtifactRarity.Common => "일반",
            ArtifactRarity.Rare => "희귀",
            ArtifactRarity.Legendary => "전설",
            ArtifactRarity.Mythic => "신화",
            _ => rarity.ToString()
        };

        internal static Color RarityColor(ArtifactRarity rarity) => rarity switch
        {
            ArtifactRarity.Rare => new Color32(112, 186, 255, 255),
            ArtifactRarity.Legendary => new Color32(255, 205, 99, 255),
            ArtifactRarity.Mythic => new Color32(246, 136, 172, 255),
            _ => new Color32(200, 214, 220, 255)
        };

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
            UnityEngine.UI.ColorBlock colors = _defaultConfirmColors;
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
                _cards[i].Button.navigation = new UnityEngine.UI.Navigation
                {
                    mode = UnityEngine.UI.Navigation.Mode.Explicit,
                    selectOnLeft = i > 0 ? _cards[i - 1].Button : _previousPageButton.interactable ? _previousPageButton : null,
                    selectOnRight = i < visibleCount - 1 ? _cards[i + 1].Button : _nextPageButton.interactable ? _nextPageButton : null,
                    selectOnDown = _confirmButton
                };
            }
            _previousPageButton.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit,
                selectOnRight = first, selectOnDown = first };
            _nextPageButton.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit,
                selectOnLeft = last, selectOnDown = last };
            _clearButton.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit,
                selectOnRight = _confirmButton, selectOnUp = first };
            _confirmButton.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit,
                selectOnLeft = _clearButton.gameObject.activeSelf && _clearButton.interactable ? _clearButton : null,
                selectOnUp = first };
        }

        private void HideView()
        {
            if (_screen != null)
                _screen.Manager?.ClosePopup(_screen.Id, _completed ? UICloseReason.Completed : UICloseReason.ContextLost);
        }

    }
}
