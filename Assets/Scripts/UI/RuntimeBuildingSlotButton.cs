using Game.Core;
using OZGL.KDH;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>UI 지도 버튼은 슬롯의 실제 점유 상태만 표시한다.</summary>
    public sealed class RuntimeBuildingSlotButton : MonoBehaviour
    {
        [SerializeField] private BuildingSlot _slot;
        [SerializeField] private RuntimeBuildingUiBinding _binding;
        [SerializeField] private GameFlowController _flow;
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private GameObject _emptyIcon;
        [SerializeField] private bool _selectFirstEmptySlot;
        private Building _shown;

        private void OnEnable() => _button.onClick.AddListener(HandleClick);
        private void OnDisable() => _button.onClick.RemoveListener(HandleClick);
        private void LateUpdate()
        {
            bool available = (_selectFirstEmptySlot || (_slot != null && _slot.isActiveAndEnabled)) &&
                _flow != null && _flow.CanEnterBuildMode();
            if (_button.interactable != available) _button.interactable = available;
            var current = _slot != null && _slot.IsOccupied ? _slot.CurrentBuilding : null;
            // Unity의 파괴된 오브젝트 == null 비교로 빈 슬롯 표시 갱신이 생략되지 않게 한다.
            if (ReferenceEquals(_shown, current)) return;
            _shown = current;
            _label.text = current != null && current.Data != null ? current.Data.DisplayName : "건설";
            _emptyIcon.SetActive(current == null);
        }
        private void HandleClick()
        {
            if (!_button.IsInteractable()) return;
            if (_selectFirstEmptySlot) _binding.SelectFirstEmptySlot();
            else _binding.SelectSlot(_slot);
        }
    }
}
