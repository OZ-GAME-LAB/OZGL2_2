using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

// 카탈로그의 실제 아이콘과 설명을 확인하는 독립 실행용 UI입니다.
public sealed class ArtifactImageTestPanel : MonoBehaviour
{
    [SerializeField] private ArtifactCatalog _catalog;
    [SerializeField] private ConsumableItemCatalog _consumableCatalog;
    [SerializeField] private TMP_FontAsset _font;

    private RectTransform _viewport;
    private RectTransform _content;
    private GameObject _panel;
    private GridLayoutGroup _grid;
    private TMP_Text _toggleLabel;
    private TMP_Text _consumableToggleLabel;
    private bool _showConsumables;
    private TMP_Text _heading;
    private ScrollRect _scroll;
    private float _lastWidth = -1;
    private readonly List<TMP_Text> _descriptions = new List<TMP_Text>();
    private readonly List<TMP_Text> _names = new List<TMP_Text>();

    private void Start()
    {
        if (EventSystem.current == null)
        {
            var events = new GameObject("Artifact Preview EventSystem", typeof(EventSystem));
            events.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
            events.AddComponent<InputSystemUIInputModule>();
#else
            events.AddComponent<StandaloneInputModule>();
#endif
        }

        RectTransform root = Rect(transform, "Artifact Preview Canvas");
        var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = .5f;
        root.gameObject.AddComponent<GraphicRaycaster>();

        RectTransform backdrop = Box(root, "Background", new Color(.045f, .055f, .075f));
        Stretch(backdrop, 0, 0, 0, 0);
        _heading = Label(root, "아티팩트 이미지 확인", 26);
        Top(_heading.rectTransform, 24, 16, 800, 40);
        _toggleLabel = CreateCatalogButton(root, "Toggle Artifacts", "아티팩트 목록 열기", 280,
            () => ToggleCatalog(false));
        _consumableToggleLabel = CreateCatalogButton(root, "Toggle Consumables", "소모성 아이템 목록 열기", 24,
            () => ToggleCatalog(true));
        TMP_Text hint = Label(root, "카탈로그에 등록된 아이콘 · 이름 · 설명 | 마우스 휠 또는 우측 스크롤바로 이동", 17);
        Top(hint.rectTransform, 24, 64, 1200, 32);
        hint.color = new Color(.65f, .72f, .8f);

        BuildCatalogPanel(root);
    }

    private TMP_Text CreateCatalogButton(RectTransform root, string name, string text, float right,
        UnityEngine.Events.UnityAction onClick)
    {
        var toggleRect = Box(root, name, new Color(.15f, .34f, .42f));
        toggleRect.anchorMin = toggleRect.anchorMax = toggleRect.pivot = Vector2.one;
        toggleRect.anchoredPosition = new Vector2(-right, -16);
        toggleRect.sizeDelta = new Vector2(240, 44);
        var toggle = toggleRect.gameObject.AddComponent<Button>();
        toggle.targetGraphic = toggleRect.GetComponent<Image>();
        TMP_Text label = Label(toggleRect, text, 20);
        Stretch(label.rectTransform, 8, 8, 4, 4);
        label.alignment = TextAlignmentOptions.Center;
        toggle.onClick.AddListener(onClick);
        return label;
    }

    private void BuildCatalogPanel(RectTransform root)
    {
        RectTransform panel = Rect(root, "Catalog Panel");
        Stretch(panel, 24, 24, 108, 24);
        _panel = panel.gameObject;
        _scroll = panel.gameObject.AddComponent<ScrollRect>();
        _viewport = Box(panel, "Viewport", new Color(.045f, .055f, .075f));
        Stretch(_viewport, 0, 22, 0, 0);
        _viewport.gameObject.AddComponent<RectMask2D>();
        _content = Rect(_viewport, "Cards");
        _content.anchorMin = new Vector2(0, 1);
        _content.anchorMax = Vector2.one;
        _content.pivot = new Vector2(.5f, 1);
        _content.sizeDelta = Vector2.zero;
        _grid = _content.gameObject.AddComponent<GridLayoutGroup>();
        _grid.spacing = new Vector2(16, 16);
        _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _scroll.viewport = _viewport;
        _scroll.content = _content;
        _scroll.horizontal = false;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = 45;

        RectTransform bar = Box(panel, "Scrollbar", new Color(.09f, .12f, .16f));
        bar.anchorMin = new Vector2(1, 0);
        bar.anchorMax = Vector2.one;
        bar.pivot = new Vector2(1, .5f);
        bar.sizeDelta = new Vector2(12, 0);
        var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        RectTransform handle = Box(bar, "Handle", new Color(.35f, .49f, .58f));
        Stretch(handle, 0, 0, 0, 0);
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        _scroll.verticalScrollbar = scrollbar;
        _panel.SetActive(false);
    }

