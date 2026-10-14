using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>제단과 특성 버튼의 공통 표시 및 클릭 전달입니다. 제단 선택이나 토템 선택 버튼등을 표시합니다. </summary>
public class OutGameChoiceButton : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _background;
    [SerializeField] private TMP_Text _label;

    public event Action<OutGameChoiceButton> Clicked;

    public bool ValidateReferences()
    {
        if (_button != null && _background != null && _label != null) return true;
        Debug.LogError("[OutGameChoiceButton] 버튼, 배경, 라벨을 연결해주세요.", this);
        return false;
    }

    public void Initialize()
    {
        Shutdown();
        if (!ValidateReferences()) return;
        _button.onClick.AddListener(OnClicked);
    }

    public void SetDisplay(string text, Color color)
    {
        _label.text = text;
        _background.color = color;
    }

    public void Shutdown()
    {
        if (_button != null) _button.onClick.RemoveListener(OnClicked);
    }

    private void OnClicked()
    {
        Clicked?.Invoke(this);
    }

    private void OnDestroy()
    {
        Shutdown();
    }
}
