using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AltarButtonUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerMoveHandler,
    IPointerClickHandler
{
    [Header("Visual States")]
    [SerializeField] private GameObject disabledState;
    [SerializeField] private GameObject mouseOverState;
    [SerializeField] private GameObject enableState;

    [Header("Locked Appearance")]
    [Tooltip("미해금 상태에서 기본 이미지의 원래 색상에 곱할 색상입니다.")]
    [SerializeField] private Color lockedColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    public bool IsUnlocked { get; private set; } = true;

    private Image[] _defaultImages;
    private Color[] _defaultColors;

    public bool IsSelected { get; private set; }
    public bool IsHovered { get; private set; }

    public event Action<AltarButtonUI, Vector2> PointerEntered;
    public event Action<AltarButtonUI, Vector2> PointerMoved;
    public event Action<AltarButtonUI> PointerExited;
    public event Action<AltarButtonUI> Clicked;


    private void Awake()
    {
        SetSelected(IsSelected);
        SetHovered(false);
    }


    public bool ValidateReferences()
    {
        bool valid =
            disabledState != null &&
            mouseOverState != null &&
            enableState != null;

        if (!valid)
        {
            Debug.LogError(
                "[AltarButtonUI] Disabled / MouseOver / Enable 상태를 모두 연결해주세요.",
                this
            );
        }

        return valid;
    }


    public void OnPointerEnter(
        PointerEventData eventData)
    {
        IsHovered = true;

        RefreshVisual();

        PointerEntered?.Invoke(
            this,
            eventData.position
        );
    }


    public void OnPointerMove(
        PointerEventData eventData)
    {
        if (!IsHovered)
            return;

        PointerMoved?.Invoke(
            this,
            eventData.position
        );
    }


    public void OnPointerExit(
        PointerEventData eventData)
    {
        IsHovered = false;

        RefreshVisual();

        PointerExited?.Invoke(this);
    }


    public void OnPointerClick(
        PointerEventData eventData)
    {
        if (eventData.button !=
            PointerEventData.InputButton.Left)
        {
            return;
        }

        Clicked?.Invoke(this);
    }


    public void SetSelected(
        bool selected)
    {
        IsSelected = selected;

        RefreshVisual();
    }


    public void SetHovered(
        bool hovered)
    {
        IsHovered = hovered;

        RefreshVisual();
    }


    public void SetUnlocked(bool unlocked)
    {
        IsUnlocked = unlocked;
        RefreshVisual();
    }

    private void RefreshDefaultColor()
    {
        if (disabledState == null) return;

        // Initialize가 Awake보다 먼저 호출되어도 원래 색상은 한 번만 보관합니다.
        if (_defaultImages == null)
        {
            _defaultImages = disabledState.GetComponentsInChildren<Image>(true);
            _defaultColors = new Color[_defaultImages.Length];
            for (int i = 0; i < _defaultImages.Length; i++)
                _defaultColors[i] = _defaultImages[i].color;
        }

        for (int i = 0; i < _defaultImages.Length; i++)
        {
            if (_defaultImages[i] != null)
                _defaultImages[i].color = IsUnlocked
                    ? _defaultColors[i]
                    : _defaultColors[i] * lockedColor;
        }
    }

    private void RefreshVisual()
    {
        // 미해금은 기본 이미지를 유지합니다. 포인터 이벤트는 툴팁에 계속 전달합니다.
        RefreshDefaultColor();
        if (enableState != null)
        {
            enableState.SetActive(
                IsUnlocked && IsSelected
            );
        }

        if (mouseOverState != null)
        {
            mouseOverState.SetActive(
                IsUnlocked && !IsSelected &&
                IsHovered
            );
        }

        if (disabledState != null)
        {
            disabledState.SetActive(
                !IsUnlocked || (!IsSelected &&
                !IsHovered)
            );
        }
    }
}
