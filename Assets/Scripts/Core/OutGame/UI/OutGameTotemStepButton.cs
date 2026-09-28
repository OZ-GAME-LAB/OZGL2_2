using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>토템의 좌클릭(+1), 우클릭(-1)을 전달합니다. Button.onClick에는 연결하지 않습니다.</summary>
public class OutGameTotemStepButton : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _background;
    [SerializeField] private TMP_Text _label;

    private bool _initialized;
    public event Action<int> Clicked;

    public bool ValidateReferences()
    {
        if (_button != null && _background != null && _label != null) return true;
        Debug.LogError("[OutGameTotemStepButton] 버튼, 배경, 라벨을 연결해주세요.", this);
        return false;
    }

    public void Initialize()
    {
        _initialized = ValidateReferences();
    }

    public void SetDisplay(string text, bool selected)
    {
        _label.text = text;
        _background.color = selected ? new Color(0.96f, 0.23f, 0.12f) : Color.white;
        _label.color = selected ? Color.white : new Color(0.15f, 0.15f, 0.15f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_initialized || !_button.IsActive() || !_button.IsInteractable()) return;
        if (eventData.button == PointerEventData.InputButton.Left) Clicked?.Invoke(1);
        else if (eventData.button == PointerEventData.InputButton.Right) Clicked?.Invoke(-1);
    }

    public void Shutdown()
    {
        _initialized = false;
    }
}
