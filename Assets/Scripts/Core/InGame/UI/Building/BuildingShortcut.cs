using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.InGame
{
    /// <summary>전달된 슬롯 표시와 클릭만 처리한다. 슬롯 선택은 게임 연결부가 맡는다.</summary>
    public sealed class BuildingShortcut : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private GameObject _emptyIcon;
        [SerializeField] private bool _selectFirstEmptySlot;

        public event Action<BuildingShortcut> Clicked;
        public bool IsReady => _button != null;
        public bool SelectsFirstEmptySlot => _selectFirstEmptySlot;

        private void OnEnable() { if (_button != null) _button.onClick.AddListener(HandleClick); }
        private void OnDisable() { if (_button != null) _button.onClick.RemoveListener(HandleClick); }

        public void Render(bool available, string label, bool empty)
        {
            if (_button != null && _button.interactable != available) _button.interactable = available;
            if (_label != null && _label.text != label) _label.text = label;
            if (_emptyIcon != null && _emptyIcon.activeSelf != empty) _emptyIcon.SetActive(empty);
        }

        private void HandleClick()
        {
            if (_button != null && _button.IsInteractable()) Clicked?.Invoke(this);
        }
    }
}
