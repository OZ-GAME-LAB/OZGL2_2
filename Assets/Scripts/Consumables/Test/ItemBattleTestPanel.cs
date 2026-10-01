using System;
using System.Collections.Generic;
using Game.Core;
using TMPro;
using Units;
using Units.Skills;
using UnityEngine;
using UnityEngine.UI;

// ItemTestScene 전용 Canvas UI. 기존 전투 진행 및 아이템 사용 경로를 사용합니다.
[DefaultExecutionOrder(1000)]
public class ItemBattleTestPanel : MonoBehaviour
{
    [SerializeField] private ConsumableItemManager _items;
    [SerializeField] private TMP_FontAsset _font;
    private GameFlowController _flow;
    private RuntimeUnitManager _units;
    private ItemAreaUseTestController _areaUse;
    private Canvas _canvas;
    private RectTransform _panel, _popup, _slots, _choices;
    private TMP_Text _status, _detail, _message, _popupTitle, _toggleLabel;
    private Button _use, _choose, _discard;
    private Unit_Gateway _target;
    private int _slot = -1;
    private bool _dirty = true;
    private readonly Color _surface = new Color(.07f, .10f, .15f, .97f);
    private readonly Color _button = new Color(.16f, .22f, .29f, 1f);
    private readonly Color _accent = new Color(.12f, .48f, .48f, 1f);

    private void Start()
    {
        BuildCanvas();
        _flow = FindInScene<GameFlowController>();
        _units = FindInScene<RuntimeUnitManager>();
        EffectManager effects = FindInScene<EffectManager>();
        SkillEffectResolver resolver = FindInScene<SkillEffectResolver>();
        if (_items == null || _flow == null || _units == null || effects == null || resolver == null)
        {
            Report("아이템 및 전투 시스템 연결을 확인하세요.");
            return;
        }
        if (!_items.IsInitialized) _items.Initialize(effects, _flow, _units, resolver);
        if (!_items.IsInitialized) { Report("아이템 초기화 설정을 확인하세요."); return; }
        _items.InventoryChanged += RefreshInventory;
        _areaUse = gameObject.AddComponent<ItemAreaUseTestController>();
        _areaUse.Initialize(_items, _flow, pointer =>
            (_panel.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(_panel, pointer)) ||
            (_popup.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(_popup, pointer)), Report);
        foreach (ConsumableItemData item in _items.Catalog.Items)
        {
            if (!_items.HasEmptySlot) break;
            _items.TryAdd(item);
        }
        Report("전투 중 사용할 아이템을 선택하세요.");
    }

