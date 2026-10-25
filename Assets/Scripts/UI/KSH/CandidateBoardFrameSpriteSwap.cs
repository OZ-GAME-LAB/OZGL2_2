using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Candidate 루트의 기존 Button은 유지하면서 Board/Frame 두 Image를
/// 애니메이터 없이 즉시 Sprite Swap합니다.
/// Selected는 EventSystem 포커스가 아닌 이 컴포넌트의 선택 상태입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class CandidateBoardFrameSpriteSwap : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [Serializable]
    private sealed class SpriteSet
    {
        public Sprite normal;
        public Sprite highlighted;
        public Sprite pressed;
        public Sprite selected;
        public Sprite disabled;
    }

    private enum VisualState
    {
        Normal,
        Highlighted,
        Pressed,
        Selected,
        Disabled
    }

    [Header("Image References")]
    [SerializeField] private Image boardImage;
    [SerializeField] private Image frameImage;

    [Header("Board Sprites")]
    [SerializeField] private SpriteSet boardSprites = new SpriteSet();

    [Header("Frame Sprites")]
    [SerializeField] private SpriteSet frameSprites = new SpriteSet();

    [Header("Selection")]
    [Tooltip("클릭 시 이 Candidate를 선택하고 다른 Candidate의 선택을 해제합니다.")]
    [SerializeField] private bool selectOnClick = true;

    [Tooltip("비워 두면 같은 부모에 있는 Candidate들을 하나의 선택 그룹으로 처리합니다.")]
    [SerializeField] private Transform selectionGroupRoot;

    private Button _button;
    private Sprite _originalBoardSprite;
    private Sprite _originalFrameSprite;
    private bool _hovered;
    private bool _pressed;
    private bool _selected;
    private bool _wasInteractable;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _originalBoardSprite = boardImage != null ? boardImage.sprite : null;
        _originalFrameSprite = frameImage != null ? frameImage.sprite : null;

        // 기존 Button.onClick 이벤트 및 UIItemSlot의 Button 참조를 그대로 유지합니다.
        _button.onClick.AddListener(HandleClicked);

        if (GetComponent<Animator>() != null)
        {
            Debug.LogWarning(
                $"[{nameof(CandidateBoardFrameSpriteSwap)}] {name}: " +
                "기존 Animator를 제거하거나 비활성화하세요. Sprite를 덮어쓸 수 있습니다.", this);
        }
    }

    private void OnEnable()
    {
        if (_button == null)
            _button = GetComponent<Button>();

        // 기본 Button Transition과 중복 실행되지 않게 합니다.
        _button.transition = Selectable.Transition.None;

        _hovered = false;
        _pressed = false;
        _selected = false;
        _wasInteractable = _button.IsInteractable();
        ApplySprites();
    }

    private void OnDisable()
    {
        _hovered = false;
        _pressed = false;
        _selected = false;
        ApplyNormalSprites();
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(HandleClicked);
    }

    private void LateUpdate()
    {
        if (_button == null)
            return;

        bool interactable = _button.IsInteractable();
        if (interactable == _wasInteractable)
            return;

        _wasInteractable = interactable;
        if (!interactable)
            _pressed = false;

        ApplySprites();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        ApplySprites();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        _pressed = false;
        ApplySprites();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !CanInteract())
            return;

        _pressed = true;
        ApplySprites();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        _pressed = false;
        ApplySprites();
    }

    private void HandleClicked()
    {
        if (!selectOnClick || !CanInteract())
            return;

        Transform group = selectionGroupRoot != null
            ? selectionGroupRoot
            : transform.parent;

        if (group == null)
        {
            SetSelected(true);
            return;
        }

        var candidates = group.GetComponentsInChildren<CandidateBoardFrameSpriteSwap>(true);
        foreach (var candidate in candidates)
            candidate.SetSelected(candidate == this);
    }

    /// <summary>게임 로직에서 유물 선택 상태를 직접 동기화할 때 호출.</summary>
    public void SetSelected(bool selected)
    {
        _selected = selected;
        ApplySprites();
    }

    /// <summary>
    /// 새 유물 보상창을 열 때 호출하면 이전 Hover/Pressed/Selected를 제거합니다.
    /// </summary>
    public void ResetVisuals()
    {
        _hovered = false;
        _pressed = false;
        _selected = false;
        ApplyNormalSprites();
    }

    private bool CanInteract()
    {
        return isActiveAndEnabled && _button != null && _button.IsInteractable();
    }

    private void ApplySprites()
    {
        if (_button == null)
            return;

        VisualState state;
        if (!CanInteract())
            state = VisualState.Disabled;
        else if (_pressed)
            state = VisualState.Pressed;
        else if (_selected)
            state = VisualState.Selected;
        else if (_hovered)
            state = VisualState.Highlighted;
        else
            state = VisualState.Normal;

        ApplyImage(boardImage, boardSprites, _originalBoardSprite, state);
        ApplyImage(frameImage, frameSprites, _originalFrameSprite, state);
    }

    private void ApplyNormalSprites()
    {
        ApplyImage(boardImage, boardSprites, _originalBoardSprite, VisualState.Normal);
        ApplyImage(frameImage, frameSprites, _originalFrameSprite, VisualState.Normal);
    }

    private static void ApplyImage(Image image, SpriteSet set, Sprite original, VisualState state)
    {
        if (image == null || set == null)
            return;

        Sprite normal = set.normal != null ? set.normal : original;
        Sprite sprite = normal;

        switch (state)
        {
            case VisualState.Highlighted:
                sprite = set.highlighted != null ? set.highlighted : normal;
                break;
            case VisualState.Pressed:
                sprite = set.pressed != null ? set.pressed : normal;
                break;
            case VisualState.Selected:
                sprite = set.selected != null ? set.selected : normal;
                break;
            case VisualState.Disabled:
                sprite = set.disabled != null ? set.disabled : normal;
                break;
        }

        // Image.sprite를 손상시키지 않고 즉시 표시 스프라이트만 교체합니다.
        image.overrideSprite = sprite;
    }
}
