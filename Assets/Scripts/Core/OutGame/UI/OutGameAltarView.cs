using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class AltarButtonSlot
{
    public AltarId Id;
    public AltarButtonUI Button;
}

/// <summary>제단 버튼과 설명 표시입니다. 잠긴 제단의 미리보기와 실제 선택을 구분합니다.</summary>
public class OutGameAltarView : MonoBehaviour
{
    [SerializeField] private AltarButtonSlot[] _slots; //제단 정보가 들어있는 슬롯


    [Header("Selected Altar Info")]
    [SerializeField]
    private GameObject _selectedInfoPanel;

    [SerializeField]
    private TMP_Text _selectedTitleText;

    [SerializeField]
    private TMP_Text _selectedDescriptionText;


    [Header("Hover Info")]
    [SerializeField]
    private RectTransform _hoverInfoPanel;

    [SerializeField]
    private TMP_Text _hoverTitleText;

    [SerializeField]
    private TMP_Text _hoverDescriptionText;


    [Header("Hover Position")]
    [SerializeField]
    private float _mouseOffsetX = 30f;

    [SerializeField]
    private float _mouseOffsetY = 30f;


    [Header("Navigation")]
    [SerializeField]
    private Button _previousButton;

    [SerializeField]
    private Button _traitButton;

    [SerializeField]
    private Button _nextButton;


    private IAltarSelection _selection;

    private AltarButtonUI _hoveredAltar;

    private Canvas _rootCanvas;

    private RectTransform _hoverPanelParent;


    public event Action<AltarId> Selected;
    public event Action TraitRequested;
    public event Action NextRequested;


    /// <summary> 제단 패널에서 사용할 제단정보나 버튼들이 다 들어가 있는지 검사 </summary>
    public bool ValidateReferences()
    {
        bool valid =
            _slots != null &&
            _slots.Length > 0 &&

            _selectedInfoPanel != null &&
            _selectedTitleText != null &&
            _selectedDescriptionText != null &&

            _hoverInfoPanel != null &&
            _hoverTitleText != null &&
            _hoverDescriptionText != null &&

            _previousButton != null &&
            _traitButton != null &&
            _nextButton != null;


        if (valid)
        {
            HashSet<AltarId> ids =
                new HashSet<AltarId>();


            for (int i = 0;
                i < _slots.Length;
                i++)
            {
                if (_slots[i] == null ||
                    _slots[i].Id == AltarId.None ||
                    _slots[i].Button == null ||
                    !_slots[i].Button.ValidateReferences())
                {
                    valid = false;
                    continue;
                }


                if (!ids.Add(
                    _slots[i].Id))
                {
                    Debug.LogError(
                        "[OutGameAltarView] AltarId가 중복되었습니다: " +
                        _slots[i].Id,
                        this
                    );

                    valid = false;
                }
            }
        }


        if (!valid)
        {
            Debug.LogError(
                "[OutGameAltarView] Inspector의 슬롯과 UI 참조를 확인해주세요.",
                this
            );
        }


        return valid;
    }


    public void Initialize(
        IAltarSelection selection)
    {
        Shutdown();


        if (!ValidateReferences() ||
            selection == null)
        {
            return;
        }


        _selection =
            selection;


        _rootCanvas =
            GetComponentInParent<Canvas>();


        if (_rootCanvas != null)
        {
            _rootCanvas =
                _rootCanvas.rootCanvas;
        }


        _hoverPanelParent =
            _hoverInfoPanel.parent
            as RectTransform;


        _hoverInfoPanel
            .gameObject
            .SetActive(false);


        _selectedInfoPanel
            .SetActive(false);


        _hoveredAltar =
            null;


        //각 제단 버튼을 초기화하고 해당 버튼입력 이벤트를 연결한다
        for (int i = 0;
            i < _slots.Length;
            i++)
        {
            AltarButtonUI button =
                _slots[i].Button;


            button.PointerEntered +=
                OnPointerEntered;

            button.PointerMoved +=
                OnPointerMoved;

            button.PointerExited +=
                OnPointerExited;

            button.Clicked +=
                OnAltarClicked;
        }


        _previousButton.interactable =
            false;


        //이동 버튼과 실제 이동요청 이벤트 연결
        _traitButton.onClick
            .AddListener(
                OnTraitClicked
            );


        _nextButton.onClick
            .AddListener(
                OnNextClicked
            );


        Refresh();
    }


