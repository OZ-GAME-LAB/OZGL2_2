using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>EventSystem의 2D 월드 클릭을 유닛 정보 선택으로 전달한다. 별도 입력 폴링은 하지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class RuntimeUnitSelectionTarget : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IDragHandler
    {
        [SerializeField] private RuntimeUnitInfoBinding _binding;
        [SerializeField] private RuntimeUnitInfoSource _source;
        [SerializeField] private bool _clearsSelection;

        private string _pressedSelectionId;
        private Vector2 _pressedPosition;
        private int _pressedPointerId;
        private bool _hasPress;

        private void OnDisable()
        {
            ResetPress();
        }

        public void Initialize(RuntimeUnitInfoBinding binding, RuntimeUnitInfoSource source)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            if (source == null) throw new ArgumentNullException(nameof(source));
            ResetPress();
            _binding = binding;
            _source = source;
            _clearsSelection = false;
        }

        public void InitializeClearSurface(RuntimeUnitInfoBinding binding)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            ResetPress();
            _binding = binding;
            _source = null;
            _clearsSelection = true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
            ResetPress();
            if (!CanHandle(eventData)) return;
            _hasPress = true;
            _pressedPointerId = eventData.pointerId;
            _pressedPosition = eventData.position;
            _pressedSelectionId = _source != null ? _source.SelectionId : null;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!CanHandle(eventData) || !_hasPress || eventData.pointerId != _pressedPointerId) return;
            string pressedId = _pressedSelectionId;
            float movedSquared = (eventData.position - _pressedPosition).sqrMagnitude;
            float threshold = EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 0;
            ResetPress();
            // 이동과 버튼 해제가 같은 프레임에 처리되면 OnDrag보다 OnPointerClick이 먼저 올 수 있다.
            if (eventData.dragging || (movedSquared > 0 &&
                (!eventData.useDragThreshold || movedSquared >= threshold * threshold))) return;
            if (_clearsSelection)
                _binding.ClearSelection();
            else if (_source != null && pressedId != null && _source.SelectionId == pressedId)
                _binding.TrySelect(_source);
        }

        public void OnDrag(PointerEventData eventData)
        {
            // 핸들러가 있어야 EventSystem이 임계값 이동을 드래그로 추적한다. 이동 명령은 발행하지 않는다.
            if (eventData != null && eventData.pointerId == _pressedPointerId) ResetPress();
        }

        private bool CanHandle(PointerEventData eventData) =>
            isActiveAndEnabled && eventData != null && eventData.button == PointerEventData.InputButton.Left &&
            _binding != null && _binding.isActiveAndEnabled;

        private void ResetPress()
        {
            _hasPress = false;
            _pressedSelectionId = null;
        }
    }
}
