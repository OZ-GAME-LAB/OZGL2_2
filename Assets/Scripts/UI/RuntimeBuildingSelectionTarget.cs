using System;
using Game.Core;
using OZGL.KDH;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>2D 월드 클릭을 기존 건물 UI로 전달한다. 건설/재화/해금 상태는 변경하지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class RuntimeBuildingSelectionTarget : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IDragHandler
    {
        [SerializeField] private RuntimeBuildingUiBinding _binding;
        [SerializeField] private BuildingSlot _slot;
        [SerializeField] private GameFlowController _flow;

        private Collider2D _collider;
        private GameFlowController _subscribedFlow;
        private Building _pressedBuilding;
        private Vector2 _pressedPosition;
        private int _pressedPointerId;
        private bool _hasPress;

        private void OnEnable()
        {
            _collider = _slot != null ? _slot.GetComponent<Collider2D>() : null;
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
            ResetPress();
        }

        public void Initialize(RuntimeBuildingUiBinding binding, BuildingSlot slot, GameFlowController flow)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            if (slot == null) throw new ArgumentNullException(nameof(slot));
            if (flow == null) throw new ArgumentNullException(nameof(flow));
            Unbind();
            ResetPress();
            _binding = binding;
            _slot = slot;
            _flow = flow;
            _collider = slot.GetComponent<Collider2D>();
            if (isActiveAndEnabled) Bind();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
            ResetPress();
            if (!CanHandle(eventData)) return;
            _hasPress = true;
            _pressedPosition = eventData.position;
            _pressedPointerId = eventData.pointerId;
            _pressedBuilding = _slot.IsOccupied ? _slot.CurrentBuilding : null;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_hasPress || eventData == null || eventData.pointerId != _pressedPointerId) return;
            bool valid = CanHandle(eventData);
            float movedSquared = (eventData.position - _pressedPosition).sqrMagnitude;
            float threshold = EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 0;
            var pressedBuilding = _pressedBuilding;
            ResetPress();
            if (!valid || eventData.dragging || (movedSquared > 0 &&
                (!eventData.useDragThreshold || movedSquared >= threshold * threshold))) return;
            // 누르는 동안 다른 건물로 교체/해체됐다면 새 클릭을 요구한다.
            var current = _slot.IsOccupied ? _slot.CurrentBuilding : null;
            if (!ReferenceEquals(pressedBuilding, current)) return;
            _binding.SelectSlot(_slot);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData != null && eventData.pointerId == _pressedPointerId) ResetPress();
        }

        private void HandlePhaseChanged(GamePhase phase) => ResetPress();

        private bool CanHandle(PointerEventData eventData) =>
            isActiveAndEnabled && eventData != null && eventData.button == PointerEventData.InputButton.Left &&
            _binding != null && _binding.isActiveAndEnabled && _slot != null && _slot.isActiveAndEnabled &&
            _collider != null && _collider.enabled && _flow != null && _flow.isActiveAndEnabled && _flow.CanEnterBuildMode() &&
            ExecuteEvents.GetEventHandler<IPointerClickHandler>(eventData.pointerCurrentRaycast.gameObject) == gameObject;

        private void Bind()
        {
            if (_flow == null || _subscribedFlow != null) return;
            _subscribedFlow = _flow;
            _subscribedFlow.PhaseChanged += HandlePhaseChanged;
        }

        private void Unbind()
        {
            if (_subscribedFlow != null) _subscribedFlow.PhaseChanged -= HandlePhaseChanged;
            _subscribedFlow = null;
        }

        private void ResetPress()
        {
            _hasPress = false;
            _pressedBuilding = null;
        }
    }
}
