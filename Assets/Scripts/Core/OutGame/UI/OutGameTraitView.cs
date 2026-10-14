using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>잠금 상태와 보유 레벨은 다른 개념입니다. 혈석 잔고도 별도로 판단합니다.</summary>
public enum TraitNodeDisplayState
{
    Locked,
    Unlocked,
    On
}

public enum TraitArrowOnRule
{
    SourcePurchased,
    TargetPurchased
}

[Serializable]
public class TraitButtonSlot
{
    public TraitId Id;
    [Tooltip("기존 Hierarchy에 놓인 노드의 Button. 잠겨 있어도 설명은 클릭 가능")]
    public Button Button;
    [Tooltip("Locked/Unlocked 기본 상태 Sprite가 들어가는 Image")]
    public Image Background;
    [Tooltip("Icon Off/On Sprite가 들어가는 Image")]
    public Image Icon;
    public Sprite IconOff;
    public Sprite IconOn;
    [Tooltip("기본 Background 위에 겹쳐 놓을 별도의 하이라이트 Image. On 테두리 Sprite 사용")]
    public Image Highlight;
}

[Serializable]
public class TraitConnectionSlot
{
    public TraitId From;
    public TraitId To;
    public Image Image;
    public Sprite OffSprite;
    public Sprite OnSprite;
    public TraitArrowOnRule OnRule;
}

/// <summary>
/// OutGameTraitController의 구매 및 저장 규칙은 변경하지 않습니다.
/// 왼쪽 노드: 기본 Off + 구매 시 Icon On + Hover(0.5)/Selected(1.0) 테두리 오버레이.
/// 오른쪽 패널: 클릭 선택 상태만 갱신. Hover 팝업은 제단/토템과 동일한 패턴.
/// </summary>
public class OutGameTraitView : MonoBehaviour
{
    [Header("왼쪽 특성 트리: 배치된 일반 UI 오브젝트")]
    [SerializeField] private TraitButtonSlot[] _slots;
    [SerializeField] private TraitConnectionSlot[] _connections;

    [Header("공통 노드 Sprite")]
    [SerializeField] private Sprite _nodeLockedSprite;
    [SerializeField] private Sprite _nodeUnlockedSprite;
    [Tooltip("노드 Highlight Image에 넣는 켜진 테두리 Sprite. 내부가 투명한 프레임 권장")]
    [SerializeField] private Sprite _nodeOnSprite;

    [Header("오른쪽: 클릭으로 선택된 특성 상세 정보")]
    [SerializeField] private Image _icon;
    [SerializeField] private Image _iconBoard;
    [SerializeField] private Sprite _iconBoardOffSprite;
    [SerializeField] private Sprite _iconBoardOnSprite;
    [SerializeField] private Image _highlightFrame;
    [SerializeField] private Sprite _highlightFrameOffSprite;
    [SerializeField] private Sprite _highlightFrameOnSprite;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _description;
    [SerializeField] private TMP_Text _currentLevelLabel;
    [SerializeField] private TMP_Text _nextLevelLabel;
    [SerializeField] private TMP_Text _upgradeDescription;
    [Tooltip("반드시 Upgrade Cost와 다른 별도의 TMP 텍스트에 연결")]
    [SerializeField] private TMP_Text _walletLabel;
    [SerializeField] private TraitId _initialSelection = TraitId.None;

    [Header("제단/토템 방식의 마우스오버 정보창")]
    [SerializeField] private RectTransform _hoverInfoPanel;
    [SerializeField] private TMP_Text _hoverTitleText;
    [SerializeField] private TMP_Text _hoverDescriptionText;
    [SerializeField] private float _mouseOffsetX = 30f;
    [SerializeField] private float _mouseOffsetY = 30f;

