using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class OutGameTotemView : MonoBehaviour
{
    [Header("Totems")]
    [SerializeField]
    private TotemButtonUI[] totemButtons;


    [Header("Active Totem Effects")]
    [SerializeField]
    private GameObject totemInfoPanel;

    [SerializeField]
    private TMP_Text activeEffectsTitleText;

    [SerializeField]
    private TMP_Text activeEffectsText;

    [SerializeField]
    private ScrollRect activeEffectsScrollRect;

    [SerializeField]
    private RectTransform activeEffectsContent;


    [Header("Reward Bonus")]
    [SerializeField]
    private Image bonusFillImage;

    [SerializeField]
    private TMP_Text bonusPercentText;


    [Header("Hover Info")]
    [SerializeField]
    private RectTransform hoverInfoPanel;

    [SerializeField]
    private TMP_Text hoverTitleText;

    [SerializeField]
    private TMP_Text hoverDescriptionText;


    [Header("Hover Position")]
    [SerializeField]
    private float mouseOffsetX = 30f;

    [SerializeField]
    private float mouseOffsetY = 30f;


    [Header("Navigation")]
    [SerializeField]
    private Button backButton;

    [SerializeField]
    private Button startButton;


    // 예전 버튼.
    // Inspector 참조가 남아 있어도 상관없음.
    // GameObject는 비활성화해도 됨.
    [Header("Legacy - Not Used")]
    [SerializeField]
    private Button leftButton;

    [SerializeField]
    private Button rightButton;


    private ITotemSelection selection;

    private TotemButtonUI hoveredTotem;

    private Vector2 lastHoverMousePosition;


    private Canvas rootCanvas;

    private RectTransform hoverPanelParent;

    private bool activeEffectsInitialized;


    public event Action<TotemId, int>
        LevelChangeRequested;

    public event Action BackRequested;

    public event Action StartRequested;


    // =====================================================
    // Validation
    // =====================================================

    public bool ValidateReferences()
    {
        bool valid =
            totemButtons != null &&
            totemButtons.Length == 8 &&
            backButton != null &&
            startButton != null;


        if (!valid)
        {
            Debug.LogError(
                "[OutGameTotemView] 토템 8개와 Back / Start 버튼 참조를 확인해주세요.",
                this
            );

            return false;
        }


        HashSet<TotemId> ids =
            new HashSet<TotemId>();


        for (int i = 0;
            i < totemButtons.Length;
            i++)
        {
            TotemButtonUI button =
                totemButtons[i];

            if (button == null ||
                !button.ValidateBaseReferences())
            {
                valid = false;
                continue;
            }


            if (!ids.Add(button.TotemId))
            {
                Debug.LogError(
                    "[OutGameTotemView] TotemId가 중복되었습니다: " +
                    button.TotemId,
                    button
                );

                valid = false;
            }
        }


        if (totemInfoPanel == null ||
            activeEffectsTitleText == null ||
            activeEffectsText == null ||
            activeEffectsScrollRect == null ||
            activeEffectsContent == null)
        {
            Debug.LogError(
                "[OutGameTotemView] 현재 활성화된 토템 효과 목록 UI 참조를 확인해주세요.",
                this
            );
        }


        if (bonusFillImage == null ||
            bonusPercentText == null)
        {
            Debug.LogError(
                "[OutGameTotemView] 누적 보너스 Fill Bar 참조를 확인해주세요.",
                this
            );
        }


        if (hoverInfoPanel == null ||
            hoverTitleText == null ||
            hoverDescriptionText == null)
        {
            Debug.LogError(
                "[OutGameTotemView] Hover 정보 UI 참조를 확인해주세요.",
                this
            );
        }


        return valid;
    }


    // =====================================================
    // Initialize
    // =====================================================

    public void Initialize(
        ITotemSelection selection)
    {
        Shutdown();


        if (!ValidateReferences() ||
            selection == null)
        {
            return;
        }


        this.selection = selection;


        rootCanvas =
            GetComponentInParent<Canvas>();

        if (rootCanvas != null)
            rootCanvas =
                rootCanvas.rootCanvas;


        if (hoverInfoPanel != null)
        {
            hoverPanelParent =
                hoverInfoPanel.parent
                as RectTransform;

            hoverInfoPanel.gameObject
                .SetActive(false);
        }


        if (totemInfoPanel != null)
        {
            totemInfoPanel
                .SetActive(true);
        }


        if (activeEffectsTitleText != null)
        {
            activeEffectsTitleText.text =
                "현재 활성화 됨";
        }


        ConfigureBonusFill();


        hoveredTotem = null;

        lastHoverMousePosition =
            Vector2.zero;

        activeEffectsInitialized =
            false;


        for (int i = 0;
            i < totemButtons.Length;
            i++)
        {
            TotemButtonUI button =
                totemButtons[i];

            if (button == null)
                continue;


            TotemData data =
                FindData(button.TotemId);


            if (data == null)
            {
                Debug.LogError(
                    "[OutGameTotemView] TotemData를 찾을 수 없습니다: " +
                    button.TotemId,
                    button
                );

                continue;
            }

            button.gameObject
                .SetActive(data.IsAvailable);


            button.Initialize(
                data,
                selection.GetLevel(data.Id)
            );


            button.PointerEntered +=
                OnPointerEntered;

            button.PointerMoved +=
                OnPointerMoved;

            button.PointerExited +=
                OnPointerExited;

            button.Clicked +=
                OnTotemClicked;
        }


        backButton.onClick
            .AddListener(OnBackClicked);

        startButton.onClick
            .AddListener(OnStartClicked);


        Refresh();
    }


    // =====================================================
    // Refresh
    // =====================================================

    public void Refresh()
    {
        if (selection == null)
            return;


        for (int i = 0;
            i < totemButtons.Length;
            i++)
        {
            TotemButtonUI button =
                totemButtons[i];

            if (button == null)
                continue;


            TotemData data =
                FindData(button.TotemId);


            if (data == null ||
                !data.IsAvailable)
            {
                button.gameObject
                    .SetActive(false);

                continue;
            }


            button.gameObject
                .SetActive(true);


            button.SetLevel(
                selection.GetLevel(data.Id)
            );
        }


        RefreshActiveEffects();

        RefreshBonus();

        RefreshHoveredInfo();
    }


    // =====================================================
    // Click
    // =====================================================

    private void OnTotemClicked(
        TotemButtonUI button,
        PointerEventData.InputButton mouseButton)
    {
        if (selection == null ||
            button == null)
        {
            return;
        }


        TotemData data =
            FindData(button.TotemId);


        if (data == null ||
            !data.IsAvailable)
            return;


        int currentLevel =
            selection.GetLevel(data.Id);


        int delta = 0;


        // -----------------------------------------
        // 큰 토템
        // OFF <-> ENABLE
        // -----------------------------------------

        if (data.SelectionMode ==
            TotemSelectionMode.Toggle)
        {
            if (mouseButton ==
                PointerEventData.InputButton.Left)
            {
                delta =
                    currentLevel == 0
                    ? 1
                    : -1;
            }
            else if (
                mouseButton ==
                PointerEventData.InputButton.Right &&
                currentLevel > 0)
            {
                delta = -1;
            }
        }

        // -----------------------------------------
        // 작은 3단 토템
        // 좌클릭: 0 -> 1 -> 2 -> 3 -> 0
        // 우클릭: 3 -> 2 -> 1 -> 0
        // -----------------------------------------

        else
        {
            if (mouseButton ==
                PointerEventData.InputButton.Left)
            {
                if (currentLevel <
                    data.MaxLevel)
                {
                    delta = 1;
                }
                else
                {
                    delta =
                        -currentLevel;
                }
            }
            else if (
                mouseButton ==
                PointerEventData.InputButton.Right &&
                currentLevel > 0)
            {
                delta = -1;
            }
        }


        if (delta != 0)
        {
            LevelChangeRequested?.Invoke(
                data.Id,
                delta
            );
        }


        Refresh();
    }


    // =====================================================
    // Active Totem Effects
    // =====================================================

    private void RefreshActiveEffects()
    {
        if (selection == null ||
            activeEffectsText == null)
        {
            return;
        }


        float previousScrollPosition =
            1f;


        if (activeEffectsInitialized &&
            activeEffectsScrollRect != null)
        {
            previousScrollPosition =
                activeEffectsScrollRect
                    .verticalNormalizedPosition;
        }


        StringBuilder builder =
            new StringBuilder();


        for (int i = 0;
            i < selection.Data.Count;
            i++)
        {
            TotemData data =
                selection.Data[i];


            if (data == null ||
                !data.IsAvailable)
                continue;


            int level =
                selection.GetLevel(
                    data.Id
                );


            if (level <= 0)
                continue;


            if (builder.Length > 0)
            {
                builder.Append(
                    "\n"
                );
            }


            builder.Append(
                data.DisplayName
            );

            builder.Append(
                " - "
            );

            builder.Append(
                BuildActiveDescription(
                    data,
                    level
                )
            );
        }


        if (builder.Length == 0)
        {
            activeEffectsText.text =
                "활성화된 토템 없음";
        }
        else
        {
            activeEffectsText.text =
                builder.ToString();
        }


        activeEffectsText
            .ForceMeshUpdate();


        Canvas.ForceUpdateCanvases();


        if (activeEffectsContent != null)
        {
            LayoutRebuilder
                .ForceRebuildLayoutImmediate(
                    activeEffectsContent
                );
        }


        Canvas.ForceUpdateCanvases();


        if (activeEffectsScrollRect != null)
        {
            activeEffectsScrollRect
                .StopMovement();

            activeEffectsScrollRect
                .verticalNormalizedPosition =
                Mathf.Clamp01(
                    previousScrollPosition
                );
        }


        activeEffectsInitialized =
            true;
    }


    private string BuildActiveDescription(
        TotemData data,
        int level)
    {
        string text =
            data.ActiveDescription;


        if (string.IsNullOrWhiteSpace(
            text))
        {
            text =
                data.Description;
        }


        float totalValue =
            data.ValuePerLevel *
            level;


        text =
            text.Replace(
                "{level}",
                level.ToString()
            );


        text =
            text.Replace(
                "{max}",
                data.MaxLevel.ToString()
            );


        text =
            text.Replace(
                "{value}",
                totalValue.ToString("0.##")
            );


        text =
            text.Replace(
                "{unit}",
                data.ValueUnit ??
                string.Empty
            );


        return text;
    }


    // =====================================================
    // Reward Bonus
    // =====================================================

    private void ConfigureBonusFill()
    {
        if (bonusFillImage == null)
            return;


        bonusFillImage.type =
            Image.Type.Filled;


        bonusFillImage.fillMethod =
            Image.FillMethod.Horizontal;


        bonusFillImage.fillOrigin =
            (int)Image.OriginHorizontal.Left;


        bonusFillImage.fillAmount =
            0f;
    }


    private void RefreshBonus()
    {
        if (selection == null)
            return;


        int currentPercent =
            selection
                .GetRewardBonusPercent();


        int maxPercent =
            GetMaximumRewardBonusPercent();


        if (bonusFillImage != null)
        {
            bonusFillImage.fillAmount =
                maxPercent > 0
                ? Mathf.Clamp01(
                    (float)currentPercent /
                    maxPercent
                )
                : 0f;
        }


        if (bonusPercentText != null)
        {
            bonusPercentText.text =
                currentPercent +
                "%";
        }
    }


    private int GetMaximumRewardBonusPercent()
    {
        if (selection == null)
            return 0;


        long maximumBonus =
            0;


        for (int i = 0;
            i < selection.Data.Count;
            i++)
        {
            TotemData data =
                selection.Data[i];


            if (data == null ||
                !data.IsAvailable)
                continue;


            maximumBonus +=
                data.GetRewardBonusPercent(
                    data.MaxLevel
                );
        }


        return (int)Math.Min(
            maximumBonus,
            int.MaxValue
        );
    }


    // =====================================================
    // Hover
    // =====================================================

    private void OnPointerEntered(
        TotemButtonUI button,
        Vector2 mousePosition)
    {
        hoveredTotem =
            button;


        lastHoverMousePosition =
            mousePosition;


        ShowHoverInfo(
            button,
            mousePosition
        );
    }


    private void OnPointerMoved(
        TotemButtonUI button,
        Vector2 mousePosition)
    {
        if (hoveredTotem != button)
            return;


        lastHoverMousePosition =
            mousePosition;


        UpdateHoverPanelPosition(
            mousePosition
        );
    }


    private void OnPointerExited(
        TotemButtonUI button)
    {
        if (hoveredTotem != button)
            return;


        hoveredTotem = null;


        if (hoverInfoPanel != null)
        {
            hoverInfoPanel.gameObject
                .SetActive(false);
        }
    }


    private void RefreshHoveredInfo()
    {
        if (hoveredTotem == null ||
            !hoveredTotem.IsHovered)
        {
            return;
        }


        ShowHoverInfo(
            hoveredTotem,
            lastHoverMousePosition
        );
    }


    private void ShowHoverInfo(
        TotemButtonUI button,
        Vector2 mousePosition)
    {
        if (selection == null ||
            button == null ||
            hoverInfoPanel == null ||
            hoverTitleText == null ||
            hoverDescriptionText == null)
        {
            return;
        }


        TotemData data =
            FindData(button.TotemId);


        if (data == null ||
            !data.IsAvailable)
        {
            hoverInfoPanel.gameObject
                .SetActive(false);

            return;
        }


        int level =
            selection.GetLevel(data.Id);


        hoverTitleText.text =
            data.DisplayName;


        if (data.SelectionMode ==
            TotemSelectionMode.Leveled)
        {
            hoverDescriptionText.text =
                data.Description +
                "\n\n현재 단계: " +
                level +
                " / " +
                data.MaxLevel;
        }
        else
        {
            hoverDescriptionText.text =
                data.Description +
                "\n\n현재 상태: " +
                (
                    level > 0
                    ? "활성화"
                    : "비활성화"
                );
        }


        hoverInfoPanel.gameObject
            .SetActive(true);


        hoverTitleText
            .ForceMeshUpdate();


        hoverDescriptionText
            .ForceMeshUpdate();


        Canvas.ForceUpdateCanvases();


        LayoutRebuilder
            .ForceRebuildLayoutImmediate(
                hoverInfoPanel
            );


        Canvas.ForceUpdateCanvases();


        UpdateHoverPanelPosition(
            mousePosition
        );
    }


    // =====================================================
    // Hover Position
    // =====================================================

    private void UpdateHoverPanelPosition(
        Vector2 mouseScreenPosition)
    {
        if (hoverInfoPanel == null ||
            hoverPanelParent == null)
        {
            return;
        }


        // 기본:
        // 마우스 우상단
        // 패널 Pivot = 좌하단

        hoverInfoPanel.pivot =
            new Vector2(0f, 0f);


        Vector2 position =
            mouseScreenPosition +
            new Vector2(
                mouseOffsetX,
                mouseOffsetY
            );


        SetHoverPanelScreenPosition(
            position
        );


        Vector3[] corners =
            new Vector3[4];


        hoverInfoPanel.GetWorldCorners(
            corners
        );


        Vector2 upperRight =
            RectTransformUtility
                .WorldToScreenPoint(
                    GetUICamera(),
                    corners[2]
                );


        // 오른쪽 화면 밖으로 나갈 때만
        // 마우스 좌측으로 뒤집음

        if (upperRight.x >
            Screen.width)
        {
            hoverInfoPanel.pivot =
                new Vector2(1f, 0f);


            position =
                mouseScreenPosition +
                new Vector2(
                    -mouseOffsetX,
                    mouseOffsetY
                );


            SetHoverPanelScreenPosition(
                position
            );
        }
    }


    private void SetHoverPanelScreenPosition(
        Vector2 screenPosition)
    {
        if (hoverPanelParent == null)
            return;


        if (
            RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    hoverPanelParent,
                    screenPosition,
                    GetUICamera(),
                    out Vector2 localPoint
                )
        )
        {
            hoverInfoPanel
                .anchoredPosition =
                localPoint;
        }
    }


    private Camera GetUICamera()
    {
        if (rootCanvas == null)
            return null;


        if (rootCanvas.renderMode ==
            RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }


        return rootCanvas.worldCamera;
    }


    // =====================================================
    // Data
    // =====================================================

    private TotemData FindData(
        TotemId id)
    {
        if (selection == null)
            return null;


        for (int i = 0;
            i < selection.Data.Count;
            i++)
        {
            if (selection.Data[i].Id ==
                id)
            {
                return selection.Data[i];
            }
        }


        return null;
    }


    // =====================================================
    // Navigation
    // =====================================================

    private void OnBackClicked()
    {
        BackRequested?.Invoke();
    }


    private void OnStartClicked()
    {
        StartRequested?.Invoke();
    }


    // =====================================================
    // Shutdown
    // =====================================================

    public void Shutdown()
    {
        if (totemButtons != null)
        {
            for (int i = 0;
                i < totemButtons.Length;
                i++)
            {
                TotemButtonUI button =
                    totemButtons[i];


                if (button == null)
                    continue;


                button.PointerEntered -=
                    OnPointerEntered;

                button.PointerMoved -=
                    OnPointerMoved;

                button.PointerExited -=
                    OnPointerExited;

                button.Clicked -=
                    OnTotemClicked;
            }
        }


        if (backButton != null)
        {
            backButton.onClick
                .RemoveListener(
                    OnBackClicked
                );
        }


        if (startButton != null)
        {
            startButton.onClick
                .RemoveListener(
                    OnStartClicked
                );
        }


        selection = null;

        hoveredTotem = null;

        lastHoverMousePosition =
            Vector2.zero;

        activeEffectsInitialized =
            false;


        if (hoverInfoPanel != null)
        {
            hoverInfoPanel.gameObject
                .SetActive(false);
        }
    }


    private void OnDestroy()
    {
        Shutdown();
    }
}