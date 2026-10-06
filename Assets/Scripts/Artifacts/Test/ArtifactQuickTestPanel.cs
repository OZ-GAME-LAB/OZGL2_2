using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 테스트 씬에서 카탈로그의 아티팩트를 직접 획득합니다. 게임 초기화는 Bootstrap에 맡깁니다.
[DefaultExecutionOrder(1100)]
public sealed class ArtifactQuickTestPanel : MonoBehaviour
{
    [SerializeField] private ArtifactManager _artifacts;
    [SerializeField] private TMP_FontAsset _font;
    private Canvas _canvas;
    private GameObject _panel;
    private TMP_Text _toggleText, _detail, _message;
    private Button _apply;
    private ArtifactData _selected;
    private ArtifactCatalog _catalog;
    private RectTransform _content;
    private bool _ready;
    private readonly List<Button> _rows = new List<Button>();
    private readonly List<ArtifactData> _entries = new List<ArtifactData>();

    private void Start()
    {
        BuildUI();
        if (_artifacts == null)
        {
            _message.text = "ArtifactManager 연결을 확인하세요.";
            return;
        }
        _artifacts.StackChanged += OnStackChanged;
        _artifacts.Cleared += OnCleared;
    }

    private void Update()
    {
        if (_artifacts == null || _canvas == null) return;
        if (_catalog != _artifacts.ArtifactCatalog)
        {
            _catalog = _artifacts.ArtifactCatalog;
            RebuildList();
        }
        if (_ready != _artifacts.IsInitialized)
        {
            _ready = _artifacts.IsInitialized;
            _message.text = _ready ? "아티팩트를 선택한 뒤 획득·적용을 누르세요." : "게임 초기화를 기다리는 중입니다.";
            Refresh();
        }
    }

    private void RebuildList()
    {
        foreach (Button row in _rows)
        {
            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }
        _rows.Clear();
        _entries.Clear();
        _selected = null;
        if (_catalog != null)
        {
            var sorted = new List<ArtifactData>(_catalog.Artifacts);
            sorted.Sort((a, b) => string.CompareOrdinal(a != null ? a.Id : "", b != null ? b.Id : ""));
            foreach (ArtifactData artifact in sorted)
            {
                if (artifact == null) continue;
                Button row = MakeButton(_content, "", 0, 0, 388, 44);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
                row.onClick.AddListener(() => { _selected = artifact; Refresh(); });
                _entries.Add(artifact);
                _rows.Add(row);
            }
        }
        Refresh();
    }

    private int Stacks(ArtifactData data) => _artifacts != null &&
        _artifacts.TryGetById(data.Id, out ArtifactInstance instance) ? instance.StackCount : 0;

    private void Refresh()
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            ArtifactData data = _entries[i];
            _rows[i].GetComponentInChildren<TMP_Text>().text =
                $"{data.Id} · {data.DisplayName}\n{data.Rarity}   보유 {Stacks(data)} / {data.MaxStacks}";
            _rows[i].GetComponent<Image>().color = data == _selected
                ? new Color(.12f, .48f, .48f) : new Color(.16f, .22f, .29f);
        }
        _detail.text = _selected == null ? "목록에서 아티팩트를 선택하세요." :
            $"{_selected.DisplayName} [{_selected.Rarity}]\n{_selected.Description}";
        _apply.interactable = _artifacts != null && _artifacts.IsInitialized &&
            _selected != null && Stacks(_selected) < _selected.MaxStacks;
    }

    private void ApplySelected()
    {
        if (_artifacts == null || _selected == null) return;
        bool added = _artifacts.TryAdd(_selected);
        _message.text = added ? $"{_selected.DisplayName} 획득 완료 ({Stacks(_selected)}중첩)" :
            "획득 실패: 초기화·최대 중첩·효과 설정을 확인하세요.";
        Refresh();
    }

    private void OnStackChanged(ArtifactInstance instance, int previous, int current) => Refresh();
    private void OnCleared(IReadOnlyList<ArtifactInstance> instances) => Refresh();

    private void BuildUI()
    {
        var root = new GameObject("Artifact Test Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        _canvas = root.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 210;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = .5f;
        Button toggle = MakeButton(root.transform, "아티팩트 테스트 열기", 0, 0, 190, 34);
        AnchorBottomRight((RectTransform)toggle.transform, -18, 18);
        _toggleText = toggle.GetComponentInChildren<TMP_Text>();
        RectTransform panel = Box(root.transform, "Artifact Selector", 0, 0, 430, 550);
        AnchorBottomRight(panel, -18, 60);
        _panel = panel.gameObject;
        toggle.onClick.AddListener(() =>
        {
            bool show = !_panel.activeSelf;
            _panel.SetActive(show);
            _toggleText.text = show ? "아티팩트 테스트 닫기" : "아티팩트 테스트 열기";
            if (show) Refresh();
        });
        Label(panel, "아티팩트 직접 획득", 16, 505, 398, 30, 21);
        Label(panel, "패시브·유닛 스탯은 획득 후 생성한 유닛으로 확인하세요.", 16, 470, 398, 32, 13);
        RectTransform viewport = Box(panel, "Catalog", 16, 220, 398, 242);
        viewport.gameObject.AddComponent<RectMask2D>();
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        _content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        _content.SetParent(viewport, false);
        _content.anchorMin = new Vector2(0, 1);
        _content.anchorMax = Vector2.one;
        _content.pivot = new Vector2(0, 1);
        _content.sizeDelta = Vector2.zero;
        var layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 5;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = _content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30;
        _detail = Label(panel, "목록에서 아티팩트를 선택하세요.", 16, 92, 398, 118, 15);
        _apply = MakeButton(panel, "획득·적용", 16, 48, 398, 36);
        _apply.onClick.AddListener(ApplySelected);
        _apply.interactable = false;
        _message = Label(panel, "게임 초기화를 기다리는 중입니다.", 16, 8, 398, 34, 13);
        _panel.SetActive(false);
    }

    private static void AnchorBottomRight(RectTransform rect, float x, float y)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
        rect.anchoredPosition = new Vector2(x, y);
    }

    private static RectTransform Box(Transform parent, string name, float x, float y, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        go.GetComponent<Image>().color = new Color(.07f, .1f, .15f, .97f);
        return rect;
    }

    private TMP_Text Label(Transform parent, string value, float x, float y, float width, float height, float size)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        var text = go.GetComponent<TMP_Text>();
        if (_font != null) text.font = _font;
        text.text = value;
        text.fontSize = size;
        text.color = new Color(.9f, .94f, .97f);
        text.raycastTarget = false;
        return text;
    }

    private Button MakeButton(Transform parent, string value, float x, float y, float width, float height)
    {
        RectTransform rect = Box(parent, "Button", x, y, width, height);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        Label(rect, value, 6, 2, width - 12, height - 4, 14).alignment = TextAlignmentOptions.Center;
        return button;
    }

    private void OnEnable() { if (_canvas != null) _canvas.gameObject.SetActive(true); }
    private void OnDisable() { if (_canvas != null) _canvas.gameObject.SetActive(false); }
    private void OnDestroy()
    {
        if (_artifacts == null) return;
        _artifacts.StackChanged -= OnStackChanged;
        _artifacts.Cleared -= OnCleared;
    }
}