    [Header("오른쪽: 특성 활성 / 특성 강화 버튼")]
    [SerializeField] private Button _upgradeButton;
    [SerializeField] private Image _upgradeBackground;
    [SerializeField] private TMP_Text _upgradeTitle;
    [SerializeField] private TMP_Text _upgradeCost;
    [SerializeField] private Sprite _upgradeNormalSprite;
    [SerializeField] private Sprite _upgradeHoverSprite;
    [SerializeField] private Sprite _upgradePressedSprite;
    [SerializeField] private Sprite _upgradeOffSprite;
    [SerializeField] private Image _upgradeBloodstoneIcon;
    [SerializeField] private Button _backButton;

    private ITraitProgression _progression;
    private PersistentCurrencyManager _wallet;
    private TraitId _inspectedTrait = TraitId.None;
    private TraitId _hoveredTrait = TraitId.None;
    private Vector2 _lastHoverMousePosition;
    private Canvas _rootCanvas;
    private RectTransform _hoverPanelParent;
    private readonly Vector3[] _hoverCorners = new Vector3[4];

    private sealed class BoundNode
    {
        public Button Button;
        public UnityAction ClickHandler;
        public TraitNodePointerRelay Pointer;
    }
    private readonly List<BoundNode> _boundNodes = new List<BoundNode>();

    public event Action<TraitId> UpgradeRequested;
    public event Action BackRequested;

    public bool ValidateReferences()
    {
        bool valid = _slots != null && _slots.Length > 0 &&
            _icon != null && _iconBoard != null && _highlightFrame != null &&
            _title != null && _description != null &&
            _currentLevelLabel != null && _nextLevelLabel != null &&
            _upgradeDescription != null && _walletLabel != null &&
            _upgradeButton != null && _upgradeBackground != null &&
            _upgradeTitle != null && _upgradeCost != null &&
            _hoverInfoPanel != null && _hoverTitleText != null && _hoverDescriptionText != null &&
            _nodeLockedSprite != null && _nodeUnlockedSprite != null && _nodeOnSprite != null &&
            _iconBoardOffSprite != null && _iconBoardOnSprite != null &&
            _highlightFrameOffSprite != null && _highlightFrameOnSprite != null &&
            _upgradeNormalSprite != null && _upgradeOffSprite != null;

        if (_walletLabel != null &&
            (_walletLabel == _upgradeCost || _walletLabel == _upgradeTitle || _upgradeTitle == _upgradeCost))
        {
            valid = false;
            Debug.LogError("[OutGameTraitView] Wallet Label / Upgrade Title / Upgrade Cost는 반드시 서로 다른 TMP 컴포넌트여야 합니다.", this);
        }

        if (_slots != null)
        {
            var ids = new HashSet<TraitId>();
            for (int i = 0; i < _slots.Length; i++)
            {
                TraitButtonSlot s = _slots[i];
                bool okay = s != null && s.Id != TraitId.None && s.Button != null &&
                    s.Background != null && s.Icon != null && s.Highlight != null &&
                    s.IconOff != null && s.IconOn != null && ids.Add(s.Id) &&
                    s.Button.GetComponent<TraitNodePointerRelay>() != null;
                if (!okay)
                {
                    valid = false;
                    Debug.LogError("[OutGameTraitView] Slots[" + i + "]의 ID/Button/Background/Icon/Highlight/PointerRelay와 Sprite를 확인하세요.", this);
                }
            }
        }
        if (_connections != null)
        {
            for (int i = 0; i < _connections.Length; i++)
            {
                TraitConnectionSlot c = _connections[i];
                if (c == null || c.From == TraitId.None || c.To == TraitId.None ||
                    c.Image == null || c.OffSprite == null || c.OnSprite == null)
                {
                    valid = false;
                    Debug.LogError("[OutGameTraitView] Connections[" + i + "] 연결값을 확인하세요.", this);
                }
            }
        }
        if (!valid) Debug.LogError("[OutGameTraitView] 필수 참조가 없어서 초기화를 중단합니다. Inspector 가이드를 확인하세요.", this);
        return valid;
    }