    private void ToggleCatalog(bool consumables)
    {
        bool show = !_panel.activeSelf || _showConsumables != consumables;
        _showConsumables = consumables;
        _panel.SetActive(show);
        _toggleLabel.text = show && !consumables ? "아티팩트 목록 닫기" : "아티팩트 목록 열기";
        _consumableToggleLabel.text = show && consumables ? "소모성 아이템 목록 닫기" : "소모성 아이템 목록 열기";
        if (!show) return;

        // 다시 열면 현재 SO 값을 읽어 목록을 갱신합니다.
        foreach (Transform child in _content)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        _descriptions.Clear();
        _names.Clear();
        if (consumables)
        {
            if (_consumableCatalog != null)
                foreach (ConsumableItemData item in _consumableCatalog.Items)
                    if (item != null) CreateCard(item.Id, item.Id, item.Icon, item.DisplayName, item.Description);
            _heading.text = _consumableCatalog == null ? "Consumable Catalog를 연결해 주세요." :
                $"소모성 아이템 이미지 확인 · {_descriptions.Count}종";
        }
        else
        {
            if (_catalog != null)
                foreach (ArtifactData artifact in _catalog.Artifacts)
                    if (artifact != null) CreateCard(artifact.Id,
                        artifact.Id + "  ·  " + RarityName(artifact.Rarity), artifact.Icon, artifact.DisplayName, artifact.Description);
            _heading.text = _catalog == null ? "Artifact Catalog를 연결해 주세요." :
                $"아티팩트 이미지 확인 · {_descriptions.Count}종";
        }
        Canvas.ForceUpdateCanvases();
        ResizeCards();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        _scroll.StopMovement();
        _scroll.verticalNormalizedPosition = 1;
    }

    private void LateUpdate()
    {
        if (_panel != null && _panel.activeSelf &&
            !Mathf.Approximately(_lastWidth, _viewport.rect.width)) ResizeCards();
    }

    private void CreateCard(string itemId, string caption, Sprite sprite, string displayName, string details)
    {
        RectTransform card = Box(_content, itemId, new Color(.095f, .12f, .16f));
        TMP_Text id = Label(card, caption, 15);
        TopStretch(id.rectTransform, 16, 16, 12, 26);
        id.color = new Color(.65f, .75f, .85f);
        RectTransform iconRect = Box(card, "Icon", Color.white);
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(.5f, 1);
        iconRect.anchoredPosition = new Vector2(0, -46);
        iconRect.sizeDelta = new Vector2(144, 144);
        Image icon = iconRect.GetComponent<Image>();
        icon.sprite = sprite;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = sprite != null;
        if (sprite == null)
        {
            TMP_Text missing = Label(iconRect, "아이콘 없음", 20);
            Stretch(missing.rectTransform, 0, 0, 0, 0);
            missing.alignment = TextAlignmentOptions.Center;
        }
        TMP_Text title = Label(card, displayName, 23);
        title.fontStyle = FontStyles.Bold;
        TopStretch(title.rectTransform, 16, 16, 204, 60);
        _names.Add(title);
        TMP_Text description = Label(card, details, 19);
        TopStretch(description.rectTransform, 16, 16, 276, 100);
        description.color = new Color(.8f, .85f, .9f);
        _descriptions.Add(description);
    }

    private void ResizeCards()
    {
        _lastWidth = _viewport.rect.width;
        int columns = Mathf.Clamp(Mathf.FloorToInt((_lastWidth + 16) / 340), 1, 5);
        float width = Mathf.Max(100, (_lastWidth - 16 * (columns - 1)) / columns);
        float nameHeight = 32;
        float descriptionHeight = 80;
        foreach (TMP_Text title in _names)
            nameHeight = Mathf.Max(nameHeight, title.GetPreferredValues(title.text, width - 32, Mathf.Infinity).y);
        foreach (TMP_Text description in _descriptions)
            descriptionHeight = Mathf.Max(descriptionHeight,
                description.GetPreferredValues(description.text, width - 32, Mathf.Infinity).y);
        foreach (TMP_Text title in _names)
            TopStretch(title.rectTransform, 16, 16, 204, nameHeight + 4);
        foreach (TMP_Text description in _descriptions)
            TopStretch(description.rectTransform, 16, 16, 220 + nameHeight, descriptionHeight + 4);
        _grid.constraintCount = columns;
        _grid.cellSize = new Vector2(width, 244 + nameHeight + descriptionHeight);
    }

    private static string RarityName(ArtifactRarity rarity)
    {
        switch (rarity)
        {
            case ArtifactRarity.Common: return "일반";
            case ArtifactRarity.Rare: return "희귀";
            case ArtifactRarity.Legendary: return "전설";
            case ArtifactRarity.Mythic: return "신화";
            default: return rarity.ToString();
        }
    }

    private static RectTransform Rect(Transform parent, string name)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static RectTransform Box(Transform parent, string name, Color color)
    {
        RectTransform rect = Rect(parent, name);
        rect.gameObject.AddComponent<Image>().color = color;
        return rect;
    }

    private TMP_Text Label(Transform parent, string text, float size)
    {
        RectTransform rect = Rect(parent, "Label");
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) label.font = _font;
        label.text = text;
        label.fontSize = size;
        label.color = new Color(.93f, .95f, .98f);
        label.raycastTarget = false;
        return label;
    }

    private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void Top(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void TopStretch(RectTransform rect, float left, float right, float top, float height)
    {
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, 1);
        rect.offsetMin = new Vector2(left, -top - height);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
