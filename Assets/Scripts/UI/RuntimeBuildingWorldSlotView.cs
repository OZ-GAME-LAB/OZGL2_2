using OZGL.KDH;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>실제 슬롯의 점유 상태를 월드 라벨에 표시한다. 슬롯 상태는 소유하지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class RuntimeBuildingWorldSlotView : MonoBehaviour
    {
        [SerializeField] private BuildingSlot _slot;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private SpriteRenderer _marker;
        [SerializeField] private Color _emptyColor = new Color32(51, 79, 72, 255);
        [SerializeField] private Color _occupiedColor = new Color32(103, 84, 49, 255);

        private Building _shown;
        private bool _hasRendered;

        private void OnEnable() => _hasRendered = false;

        private void LateUpdate()
        {
            var current = _slot != null && _slot.IsOccupied ? _slot.CurrentBuilding : null;
            if (_hasRendered && ReferenceEquals(_shown, current)) return;
            _shown = current;
            _hasRendered = true;
            if (_label != null) _label.text = current != null && current.Data != null ? current.Data.DisplayName : "+ 건설";
            if (_marker != null) _marker.color = current != null ? _occupiedColor : _emptyColor;
        }
    }
}
