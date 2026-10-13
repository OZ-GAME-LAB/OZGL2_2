using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AltarSelectionUI : MonoBehaviour
{
    [Header("Hover Info")]
    [SerializeField]
    private RectTransform hoverInfoPanel;

    [SerializeField]
    private TMP_Text hoverTitleText;

    [SerializeField]
    private TMP_Text hoverDescriptionText;


    [Header("Selected Altar Info")]
    [SerializeField]
    private GameObject selectedInfoPanel;

    [Header("Totem Info")]
    [SerializeField] private GameObject totemInfoPanel;

    [SerializeField]
    private TMP_Text selectedTitleText;

    [SerializeField]
    private TMP_Text selectedDescriptionText;


    [Header("Hover Position")]
    [SerializeField]
    private float mouseOffsetX = 30f;

    [SerializeField]
    private float mouseOffsetY = 30f;


    private AltarButtonUI[] altarButtons;

    private AltarButtonUI hoveredAltar;
    private AltarButtonUI selectedAltar;

    private Canvas rootCanvas;
    private RectTransform hoverPanelParent;


    private void Awake()
    {
        rootCanvas = GetComponentInParent<Canvas>();

        altarButtons =
            GetComponentsInChildren<AltarButtonUI>(true);

        if (hoverInfoPanel != null)
        {
            hoverPanelParent =
                hoverInfoPanel.parent as RectTransform;

            hoverInfoPanel.gameObject.SetActive(false);
        }

        if (selectedInfoPanel != null)
            selectedInfoPanel.SetActive(false);

        RegisterButtons();
    }


    private void OnDestroy()
    {
        UnregisterButtons();
    }


    // =====================================================
    // Register
    // =====================================================

    private void RegisterButtons()
    {
        foreach (AltarButtonUI altar in altarButtons)
        {
            if (altar == null)
                continue;

            altar.PointerEntered += OnPointerEntered;
            altar.PointerMoved += OnPointerMoved;
            altar.PointerExited += OnPointerExited;
            altar.Clicked += OnClicked;

            altar.SetSelected(false);
        }
    }


    private void UnregisterButtons()
    {
        if (altarButtons == null)
            return;

        foreach (AltarButtonUI altar in altarButtons)
        {
            if (altar == null)
                continue;

            altar.PointerEntered -= OnPointerEntered;
            altar.PointerMoved -= OnPointerMoved;
            altar.PointerExited -= OnPointerExited;
            altar.Clicked -= OnClicked;
        }
    }


    // =====================================================
    // Hover
    // =====================================================

    private void OnPointerEntered(
        AltarButtonUI altar,
        Vector2 mousePosition)
    {
        hoveredAltar = altar;

        ShowHoverInfo(
            altar,
            mousePosition
        );
    }


    private void OnPointerMoved(
        AltarButtonUI altar,
        Vector2 mousePosition)
    {
        if (hoveredAltar != altar)
            return;

        UpdateHoverPanelPosition(
            mousePosition
        );
    }


    private void OnPointerExited(
        AltarButtonUI altar)
    {
        if (hoveredAltar != altar)
            return;

        hoveredAltar = null;

        HideHoverInfo();
    }


    private void ShowHoverInfo(
        AltarButtonUI altar,
        Vector2 mousePosition)
    {
        if (hoverInfoPanel == null)
            return;

        if (hoverTitleText != null)
            hoverTitleText.text =
                altar.AltarName;

        if (hoverDescriptionText != null)
            hoverDescriptionText.text =
                altar.AltarDescription;

        hoverInfoPanel.gameObject.SetActive(true);

        // ContentSizeFitter / Layout 사용 시
        // 실제 패널 크기 즉시 계산
        LayoutRebuilder.ForceRebuildLayoutImmediate(
            hoverInfoPanel
        );

        UpdateHoverPanelPosition(
            mousePosition
        );
    }


    private void HideHoverInfo()
    {
        if (hoverInfoPanel != null)
            hoverInfoPanel.gameObject.SetActive(false);
    }


    // =====================================================
    // Hover Position
    // =====================================================

    private void UpdateHoverPanelPosition(
        Vector2 mouseScreenPosition)
    {
        if (hoverInfoPanel == null ||
            hoverPanelParent == null)
            return;


        // -----------------------------------------
        // 1. 기본 위치
        //
        // 마우스 우상단
        //
        // Pivot = 좌하단
        // Mouse + (30, 30)
        // -----------------------------------------

        hoverInfoPanel.pivot =
            new Vector2(0f, 0f);

        Vector2 defaultScreenPosition =
            mouseScreenPosition +
            new Vector2(
                mouseOffsetX,
                mouseOffsetY
            );

        SetPanelScreenPosition(
            defaultScreenPosition
        );


        // -----------------------------------------
        // 2. 실제 패널 오른쪽 끝 확인
        // -----------------------------------------

        Vector3[] corners =
            new Vector3[4];

        hoverInfoPanel.GetWorldCorners(corners);

        Camera uiCamera = GetUICamera();

        Vector2 upperRightScreen =
            RectTransformUtility.WorldToScreenPoint(
                uiCamera,
                corners[2]
            );


        // -----------------------------------------
        // 3. 오른쪽 화면 밖으로 나가면
        //
        // 마우스 좌상단으로 뒤집기
        // -----------------------------------------

        if (upperRightScreen.x > Screen.width)
        {
            hoverInfoPanel.pivot =
                new Vector2(1f, 0f);

            Vector2 flippedScreenPosition =
                mouseScreenPosition +
                new Vector2(
                    -mouseOffsetX,
                    mouseOffsetY
                );

            SetPanelScreenPosition(
                flippedScreenPosition
            );
        }
    }


    private void SetPanelScreenPosition(
        Vector2 screenPosition)
    {
        Camera uiCamera = GetUICamera();

        if (
            RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    hoverPanelParent,
                    screenPosition,
                    uiCamera,
                    out Vector2 localPoint
                )
        )
        {
            hoverInfoPanel.anchoredPosition =
                localPoint;
        }
    }


    private Camera GetUICamera()
    {
        if (rootCanvas == null)
            return null;

        if (
            rootCanvas.renderMode ==
            RenderMode.ScreenSpaceOverlay
        )
        {
            return null;
        }

        return rootCanvas.worldCamera;
    }


    // =====================================================
    // Selection
    // =====================================================

    private void OnClicked(
        AltarButtonUI altar)
    {
        SelectAltar(altar);
    }


    private void SelectAltar(
        AltarButtonUI altar)
    {
        if (altar == null)
            return;

        if (selectedAltar == altar)
            return;


        // 기존 선택 해제
        if (selectedAltar != null)
        {
            selectedAltar.SetSelected(false);
        }


        // 새 제단 선택
        selectedAltar = altar;

        selectedAltar.SetSelected(true);

        UpdateSelectedInfo();
    }


    private void UpdateSelectedInfo()
    {
        if (selectedAltar == null)
        {
            if (selectedInfoPanel != null)
                selectedInfoPanel.SetActive(false);

            return;
        }


        if (selectedTitleText != null)
        {
            selectedTitleText.text =
                selectedAltar.AltarName;
        }


        if (selectedDescriptionText != null)
        {
            selectedDescriptionText.text =
                selectedAltar.AltarDescription;
        }


        if (selectedInfoPanel != null)
        {
            selectedInfoPanel.SetActive(true);
        }
    }


    public AltarButtonUI SelectedAltar
        => selectedAltar;
}