    private void BuildCanvas()
    {
        GameObject root = new GameObject("Item Inventory Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        _canvas = root.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 200;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = .5f;
        Button toggle = MakeButton(root.transform, "아이템 패널 닫기", 18, 18, 160, 32, TogglePanel);
        _toggleLabel = toggle.GetComponentInChildren<TMP_Text>();
        _panel = Box(root.transform, "Inventory", 18, 60, 444, 290, _surface);
        _status = Label(_panel, "소모성 아이템", 16, 246, 265, 28, 19);
        MakeButton(_panel, "아이템 추가", 318, 244, 110, 30, ShowCatalog);
        _slots = Scroll(_panel, "Slots", 16, 130, 412, 102, true);
        _detail = Label(_panel, "아이템을 선택하세요.", 16, 65, 412, 60, 14);
        _use = MakeButton(_panel, "사용", 16, 30, 160, 30, UseSelected);
        _use.GetComponent<Image>().color = _accent;
        _choose = MakeButton(_panel, "대상 선택", 184, 30, 120, 30, ShowTargets);
        _discard = MakeButton(_panel, "버리기", 312, 30, 116, 30, () =>
        {
            if (_items != null) Report(_items.TryRemove(_slot) ? "아이템을 버렸습니다." : "버릴 수 없습니다.");
        });
        _message = Label(_panel, "초기화 중", 16, 3, 412, 24, 12);
        _use.interactable = _choose.interactable = _discard.interactable = false;
        _popup = Box(root.transform, "Item Choices", 18, 362, 444, 270, _surface);
        _popupTitle = Label(_popup, "아이템 추가", 16, 230, 310, 28, 18);
        MakeButton(_popup, "닫기", 358, 230, 70, 28, () => _popup.gameObject.SetActive(false));
        _choices = Scroll(_popup, "Choices", 16, 16, 412, 202, false);
        _popup.gameObject.SetActive(false);
    }

    private void TogglePanel()
    {
        SetPanelVisible(!_panel.gameObject.activeSelf);
    }

    private void SetPanelVisible(bool show)
    {
        if (!show)
        {
            _popup.gameObject.SetActive(false);
        }
        _panel.gameObject.SetActive(show);
        _toggleLabel.text = show ? "아이템 패널 닫기" : "아이템 패널 열기";
    }

    private void BeginAreaUse()
    {
        _areaUse.Begin(_slot);
        if (_areaUse.IsAiming) SetPanelVisible(false);
    }

    private void Update()
    {
        if (_canvas == null || !_panel.gameObject.activeSelf || _items == null || !_items.IsInitialized || _areaUse == null) return;
        if (_dirty) { RebuildSlots(); _dirty = false; }
        _status.text = $"소모성 아이템  {_items.ItemCount} / {_items.Capacity}";
        bool selected = _items.TryGetItem(_slot, out ConsumableItemData item);
        _use.interactable = selected && _flow.CanBattle() && !_items.IsUsing;
        _choose.interactable = selected && item.TargetMode == ConsumableTargetMode.Single;
        _discard.interactable = selected && !_items.IsUsing;
        if (!selected) { _detail.text = "슬롯을 선택하세요. 아이템은 전투 중 사용할 수 있습니다."; return; }
        string target = item.TargetMode == ConsumableTargetMode.Area ? $"범위 · 반경 {item.Radius}" :
            item.TargetMode == ConsumableTargetMode.All ? "전체 대상" :
            _target != null ? $"{_target.name} · HP {_target.CurrentHp:0.#}" : "단일 대상 · 대상을 선택하세요";
        _detail.text = $"<b>{item.DisplayName}</b>  <color=#8ED6D0>{target}</color>\n{item.Description}";
        _use.GetComponentInChildren<TMP_Text>().text = item.TargetMode == ConsumableTargetMode.Area ? "범위 지정" : "사용";
    }

    private void RefreshInventory() { _dirty = true; }

    private void RebuildSlots()
    {
        Clear(_slots);
        for (int i = 0; i < _items.Slots.Count; i++)
        {
            int index = i;
            ConsumableItemData item = _items.Slots[i].Item;
            Button button = MakeButton(_slots, "", 0, 0, 96, 96, () => SelectSlot(index));
            button.GetComponent<Image>().color = _slot == i ? _accent : _button;
            LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = layout.preferredHeight = 96;
            Label(button.transform, $"{i + 1}", 6, 73, 20, 20, 12);
            if (item != null && item.Icon != null)
            {
                RectTransform icon = Box(button.transform, "Icon", 27, 38, 42, 42, Color.white);
                icon.GetComponent<Image>().sprite = item.Icon;
                icon.GetComponent<Image>().preserveAspect = true;
                icon.GetComponent<Image>().raycastTarget = false;
            }
            else Label(button.transform, item == null ? "—" : "◆", 28, 38, 40, 40, 25).alignment = TextAlignmentOptions.Center;
            Label(button.transform, item == null ? "빈 슬롯" : item.DisplayName, 5, 3, 86, 34, 12).alignment = TextAlignmentOptions.Center;
        }
    }

    private void SelectSlot(int index)
    {
        _areaUse.Cancel();
        _slot = index;
        _target = null;
        _dirty = true;
        _popup.gameObject.SetActive(false);
        if (!_items.TryGetItem(index, out ConsumableItemData item)) return;
        if (item.TargetMode == ConsumableTargetMode.Area) BeginAreaUse();
        else if (item.TargetMode == ConsumableTargetMode.Single) ShowTargets();
    }

    private void ShowCatalog()
    {
        if (_items == null || !_items.IsInitialized) return;
        OpenPopup("테스트 아이템 추가 · 무료");
        foreach (ConsumableItemData item in _items.Catalog.Items)
            Choice(item.DisplayName, () => Report(_items.TryAdd(item) ? "아이템을 추가했습니다." : "빈 슬롯이 없거나 추가할 수 없습니다."));
    }

    private void ShowTargets()
    {
        if (_items == null || !_items.TryGetItem(_slot, out ConsumableItemData item)) return;
        OpenPopup("사용 대상 선택");
        AddTargets(_units.AllyUnits, item);
        AddTargets(_units.EnemyUnits, item);
        if (_choices.childCount == 0) Choice("선택 가능한 유닛이 없습니다.", null).interactable = false;
    }

    private void AddTargets(IReadOnlyList<Unit_Gateway> units, ConsumableItemData item)
    {
        foreach (Unit_Gateway unit in units)
        {
            if (unit == null || !unit.IsAlive || !unit.IsTargetable) continue;
            if (item.TargetTeam == ConsumableTargetTeam.Ally && unit.Team != UnitTeam.Ally) continue;
            if (item.TargetTeam == ConsumableTargetTeam.Enemy && unit.Team != UnitTeam.Enemy) continue;
            Choice($"{unit.name} · HP {unit.CurrentHp:0.#}", () => { _target = unit; _popup.gameObject.SetActive(false); });
        }
    }

    private void UseSelected()
    {
        if (_items == null || !_items.TryGetItem(_slot, out ConsumableItemData item)) return;
        _popup.gameObject.SetActive(false);
        try
        {
            if (item.TargetMode == ConsumableTargetMode.Area) BeginAreaUse();
            else Report(_items.TryUse(_slot, _target) ? "사용 완료 · 아이템 1개 소모" : "적용 가능한 대상과 효과 설정을 확인하세요.");
        }
        catch (Exception exception) { Report("사용 오류 · Console을 확인하세요."); Debug.LogException(exception, this); }
    }

    private void OpenPopup(string title)
    {
        _areaUse.Cancel();
        Clear(_choices);
        _popupTitle.text = title;
        _popup.gameObject.SetActive(true);
    }

    private Button Choice(string title, Action action)
    {
        Button button = MakeButton(_choices, title, 0, 0, 390, 42, action);
        button.gameObject.AddComponent<LayoutElement>().preferredHeight = 42;
        return button;
    }

    private RectTransform Scroll(Transform parent, string name, float x, float y, float w, float h, bool horizontal)
    {
        RectTransform viewport = Box(parent, name, x, y, w, h, new Color(0, 0, 0, .12f));
        viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        RectTransform content = Box(viewport, "Content", 0, 0, w, h, Color.clear);
        Destroy(content.GetComponent<Image>());
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(horizontal ? 0 : 1, 1);
        content.pivot = new Vector2(0, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(horizontal ? w : 0, h);
        HorizontalOrVerticalLayoutGroup layout = horizontal ?
            (HorizontalOrVerticalLayoutGroup)content.gameObject.AddComponent<HorizontalLayoutGroup>() : content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = !horizontal;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        if (horizontal) fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        else fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = horizontal;
        scroll.vertical = !horizontal;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 25;
        return content;
    }

    private RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
        go.GetComponent<Image>().color = color;
        return rect;
    }

    private TMP_Text Label(Transform parent, string value, float x, float y, float w, float h, float size)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
        TMP_Text text = go.GetComponent<TMP_Text>();
        if (_font != null) text.font = _font;
        text.text = value;
        text.fontSize = size;
        text.color = new Color(.9f, .94f, .97f);
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private Button MakeButton(Transform parent, string label, float x, float y, float w, float h, Action action)
    {
        RectTransform rect = Box(parent, label.Length == 0 ? "Slot" : label, x, y, w, h, _button);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        if (action != null) button.onClick.AddListener(() => action());
        TMP_Text text = Label(rect, label, 4, 2, w - 8, h - 4, 14);
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private void Clear(Transform parent)
    {
        while (parent.childCount > 0)
        {
            Transform child = parent.GetChild(0);
            child.gameObject.SetActive(false);
            child.SetParent(null);
            Destroy(child.gameObject);
        }
    }

    private void Report(string message) { if (_message != null) _message.text = message; }
    private void OnEnable() { if (_canvas != null) _canvas.gameObject.SetActive(true); }
    private void OnDisable()
    {
        if (_areaUse != null) _areaUse.Cancel();
        if (_canvas != null) _canvas.gameObject.SetActive(false);
    }
    private void OnDestroy() { if (_items != null) _items.InventoryChanged -= RefreshInventory; }

    private T FindInScene<T>() where T : Component
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            foreach (T component in root.GetComponentsInChildren<T>(false))
            {
                if (component is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;
                return component;
            }
        return null;
    }
}