    public void Initialize(ITraitProgression progression, PersistentCurrencyManager wallet)
    {
        Shutdown();
        if (progression == null || wallet == null || !ValidateReferences()) return;

        _progression = progression;
        _wallet = wallet;
        _inspectedTrait = _initialSelection;
        _hoveredTrait = TraitId.None;

        _rootCanvas = GetComponentInParent<Canvas>();
        if (_rootCanvas != null) _rootCanvas = _rootCanvas.rootCanvas;
        _hoverPanelParent = _hoverInfoPanel.parent as RectTransform;
        if (_hoverPanelParent == null)
        {
            Debug.LogError("[OutGameTraitView] HoverInfoPanel 부모는 RectTransform이어야 합니다.", this);
            Shutdown();
            return;
        }
        _hoverInfoPanel.gameObject.SetActive(false);
        // 툴팁이 입력을 가로채면 PointerExit/Enter가 연속 발생하므로 Raycast을 끕니다.
        Graphic[] hoverGraphics = _hoverInfoPanel.GetComponentsInChildren<Graphic>(true);
        foreach (Graphic g in hoverGraphics) g.raycastTarget = false;

        for (int i = 0; i < _slots.Length; i++)
        {
            TraitButtonSlot slot = _slots[i];
            TraitId id = slot.Id;
            var relay = slot.Button.GetComponent<TraitNodePointerRelay>();
            relay.Configure(id);
            relay.PointerEntered += OnTraitPointerEntered;
            relay.PointerMoved += OnTraitPointerMoved;
            relay.PointerExited += OnTraitPointerExited;

            UnityAction click = () => SelectTrait(id);
            slot.Button.onClick.AddListener(click);
            _boundNodes.Add(new BoundNode { Button = slot.Button, ClickHandler = click, Pointer = relay });

            slot.Button.interactable = true; // Lock 상태여도 미리보기 가능
            slot.Button.transition = Selectable.Transition.None;
            slot.Button.targetGraphic = slot.Background;
            slot.Background.raycastTarget = true;
            slot.Icon.raycastTarget = false;
            slot.Highlight.raycastTarget = false;
            slot.Highlight.sprite = _nodeOnSprite;
            slot.Highlight.color = new Color(1f, 1f, 1f, 0f);
            slot.Highlight.enabled = true;
        }

        if (_connections != null)
            foreach (TraitConnectionSlot c in _connections) c.Image.raycastTarget = false;
        _icon.raycastTarget = false;
        _iconBoard.raycastTarget = false;
        _highlightFrame.raycastTarget = false;

        _upgradeButton.targetGraphic = _upgradeBackground;
        _upgradeButton.transition = Selectable.Transition.SpriteSwap;
        SpriteState states = _upgradeButton.spriteState;
        states.highlightedSprite = _upgradeHoverSprite != null ? _upgradeHoverSprite : _upgradeNormalSprite;
        states.selectedSprite = _upgradeHoverSprite != null ? _upgradeHoverSprite : _upgradeNormalSprite;
        states.pressedSprite = _upgradePressedSprite != null ? _upgradePressedSprite : _upgradeNormalSprite;
        states.disabledSprite = _upgradeOffSprite;
        _upgradeButton.spriteState = states;
        _upgradeButton.onClick.AddListener(OnUpgradeClicked);
        if (_backButton != null) _backButton.onClick.AddListener(OnBackClicked);
        Refresh();
    }