    public void Refresh()
    {
        if (_selection == null)
            return;


        // =================================================
        // 버튼 비주얼 동기화
        //
        // 화면이 자체적으로 선택 상태를 가지지 않고
        // 실제 Controller의 SelectedAltar만 따라갑니다.
        // =================================================

        for (int i = 0;
            i < _slots.Length;
            i++)
        {
            AltarButtonSlot slot =
                _slots[i];


            if (slot == null ||
                slot.Button == null)
            {
                continue;
            }


            bool selected =
                _selection.SelectedAltar ==
                slot.Id;


            slot.Button.SetUnlocked(_selection.IsUnlocked(slot.Id));

            slot.Button.SetSelected(
                selected
            );
        }


        RefreshSelectedInfo();
    }


    // =====================================================
    // Selected Altar Info
    // =====================================================

    private void RefreshSelectedInfo()
    {
        if (_selection == null)
            return;


        AltarData selected =
            FindData(
                _selection.SelectedAltar
            );


        if (selected == null)
        {
            if (_selectedInfoPanel != null)
            {
                _selectedInfoPanel
                    .SetActive(false);
            }

            return;
        }


        if (_selectedTitleText != null)
        {
            _selectedTitleText.text =
                selected.DisplayName;
        }


        if (_selectedDescriptionText != null)
        {
            _selectedDescriptionText.text =
                selected.Description;
        }


        if (_selectedInfoPanel != null)
        {
            _selectedInfoPanel
                .SetActive(true);
        }
    }


    // =====================================================
    // Hover
    // =====================================================

    private void OnPointerEntered(
        AltarButtonUI altar,
        Vector2 mousePosition)
    {
        _hoveredAltar =
            altar;


        ShowHoverInfo(
            altar,
            mousePosition
        );
    }


    private void OnPointerMoved(
        AltarButtonUI altar,
        Vector2 mousePosition)
    {
        if (_hoveredAltar !=
            altar)
        {
            return;
        }


        UpdateHoverPanelPosition(
            mousePosition
        );
    }


    private void OnPointerExited(
        AltarButtonUI altar)
    {
        if (_hoveredAltar !=
            altar)
        {
            return;
        }


        _hoveredAltar =
            null;


        HideHoverInfo();
    }


    private void ShowHoverInfo(
        AltarButtonUI altar,
        Vector2 mousePosition)
    {
        if (_selection == null ||
            _hoverInfoPanel == null ||
            altar == null)
        {
            return;
        }


        AltarButtonSlot slot =
            FindSlot(altar);


        if (slot == null)
            return;


        AltarData data =
            FindData(slot.Id);


        if (data == null)
            return;


        if (_hoverTitleText != null)
        {
            _hoverTitleText.text =
                data.DisplayName;
        }


        if (_hoverDescriptionText != null)
        {
            if (_selection.IsUnlocked(
                data.Id))
            {
                _hoverDescriptionText.text =
                    data.Description;
            }
            else
            {
                _hoverDescriptionText.text =
                    data.Description +
                    "\n\n잠금 해제 조건\n" +
                    _selection
                        .GetUnlockCondition(
                            data.Id
                        );
            }
        }


        _hoverInfoPanel
            .gameObject
            .SetActive(true);


        _hoverTitleText
            .ForceMeshUpdate();


        _hoverDescriptionText
            .ForceMeshUpdate();


        Canvas.ForceUpdateCanvases();


        // ContentSizeFitter / Layout 사용 시
        // 실제 패널 크기 즉시 계산
        LayoutRebuilder
            .ForceRebuildLayoutImmediate(
                _hoverInfoPanel
            );


        Canvas.ForceUpdateCanvases();


        UpdateHoverPanelPosition(
            mousePosition
        );
    }


    private void HideHoverInfo()
    {
        if (_hoverInfoPanel != null)
        {
            _hoverInfoPanel
                .gameObject
                .SetActive(false);
        }
    }


    // =====================================================
    // Hover Position
    // =====================================================

