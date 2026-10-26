using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TotemButtonUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerMoveHandler,
    IPointerClickHandler
{
    [Header("Totem")]
    [SerializeField] private TotemId totemId;

    [Header("Major Totem - Toggle Visual")]
    [SerializeField] private GameObject offState;
    [SerializeField] private GameObject mouseOverState;
    [SerializeField] private GameObject enableState;

    [Header("Small Totem - Level Visual")]
    [Tooltip("0, 1, 2, 3 순서")]
    [SerializeField] private GameObject[] levelStates;

    [Tooltip("작은 토템용 Hover 오버레이가 있으면 연결. 없어도 됨.")]
    [SerializeField] private GameObject levelMouseOverOverlay;

    [Header("Small Totem Runtime Tint")]
    [Tooltip("체크하면 작은 토템의 Image Color를 런타임에도 이 색으로 유지합니다.")]
    [SerializeField] private bool forceLevelTint = true;

    [SerializeField]
    private Color levelTint =
        new Color(0.72f, 0.72f, 0.72f, 1f);


    public TotemId TotemId => totemId;
    public bool IsHovered { get; private set; }

    public event Action<TotemButtonUI, Vector2> PointerEntered;
    public event Action<TotemButtonUI, Vector2> PointerMoved;
    public event Action<TotemButtonUI> PointerExited;

    public event Action<
        TotemButtonUI,
        PointerEventData.InputButton
    > Clicked;


    private TotemSelectionMode selectionMode;
    private int currentLevel;
    private int maxLevel;
    private bool initialized;


    public bool ValidateBaseReferences()
    {
        if (totemId != TotemId.None)
            return true;

        Debug.LogError(
            "[TotemButtonUI] TotemId를 지정해주세요.",
            this
        );

        return false;
    }


    public bool Initialize(
        TotemData data,
        int level)
    {
        if (data == null)
            return false;

        selectionMode = data.SelectionMode;

        maxLevel =
            selectionMode == TotemSelectionMode.Toggle
            ? 1
            : data.MaxLevel;

        if (!ValidateVisualReferences())
        {
            initialized = false;
            return false;
        }

        DisableLegacyButtons();

        initialized = true;

        currentLevel =
            Mathf.Clamp(level, 0, maxLevel);

        IsHovered = false;

        RefreshVisual();

        return true;
    }


    private bool ValidateVisualReferences()
    {
        if (selectionMode == TotemSelectionMode.Toggle)
        {
            bool valid =
                offState != null &&
                mouseOverState != null &&
                enableState != null;

            if (!valid)
            {
                Debug.LogError(
                    "[TotemButtonUI] 단일 토템은 Off / MouseOver / Enable을 모두 연결해주세요.",
                    this
                );
            }

            return valid;
        }

        if (levelStates == null ||
            levelStates.Length < maxLevel + 1)
        {
            Debug.LogError(
                "[TotemButtonUI] 레벨 토템은 0~3 상태를 순서대로 연결해주세요.",
                this
            );

            return false;
        }

        for (int i = 0; i <= maxLevel; i++)
        {
            if (levelStates[i] == null)
            {
                Debug.LogError(
                    "[TotemButtonUI] Level " +
                    i +
                    " 상태가 비어 있습니다.",
                    this
                );

                return false;
            }
        }

        return true;
    }


    private void DisableLegacyButtons()
    {
        DisableButtonsIn(offState);
        DisableButtonsIn(mouseOverState);
        DisableButtonsIn(enableState);

        if (levelStates == null)
            return;

        for (int i = 0;
            i < levelStates.Length;
            i++)
        {
            DisableButtonsIn(
                levelStates[i]
            );
        }
    }


    private void DisableButtonsIn(
        GameObject target)
    {
        if (target == null)
            return;

        Button[] buttons =
            target.GetComponentsInChildren<Button>(
                true
            );

        for (int i = 0;
            i < buttons.Length;
            i++)
        {
            buttons[i].enabled = false;
        }
    }


    public void SetLevel(int level)
    {
        currentLevel =
            Mathf.Clamp(level, 0, maxLevel);

        RefreshVisual();
    }


    public void OnPointerEnter(
        PointerEventData eventData)
    {
        if (!initialized)
            return;

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
        if (!initialized ||
            !IsHovered)
            return;

        PointerMoved?.Invoke(
            this,
            eventData.position
        );
    }


    public void OnPointerExit(
        PointerEventData eventData)
    {
        if (!initialized)
            return;

        IsHovered = false;

        RefreshVisual();

        PointerExited?.Invoke(this);
    }


    public void OnPointerClick(
        PointerEventData eventData)
    {
        if (!initialized)
            return;

        if (eventData.button !=
                PointerEventData.InputButton.Left &&
            eventData.button !=
                PointerEventData.InputButton.Right)
        {
            return;
        }

        Clicked?.Invoke(
            this,
            eventData.button
        );
    }


    private void RefreshVisual()
    {
        if (selectionMode ==
            TotemSelectionMode.Toggle)
        {
            // 선택된 상태가 Hover보다 우선
            bool enabled =
                currentLevel > 0;

            if (offState != null)
                offState.SetActive(
                    !enabled &&
                    !IsHovered
                );

            if (mouseOverState != null)
                mouseOverState.SetActive(
                    !enabled &&
                    IsHovered
                );

            if (enableState != null)
                enableState.SetActive(
                    enabled
                );


            if (levelStates != null)
            {
                for (int i = 0;
                    i < levelStates.Length;
                    i++)
                {
                    if (levelStates[i] != null)
                        levelStates[i]
                            .SetActive(false);
                }
            }

            if (levelMouseOverOverlay != null)
                levelMouseOverOverlay
                    .SetActive(false);

            return;
        }


        // -----------------------------
        // 3단 작은 토템
        // -----------------------------

        if (offState != null)
            offState.SetActive(false);

        if (mouseOverState != null)
            mouseOverState.SetActive(false);

        if (enableState != null)
            enableState.SetActive(false);


        bool showLevelHover = IsHovered && currentLevel == 0;
        int hoverLevelIndex = levelMouseOverOverlay == null
            ? -1
            : Array.IndexOf(levelStates, levelMouseOverOverlay);
        // 단계 이미지를 Hover 이미지로 함께 사용해도 선택된 단계를 끄지 않습니다.
        int displayLevel = showLevelHover && hoverLevelIndex >= 0
            ? hoverLevelIndex
            : currentLevel;

        for (int i = 0;
            i < levelStates.Length;
            i++)
        {
            if (levelStates[i] == null)
                continue;

            levelStates[i].SetActive(
                i == displayLevel
            );
        }


        // 단계 이미지와 별개의 오버레이만 따로 제어합니다.
        if (levelMouseOverOverlay != null && hoverLevelIndex < 0)
        {
            levelMouseOverOverlay.SetActive(
                showLevelHover
            );
        }


        ApplyLevelTint();
    }


    private void ApplyLevelTint()
    {
        if (!forceLevelTint ||
            levelStates == null)
        {
            return;
        }

        for (int i = 0;
            i < levelStates.Length;
            i++)
        {
            GameObject state =
                levelStates[i];

            if (state == null)
                continue;

            Image[] images =
                state.GetComponentsInChildren<Image>(
                    true
                );

            for (int j = 0;
                j < images.Length;
                j++)
            {
                images[j].color =
                    IsHovered && currentLevel == 0
                        ? Color.white
                        : levelTint;
            }
        }
    }
}