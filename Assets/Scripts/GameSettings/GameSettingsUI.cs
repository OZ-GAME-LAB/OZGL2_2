using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 설정 패널의 화면 설정과 음량, 음소거 입력을
/// GameSettingsManager에 전달한다.
/// <br/>음량은 실시간으로 적용하고,
/// 화면 설정과 파일 저장은 Confirm에서 처리한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameSettingsUI : MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler,
    IInitializePotentialDragHandler
{
    // ============================================================
    // References / Runtime State
    // ============================================================

    [SerializeField] private TMP_Dropdown _resolutionDropdown;

    [SerializeField] private Image _bgmFillImage;
    [SerializeField] private Image _sfxFillImage;

    [SerializeField] private Toggle _bgmMuteToggle;
    [SerializeField] private Toggle _sfxMuteToggle;

    [SerializeField] private Button _confirmButton;

    private GameSettingsManager _settingsManager;
    private Coroutine _initializeRoutine;

    private bool _isReady;

    private Image _dragFillImage;
    private int _dragPointerId;

    public string LastError { get; private set; }

    public event Action Confirmed;
    public event Action<string> SaveFailed;

    // ============================================================
    // Initialize / Shutdown
    // ============================================================

    private void OnEnable()
    {
        if (!ValidateReferences())
            return;

        _initializeRoutine =
            StartCoroutine(InitializeWhenReady());
    }

    /// <summary>
    /// 패널이 Bootstrap보다 먼저 켜져도
    /// 저장값 복원이 끝난 뒤 UI를 채운다.
    /// </summary>
    private IEnumerator InitializeWhenReady()
    {
        while (_settingsManager == null ||
               !_settingsManager.IsInitialized)
        {
            if (_settingsManager == null)
            {
                _settingsManager =
                    FindFirstObjectByType<GameSettingsManager>();
            }

            yield return null;
        }

        RefreshFromSettings();

        _bgmMuteToggle.onValueChanged.AddListener(
            OnBGMMuteChanged);

        _sfxMuteToggle.onValueChanged.AddListener(
            OnSFXMuteChanged);

        _confirmButton.onClick.AddListener(
            Confirm);

        _isReady = true;
    }

    private void OnDisable()
    {
        if (_initializeRoutine != null)
            StopCoroutine(_initializeRoutine);

        _initializeRoutine = null;

        _isReady = false;
        _dragFillImage = null;

        if (_bgmMuteToggle != null)
        {
            _bgmMuteToggle.onValueChanged.RemoveListener(
                OnBGMMuteChanged);
        }

        if (_sfxMuteToggle != null)
        {
            _sfxMuteToggle.onValueChanged.RemoveListener(
                OnSFXMuteChanged);
        }

        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveListener(
                Confirm);
        }
    }

    // ============================================================
    // Refresh
    // ============================================================

    /// <summary>
    /// 다시 열었을 때 현재 화면 설정과 음량,
    /// 음소거 상태를 표시한다.
    /// 자동 저장이나 취소는 하지 않는다.
    /// </summary>
    private void RefreshFromSettings()
    {
        RefreshResolutionDropdown();

        // 음소거 중에도 저장된 조절값은 Fill에 남겨
        // 해제 후 사용할 음량을 보여준다.
        _bgmFillImage.fillAmount =
            _settingsManager.BGMVolume;

        _sfxFillImage.fillAmount =
            _settingsManager.SFXVolume;

        _bgmMuteToggle.SetIsOnWithoutNotify(
            _settingsManager.BGMMuted);

        _sfxMuteToggle.SetIsOnWithoutNotify(
            _settingsManager.SFXMuted);

        LastError = null;
    }

    /// <summary>
    /// 0번에는 전체 화면을 표시하고,
    /// 이후 항목에는 창 모드 해상도를 표시한다.
    /// </summary>
    private void RefreshResolutionDropdown()
    {
        var options = new List<string>
        {
            "[전체 화면]"
        };

        foreach (var resolution in
                 _settingsManager.AvailableResolutions)
        {
            string optionText = "[창 모드] " + resolution.ToString();

            options.Add(optionText);
        }

        _resolutionDropdown.ClearOptions();
        _resolutionDropdown.AddOptions(options);

        if (_settingsManager.Fullscreen)
        {
            _resolutionDropdown.SetValueWithoutNotify(0);
        }
        else
        {
            int resolutionIndex =
                _settingsManager.SelectedResolutionIndex;

            // Dropdown의 0번은 전체 화면이므로
            // 실제 해상도 인덱스보다 1 크게 표시한다.
            int dropdownIndex =
                resolutionIndex >= 0
                    ? resolutionIndex + 1
                    : 0;

            _resolutionDropdown.SetValueWithoutNotify(
                dropdownIndex);
        }

        _resolutionDropdown.RefreshShownValue();
    }

    // ============================================================
    // Fill Image Input
    // ============================================================

    /// <summary>
    /// Fill 영역을 누른 위치를 음량으로 적용하고
    /// 해당 바의 드래그를 시작한다.
    /// </summary>
    public void OnPointerDown(
        PointerEventData eventData)
    {
        if (!_isReady ||
            eventData.button !=
            PointerEventData.InputButton.Left ||
            _dragFillImage != null)
        {
            return;
        }

        if (RectTransformUtility.RectangleContainsScreenPoint(
                _bgmFillImage.rectTransform,
                eventData.position,
                eventData.pressEventCamera))
        {
            _dragFillImage = _bgmFillImage;
        }
        else if (
            RectTransformUtility.RectangleContainsScreenPoint(
                _sfxFillImage.rectTransform,
                eventData.position,
                eventData.pressEventCamera))
        {
            _dragFillImage = _sfxFillImage;
        }
        else
        {
            return;
        }

        _dragPointerId = eventData.pointerId;

        ApplyPointerVolume(eventData);
    }

    public void OnInitializePotentialDrag(
        PointerEventData eventData)
    {
        eventData.useDragThreshold = false;
    }

    /// <summary>
    /// 바 밖으로 드래그해도 처음 누른 바의 음량을
    /// 0~1 범위에서 조절한다.
    /// </summary>
    public void OnDrag(
        PointerEventData eventData)
    {
        if (_isReady &&
            _dragFillImage != null &&
            eventData.pointerId == _dragPointerId)
        {
            ApplyPointerVolume(eventData);
        }
    }

    public void OnPointerUp(
        PointerEventData eventData)
    {
        if (eventData.pointerId == _dragPointerId)
            _dragFillImage = null;
    }

    /// <summary>
    /// 화면 좌표를 Fill의 전체 Rect 기준으로 환산한다.
    /// 채워진 부분의 길이는 기준으로 쓰지 않는다.
    /// </summary>
    private void ApplyPointerVolume(
        PointerEventData eventData)
    {
        RectTransform rectTransform =
            _dragFillImage.rectTransform;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        Rect rect = rectTransform.rect;

        if (rect.width <= 0f)
            return;

        float volume =
            Mathf.Clamp01(
                (localPoint.x - rect.xMin) /
                rect.width);

        if (_dragFillImage.fillOrigin ==
            (int)Image.OriginHorizontal.Right)
        {
            volume = 1f - volume;
        }

        _dragFillImage.fillAmount = volume;

        if (_dragFillImage == _bgmFillImage)
        {
            _settingsManager.SetBGMVolume(volume);
        }
        else
        {
            _settingsManager.SetSFXVolume(volume);
        }
    }

    // ============================================================
    // Mute
    // ============================================================

    private void OnBGMMuteChanged(bool isOn)
    {
        if (_isReady)
            _settingsManager.SetBGMMuted(isOn);
    }

    private void OnSFXMuteChanged(bool isOn)
    {
        if (_isReady)
            _settingsManager.SetSFXMuted(isOn);
    }

    // ============================================================
    // Confirm
    // ============================================================

    /// <summary>
    /// 선택한 화면 설정과 현재 음량,
    /// 음소거 상태를 적용하고 파일에 저장한다.
    /// </summary>
    public void Confirm()
    {
        if (!_isReady ||
            _settingsManager == null)
        {
            return;
        }

        // 0번은 현재 디스플레이에 적합한
        // 16:9 전체 화면을 의미한다.
        if (_resolutionDropdown.value == 0)
        {
            _settingsManager.SetFullscreen();
        }
        else
        {
            // Dropdown의 0번이 전체 화면이므로
            // 실제 해상도 인덱스는 1을 뺀다.
            int resolutionIndex =
                _resolutionDropdown.value - 1;

            _settingsManager.SetWindowedResolutionIndex(
                resolutionIndex);
        }

        _settingsManager.SetBGMMuted(
            _bgmMuteToggle.isOn);

        _settingsManager.SetSFXMuted(
            _sfxMuteToggle.isOn);

        _settingsManager.SetBGMVolume(
            _bgmFillImage.fillAmount);

        _settingsManager.SetSFXVolume(
            _sfxFillImage.fillAmount);

        if (!_settingsManager.TrySave(
                out string error))
        {
            LastError = error;

            Debug.LogError(
                "[GameSettingsUI] 설정 저장 실패: " +
                error,
                this);

            SaveFailed?.Invoke(error);

            return;
        }

        LastError = null;

        Confirmed?.Invoke();

        gameObject.SetActive(false);
    }

    // ============================================================
    // Validation
    // ============================================================

    private bool ValidateReferences()
    {
        if (_resolutionDropdown != null &&
            _bgmFillImage != null &&
            _sfxFillImage != null &&
            _bgmMuteToggle != null &&
            _sfxMuteToggle != null &&
            _confirmButton != null &&
            _bgmFillImage.type == Image.Type.Filled &&
            _sfxFillImage.type == Image.Type.Filled &&
            _bgmFillImage.fillMethod ==
            Image.FillMethod.Horizontal &&
            _sfxFillImage.fillMethod ==
            Image.FillMethod.Horizontal)
        {
            return true;
        }

        Debug.LogError(
            "[GameSettingsUI] Dropdown, 수평 Filled Image 2개, " +
            "음소거 Toggle 2개, Confirm Button을 연결해주세요.",
            this);

        return false;
    }
}