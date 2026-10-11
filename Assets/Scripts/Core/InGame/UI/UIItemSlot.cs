using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.InGame
{
    /// <summary>반복 카드의 표시와 클릭만 담당한다. 가격·보상 규칙은 호출자가 결정한다.</summary>
    public sealed class UIItemSlot : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _value;
        [SerializeField] private GameObject _selection; //클릭시 열리는 팝업창
        private Action _onClick;

        private void OnEnable() { if (_button != null) _button.onClick.AddListener(HandleClick); }
        private void OnDisable() { if (_button != null) _button.onClick.RemoveListener(HandleClick); }
        public void Bind(Sprite icon, string title, string value, bool selected, bool interactable, Action onClick)
        {
            _onClick = onClick;
            if (_icon != null) _icon.sprite = icon;
            if (_title != null) _title.text = title ?? string.Empty;
            if (_value != null) _value.text = value ?? string.Empty;
            if (_selection != null) _selection.SetActive(selected);
            if (_button != null) _button.interactable = interactable;
        }

        public void Unbind()
        {
            _onClick = null;
        }

        /// <summary>비어있는 슬롯 제작
        public void EmptySlot()
        {
            _onClick = null;
            if (_icon != null) _icon.sprite = null;
            if (_title != null) _title.text = string.Empty;
            if (_value != null) _value.text = string.Empty;
            if (_selection != null) _selection.SetActive(false);
            if (_button != null) _button.interactable = false;
        }
        private void HandleClick()
        {
            if (_button != null && _button.IsInteractable()) 
                _onClick?.Invoke();
        }
    }
}