    public void Refresh()
    {
        if (_progression == null || _wallet == null) return;
        _walletLabel.text = " " + _wallet.Balance;

        foreach (TraitButtonSlot s in _slots)
        {
            TraitData data = FindData(s.Id);
            if (data == null) continue;
            int level = _progression.GetLevel(s.Id);
            bool prereqReady = HasPrerequisite(data);

            // 구매됐어도 기본 테두리는 Unlocked(Off) 유지. 아이콘만 On.
            s.Background.sprite = level == 0 && !prereqReady ? _nodeLockedSprite : _nodeUnlockedSprite;
            s.Background.color = Color.white;
            s.Background.enabled = s.Background.sprite != null;
            s.Icon.sprite = level > 0 ? s.IconOn : s.IconOff;
            s.Icon.color = Color.white;
            s.Icon.enabled = s.Icon.sprite != null;
            s.Button.interactable = true;
            SetHighlight(s);
        }

        if (_connections != null)
        {
            foreach (TraitConnectionSlot c in _connections)
            {
                TraitId triggerId = c.OnRule == TraitArrowOnRule.TargetPurchased ? c.To : c.From;
                c.Image.sprite = _progression.GetLevel(triggerId) > 0 ? c.OnSprite : c.OffSprite;
                c.Image.color = Color.white;
                c.Image.enabled = c.Image.sprite != null;
            }
        }

        RefreshDetails();
        RefreshHoveredInfo();
    }

    private void SetHighlight(TraitButtonSlot s)
    {
        bool selected = _inspectedTrait == s.Id;
        bool hovered = !selected && _hoveredTrait == s.Id;
        float alpha = selected ? 1f : hovered ? 0.5f : 0f;
        Color tint = s.Highlight.color;
        if (Mathf.Approximately(tint.a, alpha)) return;
        tint.a = alpha;
        s.Highlight.color = tint;
    }

    private void RefreshDetails()
    {
        TraitData inspected = _inspectedTrait == TraitId.None ? null : FindData(_inspectedTrait);
        TraitButtonSlot slot = inspected == null ? null : FindSlot(_inspectedTrait);
        int current = inspected == null ? 0 : _progression.GetLevel(inspected.Id);
        Sprite sprite = slot == null ? null : current > 0 ? slot.IconOn : slot.IconOff;
        if (sprite == null && inspected != null) sprite = inspected.Icon;
        bool hasIcon = sprite != null;
        _icon.sprite = sprite;
        _icon.enabled = hasIcon;
        _icon.color = Color.white;

        bool on = hasIcon && current > 0;
        _iconBoard.sprite = on ? _iconBoardOnSprite : _iconBoardOffSprite;
        _highlightFrame.sprite = on ? _highlightFrameOnSprite : _highlightFrameOffSprite;
        _iconBoard.color = Color.white;
        _highlightFrame.color = Color.white;

        if (inspected == null)
        {
            _title.text = string.Empty;
            _description.text = string.Empty;
            _currentLevelLabel.text = string.Empty;
            _nextLevelLabel.text = string.Empty;
            _upgradeDescription.text = string.Empty;
            _upgradeTitle.text = string.Empty;
            _upgradeCost.text = string.Empty;
            SetUpgradeAvailability(false);
            return;
        }

        _title.text = inspected.DisplayName;
        _description.text = inspected.Description;
        bool max = current >= inspected.MaxLevel;
        int next = Mathf.Min(current + 1, inspected.MaxLevel);
        _currentLevelLabel.text = "Lv. " + current;
        _nextLevelLabel.text = max ? "MAX" : "Lv. " + next;
        string unit = inspected.ValueUnit ?? string.Empty;
        string oldValue = (inspected.ValuePerLevel * current).ToString("0.##") + unit;
        string newValue = (inspected.ValuePerLevel * next).ToString("0.##") + unit;
        _upgradeDescription.text = inspected.EffectLabel + "   " + oldValue + "  >  " + newValue;

        int cost = max ? 0 : inspected.GetUpgradeCost(current);
        bool prereqReady = HasPrerequisite(inspected);
        bool lacksBloodstone = !max && prereqReady && _wallet.Balance < cost;
        bool canUpgrade = _progression.CanUpgrade(inspected.Id, out _);

        _upgradeTitle.text = max ? "최대 레벨" :
            !prereqReady ? "특성 잠김" :
            lacksBloodstone ? "혈석 부족" :
            current == 0 ? "특성 활성" : "특성 강화";
        _upgradeCost.text = max ? string.Empty : cost.ToString();
        SetUpgradeAvailability(canUpgrade);
    }

