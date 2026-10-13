using System;
using UnityEngine;
using UnityEngine.EventSystems;

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

    public bool IsSelected { get; private set; }
    public bool IsHovered { get; private set; }

    public event Action<AltarButtonUI, Vector2> PointerEntered;
    public event Action<AltarButtonUI, Vector2> PointerMoved;
    public event Action<AltarButtonUI> PointerExited;
    public event Action<AltarButtonUI> Clicked;


    private void Awake()
    {
        SetSelected(false);
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


    private void RefreshVisual()
    {
        // 선택 상태가 가장 우선
        if (enableState != null)
        {
            enableState.SetActive(
                IsSelected
            );
        }

        if (mouseOverState != null)
        {
            mouseOverState.SetActive(
                !IsSelected &&
                IsHovered
            );
        }

        if (disabledState != null)
        {
            disabledState.SetActive(
                !IsSelected &&
                !IsHovered
            );
        }
    }
}