    private void UpdateHoverPanelPosition(
        Vector2 mouseScreenPosition)
    {
        if (_hoverInfoPanel == null ||
            _hoverPanelParent == null)
        {
            return;
        }


        // -----------------------------------------
        // 1. 기본 위치
        //
        // 마우스 우상단
        //
        // Pivot = 좌하단
        // Mouse + (30, 30)
        // -----------------------------------------

        _hoverInfoPanel.pivot =
            new Vector2(
                0f,
                0f
            );


        Vector2 defaultScreenPosition =
            mouseScreenPosition +
            new Vector2(
                _mouseOffsetX,
                _mouseOffsetY
            );


        SetPanelScreenPosition(
            defaultScreenPosition
        );


        // -----------------------------------------
        // 2. 실제 패널 오른쪽 끝 확인
        // -----------------------------------------

        Vector3[] corners =
            new Vector3[4];


        _hoverInfoPanel.GetWorldCorners(
            corners
        );


        Camera uiCamera =
            GetUICamera();


        Vector2 upperRightScreen =
            RectTransformUtility
                .WorldToScreenPoint(
                    uiCamera,
                    corners[2]
                );


        // -----------------------------------------
        // 3. 오른쪽 화면 밖으로 나가면
        //
        // 마우스 좌상단으로 뒤집기
        // -----------------------------------------

        if (upperRightScreen.x >
            Screen.width)
        {
            _hoverInfoPanel.pivot =
                new Vector2(
                    1f,
                    0f
                );


            Vector2 flippedScreenPosition =
                mouseScreenPosition +
                new Vector2(
                    -_mouseOffsetX,
                    _mouseOffsetY
                );


            SetPanelScreenPosition(
                flippedScreenPosition
            );
        }
    }


    private void SetPanelScreenPosition(
        Vector2 screenPosition)
    {
        Camera uiCamera =
            GetUICamera();


        if (
            RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    _hoverPanelParent,
                    screenPosition,
                    uiCamera,
                    out Vector2 localPoint
                )
        )
        {
            _hoverInfoPanel
                .anchoredPosition =
                localPoint;
        }
    }


    private Camera GetUICamera()
    {
        if (_rootCanvas == null)
            return null;


        if (
            _rootCanvas.renderMode ==
            RenderMode.ScreenSpaceOverlay
        )
        {
            return null;
        }


        return
            _rootCanvas.worldCamera;
    }


    public void Shutdown()
    {
        if (_slots != null)
        {
            for (int i = 0;
                i < _slots.Length;
                i++)
            {
                if (_slots[i] == null ||
                    _slots[i].Button == null)
                {
                    continue;
                }


                _slots[i].Button.PointerEntered -=
                    OnPointerEntered;

                _slots[i].Button.PointerMoved -=
                    OnPointerMoved;

                _slots[i].Button.PointerExited -=
                    OnPointerExited;

                _slots[i].Button.Clicked -=
                    OnAltarClicked;
            }
        }


        if (_traitButton != null)
        {
            _traitButton.onClick
                .RemoveListener(
                    OnTraitClicked
                );
        }


        if (_nextButton != null)
        {
            _nextButton.onClick
                .RemoveListener(
                    OnNextClicked
                );
        }


        _selection =
            null;


        _hoveredAltar =
            null;


        if (_hoverInfoPanel != null)
        {
            _hoverInfoPanel
                .gameObject
                .SetActive(false);
        }
    }


    private AltarData FindData(
        AltarId id)
    {
        if (_selection == null)
            return null;


        for (int i = 0;
            i < _selection.Data.Count;
            i++)
        {
            if (_selection.Data[i].Id ==
                id)
            {
                return
                    _selection.Data[i];
            }
        }


        return null;
    }


    private AltarButtonSlot FindSlot(
        AltarButtonUI button)
    {
        if (_slots == null ||
            button == null)
        {
            return null;
        }


        for (int i = 0;
            i < _slots.Length;
            i++)
        {
            if (_slots[i] != null &&
                _slots[i].Button ==
                button)
            {
                return
                    _slots[i];
            }
        }


        return null;
    }


    /// <summary> 특정 제단이 클릭되었을 때 해당 제단 ID를 Selected 이벤트로 발행 </summary>
    private void OnAltarClicked(
        AltarButtonUI button)
    {
        AltarButtonSlot slot =
            FindSlot(button);


        if (slot == null)
            return;


        Selected?.Invoke(
            slot.Id
        );
    }


    private void OnTraitClicked()
    {
        TraitRequested?.Invoke();
    }


    private void OnNextClicked()
    {
        NextRequested?.Invoke();
    }


    private void OnDestroy()
    {
        Shutdown();
    }
}