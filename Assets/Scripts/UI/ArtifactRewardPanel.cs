using System;
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
        [SerializeField] private Card[] _cards;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private TMP_Text _confirmText;
        [SerializeField] private Button _clearButton;
        [SerializeField] private TMP_Text _instructionText;
        [SerializeField] private TMP_Text _pageText;
        [SerializeField] private Button _previousPageButton;
        [SerializeField] private Button _nextPageButton;

        private Action<ArtifactRewardRequest> _choiceRequested;
        private ArtifactRewardViewData _data;
        private ArtifactRewardRequest _pendingRequest;
        private GameObject _previousSelection;
        private int _selectedIndex = -1;
        private int _pageIndex;
        private bool _showRequested;
        private bool _completed;
        private bool _listening;
        private string _message;

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
            if (!CanInteract() || _choiceRequested == null) return;
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
            _rewardText.text = "획득 골드 " + FormatReward(_data.AwardedGold) + "\n획득 보석 " + FormatReward(_data.AwardedGems);
            _instructionText.text = $"총 {_data.Candidates.Count}개 중 1개 선택 → 확정     /     미선택 시 모두 포기";
            bool unlocked = !IsRequestPending;
            int offset = _pageIndex * CardsPerPage;
            int visibleCount = Math.Min(CardsPerPage, _data.Candidates.Count - offset);
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
                card.Name.text = offer.DisplayName;
                card.Rarity.text = offer.RarityName;
                card.Rarity.color = offer.RarityColor;
                card.Effect.text = offer.EffectDescription;
                card.Icon.sprite = offer.Icon;
                card.Icon.enabled = offer.Icon != null;
                card.MissingIcon.SetActive(offer.Icon == null);
                card.Selection.SetActive(offset + i == _selectedIndex);
                card.Button.interactable = unlocked;
            }
            _clearButton.interactable = unlocked && _selectedIndex >= 0;
            _confirmButton.interactable = unlocked && _choiceRequested != null;
            _confirmText.text = _selectedIndex >= 0 ? "선택 확정" : "모두 포기하고 계속";
            bool hasPages = PageCount > 1;
            _previousPageButton.gameObject.SetActive(hasPages);
            _nextPageButton.gameObject.SetActive(hasPages);
            _pageText.gameObject.SetActive(hasPages);
            _pageText.text = $"{_pageIndex + 1} / {PageCount}";
            _previousPageButton.interactable = unlocked && _pageIndex > 0;
            _nextPageButton.interactable = unlocked && _pageIndex < PageCount - 1;
            ConfigureNavigation(visibleCount);
            _statusText.text = IsRequestPending ? "선택 처리 중…" : _message ?? (_choiceRequested == null
                ? "보상 시스템 연결 대기" : _selectedIndex >= 0
                ? $"선택: {_data.Candidates[_selectedIndex].DisplayName} · 확정하면 이 아티팩트를 요청합니다."
                : "아티팩트 1개를 선택하세요. 선택하지 않으면 모두 포기합니다.");
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
                selectOnLeft = _clearButton.interactable ? _clearButton : null, selectOnUp = first };
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

        private static string FormatReward(int? value) => value.HasValue ? "+" + value.Value : "--";
    }
}
