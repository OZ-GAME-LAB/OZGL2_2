using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.InGame
{
    /// <summary>화면의 표시만 담당한다. 열림 순서와 닫기 정책은 매니저가 관리한다.</summary>
    public sealed class UIScreen : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private RectTransform _window;
        [SerializeField] private Selectable _initialFocus;
        [SerializeField] private RectTransform[] _inputRoots = Array.Empty<RectTransform>();
        [SerializeField] private Button _detailsButton;
        [SerializeField] private TMP_Text _detailsLabel;
        [SerializeField] private GameObject _detailsRoot;
        [SerializeField] private float _collapsedHeight = 300;
        [SerializeField] private float _expandedHeight = 440;

        private CanvasGroup[] _inputGroups;
        private Canvas _canvas;
        private int _sortingOrder;
        private bool _overrideSorting;
        public InGameUIManager Manager { get; private set; }
        public UIId Id { get; private set; }
        public UIHandle Handle { get; private set; }
        public bool IsVisible => _root != null && _root.activeInHierarchy;
        public bool IsExpanded { get; private set; }
        public RectTransform Window => _window;
        public Selectable InitialFocus => _initialFocus;
        public GameObject Root => _root;
        public event Action<UIScreen> Shown;
        public event Action<UIScreen, UICloseReason> Closed;

        private void OnEnable()
        {
            if (_detailsButton != null) _detailsButton.onClick.AddListener(HandleDetails);
        }

        private void OnDisable()
        {
            if (_detailsButton != null) _detailsButton.onClick.RemoveListener(HandleDetails);
        }

        internal bool Attach(InGameUIManager manager, UIId id)
        {
            if (_root == null) return false;
            Manager = manager;
            Id = id;
            foreach (OpenUtility utility in GetComponentsInChildren<OpenUtility>(true)) utility.Initialize(manager);
            if (_inputGroups == null)
            {
                int count = _inputRoots.Length == 0 ? 1 : _inputRoots.Length;
                _inputGroups = new CanvasGroup[count];
                for (int i = 0; i < count; i++)
                {
                    GameObject inputRoot = _inputRoots.Length == 0 ? _root :
                        _inputRoots[i] != null ? _inputRoots[i].gameObject : null;
                    if (inputRoot == null) continue;
                    if (!inputRoot.TryGetComponent(out CanvasGroup group)) group = inputRoot.AddComponent<CanvasGroup>();
                    _inputGroups[i] = group;
                }
                _canvas = GetComponent<Canvas>();
                if (_canvas != null)
                {
                    _sortingOrder = _canvas.sortingOrder;
                    _overrideSorting = _canvas.overrideSorting;
                }
            }
            return true;
        }

        public void Show() => Manager?.Open(Id);
        public void Hide(UICloseReason reason = UICloseReason.ContextLost) => Manager?.Close(Handle, reason);

        internal void Display(UIHandle handle, int aboveSortingOrder = -1)
        {
            Handle = handle;
            if (_detailsRoot != null) SetExpanded(false);
            if (_canvas != null && aboveSortingOrder >= _sortingOrder)
            {
                _canvas.overrideSorting = true;
                _canvas.sortingOrder = aboveSortingOrder + 1;
            }
            _root.SetActive(true);
            Shown?.Invoke(this);
        }

        internal void Conceal(UICloseReason reason, bool notify)
        {
            Handle = default;
            if (_root != null) _root.SetActive(false);
            if (_detailsRoot != null) SetExpanded(false);
            if (_canvas != null)
            {
                _canvas.sortingOrder = _sortingOrder;
                _canvas.overrideSorting = _overrideSorting;
            }
            if (notify) Closed?.Invoke(this, reason);
        }

        internal int SortingOrder => _canvas != null ? _canvas.sortingOrder :
            GetComponentInParent<Canvas>() != null ? GetComponentInParent<Canvas>().sortingOrder : 0;

        internal void SetInputEnabled(bool enabled)
        {
            if (_inputGroups == null) return;
            foreach (CanvasGroup group in _inputGroups)
                if (group != null) { group.interactable = enabled; group.blocksRaycasts = enabled; }
        }

        public void SetExpanded(bool expanded)
        {
            IsExpanded = expanded && _detailsRoot != null;
            if (_detailsRoot != null) _detailsRoot.SetActive(IsExpanded);
            if (_detailsLabel != null) _detailsLabel.text = IsExpanded ? "접기" : "자세히";
            if (_window != null)
                _window.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, IsExpanded ? _expandedHeight : _collapsedHeight);
        }

        public void SetContentHeights(float collapsed, float expanded)
        {
            if (collapsed <= 0 || expanded < collapsed) throw new ArgumentOutOfRangeException(nameof(collapsed));
            _collapsedHeight = collapsed;
            _expandedHeight = expanded;
            SetExpanded(IsExpanded);
        }

        private void HandleDetails() => SetExpanded(!IsExpanded);
    }
}