    private void SetUpgradeAvailability(bool canUpgrade)
    {
        _upgradeBackground.sprite = canUpgrade ? _upgradeNormalSprite : _upgradeOffSprite;
        _upgradeBackground.color = Color.white;
        _upgradeButton.interactable = canUpgrade;
        if (_upgradeBloodstoneIcon != null)
            _upgradeBloodstoneIcon.color = canUpgrade
                ? Color.white
                : new Color(0.45f, 0.45f, 0.45f, 0.1f);
    }

    // 제단/토템의 Hover UI와 동일한 PointerEnter/Move/Exit + 정보 표시 + 위치 반전.
    private void OnTraitPointerEntered(TraitId id, Vector2 mousePosition)
    {
        _hoveredTrait = id;
        _lastHoverMousePosition = mousePosition;
        TraitButtonSlot s = FindSlot(id);
        if (s != null) SetHighlight(s);
        ShowHoverInfo(id, mousePosition);
    }

    private void OnTraitPointerMoved(TraitId id, Vector2 mousePosition)
    {
        if (_hoveredTrait != id) return;
        _lastHoverMousePosition = mousePosition;
        UpdateHoverPanelPosition(mousePosition);
    }

    private void OnTraitPointerExited(TraitId id)
    {
        if (_hoveredTrait != id) return;
        _hoveredTrait = TraitId.None;
        TraitButtonSlot s = FindSlot(id);
        if (s != null) SetHighlight(s);
        HideHoverInfo();
    }

    private void RefreshHoveredInfo()
    {
        if (_hoveredTrait != TraitId.None)
            ShowHoverInfo(_hoveredTrait, _lastHoverMousePosition);
    }

    private void ShowHoverInfo(TraitId id, Vector2 mousePosition)
    {
        if (_progression == null || _hoverInfoPanel == null || !isActiveAndEnabled) return;
        TraitData data = FindData(id);
        if (data == null) { HideHoverInfo(); return; }

        int level = _progression.GetLevel(id);
        _hoverTitleText.text = data.DisplayName;
        string description = data.Description;
        if (!HasPrerequisite(data))
        {
            description += "\n\n잠금 해제 조건\n" + data.Prerequisite.DisplayName + " " + data.PrerequisiteLevel + "레벨 필요";
        }
        else
        {
            description += "\n\n현재 단계: " + level + " / " + data.MaxLevel;
            if (level >= data.MaxLevel) description += "\n최대 레벨";
            else description += "\n필요 혈석: " + data.GetUpgradeCost(level);
        }
        _hoverDescriptionText.text = description;
        _hoverInfoPanel.gameObject.SetActive(true);
        _hoverTitleText.ForceMeshUpdate();
        _hoverDescriptionText.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_hoverInfoPanel);
        Canvas.ForceUpdateCanvases();
        UpdateHoverPanelPosition(mousePosition);
    }

    private void UpdateHoverPanelPosition(Vector2 mouseScreenPosition)
    {
        if (_hoverPanelParent == null || _hoverInfoPanel == null) return;
        _hoverInfoPanel.pivot = Vector2.zero;
        Vector2 position = mouseScreenPosition + new Vector2(_mouseOffsetX, _mouseOffsetY);
        SetHoverPanelScreenPosition(position);
        _hoverInfoPanel.GetWorldCorners(_hoverCorners);
        Vector2 upperRight = RectTransformUtility.WorldToScreenPoint(GetUICamera(), _hoverCorners[2]);
        if (upperRight.x > Screen.width)
        {
            _hoverInfoPanel.pivot = new Vector2(1f, 0f);
            position = mouseScreenPosition + new Vector2(-_mouseOffsetX, _mouseOffsetY);
            SetHoverPanelScreenPosition(position);
        }
        // 기존 제단/토템 동작에 추가: 화면 위/아래로 잘리지 않게 마무리 보정.
        _hoverInfoPanel.GetWorldCorners(_hoverCorners);
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(GetUICamera(), _hoverCorners[0]);
        upperRight = RectTransformUtility.WorldToScreenPoint(GetUICamera(), _hoverCorners[2]);
        float correctionY = 0f;
        if (upperRight.y > Screen.height) correctionY = Screen.height - upperRight.y;
        else if (bottomLeft.y < 0f) correctionY = -bottomLeft.y;
        if (!Mathf.Approximately(correctionY, 0f))
            SetHoverPanelScreenPosition(position + new Vector2(0f, correctionY));
    }

    private void SetHoverPanelScreenPosition(Vector2 screenPosition)
    {
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _hoverPanelParent, screenPosition, GetUICamera(), out localPoint))
        {
            _hoverInfoPanel.anchoredPosition = localPoint;
        }
    }

    private Camera GetUICamera()
    {
        if (_rootCanvas == null || _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
        return _rootCanvas.worldCamera;
    }

    private void HideHoverInfo()
    {
        if (_hoverInfoPanel != null) _hoverInfoPanel.gameObject.SetActive(false);
    }

    private bool HasPrerequisite(TraitData data)
    {
        return data.Prerequisite == null ||
            _progression.GetLevel(data.Prerequisite.Id) >= data.PrerequisiteLevel;
    }

    private TraitButtonSlot FindSlot(TraitId id)
    {
        if (_slots == null) return null;
        for (int i = 0; i < _slots.Length; i++)
            if (_slots[i] != null && _slots[i].Id == id) return _slots[i];
        return null;
    }

    private TraitData FindData(TraitId id)
    {
        if (_progression == null) return null;
        IReadOnlyList<TraitData> list = _progression.Data;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && list[i].Id == id) return list[i];
        return null;
    }

    private void SelectTrait(TraitId id)
    {
        TraitId previous = _inspectedTrait;
        _inspectedTrait = id;
        TraitButtonSlot prior = FindSlot(previous);
        if (prior != null) SetHighlight(prior);
        TraitButtonSlot current = FindSlot(id);
        if (current != null) SetHighlight(current);
        RefreshDetails();
    }

    private void OnUpgradeClicked()
    {
        if (_inspectedTrait == TraitId.None || _progression == null) return;
        if (_progression.CanUpgrade(_inspectedTrait, out _))
            UpgradeRequested?.Invoke(_inspectedTrait);
    }

    private void OnBackClicked() { BackRequested?.Invoke(); }

    public void Shutdown()
    {
        for (int i = 0; i < _boundNodes.Count; i++)
        {
            BoundNode b = _boundNodes[i];
            if (b.Button != null) b.Button.onClick.RemoveListener(b.ClickHandler);
            if (b.Pointer != null)
            {
                b.Pointer.PointerEntered -= OnTraitPointerEntered;
                b.Pointer.PointerMoved -= OnTraitPointerMoved;
                b.Pointer.PointerExited -= OnTraitPointerExited;
            }
        }
        _boundNodes.Clear();
        if (_upgradeButton != null) _upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
        if (_backButton != null) _backButton.onClick.RemoveListener(OnBackClicked);
        _progression = null;
        _wallet = null;
        _hoveredTrait = TraitId.None;
        HideHoverInfo();
    }

    private void OnEnable()
    {
        // 제단/토템 화면에서 돌아올 때 저장된 특성/혈석 상태 재동기화.
        if (_progression != null && _wallet != null) Refresh();
    }

    private void OnDisable()
    {
        TraitId previousHover = _hoveredTrait;
        _hoveredTrait = TraitId.None;
        TraitButtonSlot previousSlot = FindSlot(previousHover);
        if (previousSlot != null && previousSlot.Highlight != null) SetHighlight(previousSlot);
        HideHoverInfo();
    }

    private void OnDestroy() { Shutdown(); }
}
