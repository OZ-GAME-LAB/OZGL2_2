using System.Collections.Generic;
using Game.UI.InGame;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ConsumableItemView : MonoBehaviour
{
    [SerializeField] private UIItemSlot _slotPrefab; // 풀링용 오브젝트
    [SerializeField] private ItemAreaIndicator _areaIndicatorPrefab;
    private ItemAreaIndicator _areaIndicator;
    private int _aimSlotIndex = -1;
    private ConsumableItemData _aimItem;
    private int _aimStartedFrame;
    private readonly List<RaycastResult> _areaUIHits = new List<RaycastResult>();

    private readonly List<UIItemSlot> _slots = new List<UIItemSlot>();
    private Transform _slotRoot;
    private ConsumableItemActionButton _actionButton;
    private IConsumableItemInventory _inventory;
    private IConsumableItemReader _reader;
    private IConsumableItemUser _user;
    private bool _subscribed;
    private int _selectedSlotIndex = -1;
    private ConsumableItemData _selectedItem;

    public bool IsReady { get; private set; }

    public void Initialize(
        IConsumableItemInventory inventory,
        IConsumableItemReader reader,
        IConsumableItemUser user,
        ConsumableItemActionButton actionButton)
    {
        CancelAreaSelection();
        Unsubscribe();
        ClearSelection();
        IsReady = false;

        _inventory = inventory;
        _reader = reader;
        _user = user;
        _actionButton = actionButton;
        _slotRoot = transform;

        if (_inventory == null || _reader == null || !_reader.IsInitialized || _user == null ||
            _slotPrefab == null || _actionButton == null || !_actionButton.IsReady)
        {
            Debug.LogError("[ConsumableItemView] 아이템 시스템과 슬롯·팝업 참조를 먼저 연결해주세요.", this);
            return;
        }

        IsReady = true;
        if (isActiveAndEnabled) Subscribe();
        // 매니저의 초기 변경 알림은 UI를 연결하기 전에 발생할 수 있다.
        RefreshSlots();
    }

    private void OnEnable()
    {
        if (!IsReady) return;
        Subscribe();
        RefreshSlots();
    }

    private void OnDisable()
    {
        CancelAreaSelection();
        Unsubscribe();
        ClearSelection();
    }

    private void OnDestroy()
    {
        if (_areaIndicator != null) Destroy(_areaIndicator.gameObject);
        Unsubscribe();
        ClearSelection();
    }

    private void Subscribe()
    {
        if (_subscribed) return;
        _reader.InventoryChanged += HandleInventoryChange;
        _actionButton.UseRequested += HandleUseRequested;
        _actionButton.RemoveRequested += HandleRemoveRequested;
        _actionButton.Screen.Closed += HandlePopupClosed;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        if (_reader != null) _reader.InventoryChanged -= HandleInventoryChange;
        if (_actionButton != null)
        {
            _actionButton.UseRequested -= HandleUseRequested;
            _actionButton.RemoveRequested -= HandleRemoveRequested;
            if (_actionButton.Screen != null) _actionButton.Screen.Closed -= HandlePopupClosed;
        }
        _subscribed = false;
    }

    /// <summary>필요한 슬롯만 추가하고, 줄어든 슬롯은 비활성 상태로 보관한다.</summary>
    private void EnsureSlotCount(int requiredCount)
    {
        while (_slots.Count < requiredCount)
        {
            UIItemSlot slot = Instantiate(_slotPrefab, _slotRoot, false);
            slot.gameObject.SetActive(false);
            _slots.Add(slot);
        }
    }

    private void HandleSlotClicked(int slotIndex)
    {
        if (!IsReady || !isActiveAndEnabled || slotIndex < 0 || slotIndex >= _slots.Count ||
            !_reader.TryGetItem(slotIndex, out ConsumableItemData item)) return;

        if (_actionButton.Open(_slots[slotIndex].transform as RectTransform))
        {
            CancelAreaSelection();
            _selectedSlotIndex = slotIndex;
            _selectedItem = item;
        }
    }

    private bool TryGetSelection(out int slotIndex)
    {
        slotIndex = _selectedSlotIndex;
        return IsReady && isActiveAndEnabled && slotIndex >= 0 && _actionButton.IsOpen &&
            _reader.TryGetItem(slotIndex, out ConsumableItemData item) && item == _selectedItem;
    }

    private void HandleUseRequested()
    {
        if (TryGetSelection(out int slotIndex)) UseSlot(slotIndex);
    }

    // 클릭과 단축키 모두 같은 검증과 사용 흐름을 거칩니다.
    private void UseSlot(int slotIndex)
    {
        if (!IsReady || !isActiveAndEnabled || !_user.CanUse ||
            slotIndex < 0 || slotIndex >= _reader.SlotCount ||
            !_reader.TryGetItem(slotIndex, out ConsumableItemData item) || item == null) return;
        CancelAreaSelection();
        // 사용 중 재고 알림이 즉시 발생하므로 선택을 먼저 정리한다.
        ClearSelection(UICloseReason.Completed);
        switch (item.TargetMode)
        {
            case ConsumableTargetMode.All:
                _user.TryUse(slotIndex);
                break;
            case ConsumableTargetMode.Area:
                BeginAreaSelection(slotIndex, item);
                break;
        }
    }

    private void BeginAreaSelection(int slotIndex, ConsumableItemData item)
    {
        if (_areaIndicatorPrefab == null)
        {
            Debug.LogError("[ConsumableItemView] Area Indicator Prefab을 연결해주세요.", this);
            return;
        }
        // 현재 인게임 씬에 Canvas와 독립된 월드 오브젝트로 생성하고, OnDestroy에서 정리합니다.
        if (_areaIndicator == null)
            _areaIndicator = Instantiate(_areaIndicatorPrefab);
        _areaIndicator.Hide();
        _aimSlotIndex = slotIndex;
        _aimItem = item;
        _aimStartedFrame = Time.frameCount;
    }

    private void CancelAreaSelection()
    {
        _aimSlotIndex = -1;
        _aimItem = null;
        if (_areaIndicator != null) _areaIndicator.Hide();
    }

    private void Update()
    {
        HandleSlotHotkeys();
        if (_aimSlotIndex < 0) return;
        if (!_user.CanUse || _areaIndicator == null ||
            !_reader.TryGetItem(_aimSlotIndex, out var current) || current != _aimItem ||
            (_actionButton.Screen.Manager != null && _actionButton.Screen.Manager.HasBlockingPopup))
        {
            CancelAreaSelection();
            return;
        }
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        bool cancel = (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
            (mouse != null && mouse.rightButton.wasPressedThisFrame);
        if (cancel) { CancelAreaSelection(); return; }
        if (mouse == null) { _areaIndicator.Hide(); return; }
        Vector2 pointer = mouse.position.ReadValue();
        bool click = mouse.leftButton.wasPressedThisFrame;
        Camera camera = Camera.main;
        if (camera == null || !camera.pixelRect.Contains(pointer) || IsPointerOverAreaUI(pointer))
        {
            _areaIndicator.Hide();
            return;
        }
        Ray ray = camera.ScreenPointToRay(pointer);
        if (!new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float distance))
        {
            _areaIndicator.Hide();
            return;
        }
        Vector2 worldPosition = ray.GetPoint(distance);
        _areaIndicator.Show(worldPosition, _aimItem.Radius);
        // 팝업의 사용 버튼 클릭이 위치 확정 클릭으로 이어지지 않게 합니다.
        if (!click || Time.frameCount <= _aimStartedFrame) return;
        try
        {
            if (_user.TryUse(_aimSlotIndex, worldPosition)) CancelAreaSelection();
            // 대상이 없으면 아이템을 유지하고 다른 위치를 선택할 수 있습니다.
        }
        catch
        {
            CancelAreaSelection();
            throw;
        }
    }

    private void HandleSlotHotkeys()
    {
        if (!IsReady || !_user.CanUse) return;
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        var manager = _actionButton.Screen.Manager;
        if (manager != null && manager.HasBlockingPopup && !_actionButton.IsOpen) return;
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected != null && (selected.GetComponentInParent<TMP_InputField>() != null ||
            selected.GetComponentInParent<InputField>() != null)) return;

        // 여러 키가 동시에 눌려도 한 프레임에 한 슬롯만 처리합니다.
        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            UseSlot(0);
        else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            UseSlot(1);
        else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            UseSlot(2);
    }

    private bool IsPointerOverAreaUI(Vector2 pointer)
    {
        if (EventSystem.current == null) return false;
        _areaUIHits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pointer }, _areaUIHits);
        foreach (var hit in _areaUIHits)
            if (hit.module is GraphicRaycaster) return true;
        return false;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) CancelAreaSelection();
    }

    private void HandleRemoveRequested()
    {
        if (!TryGetSelection(out int slotIndex)) return;
        ClearSelection(UICloseReason.Completed);
        _inventory.TryRemove(slotIndex);
    }

    private void HandlePopupClosed(UIScreen screen, UICloseReason reason)
    {
        _selectedSlotIndex = -1;
        _selectedItem = null;
    }

    private void ClearSelection(UICloseReason reason = UICloseReason.ContextLost)
    {
        bool hadSelection = _selectedSlotIndex >= 0;
        _selectedSlotIndex = -1;
        _selectedItem = null;
        if (hadSelection && _actionButton != null) _actionButton.Close(reason);
    }

    private void HandleInventoryChange()
    {
        CancelAreaSelection();
        // 슬롯이 이동하거나 비워지면 열린 팝업의 이전 선택은 유효하지 않다.
        ClearSelection();
        RefreshSlots();
    }

    private void RefreshSlots()
    {
        if (!IsReady || !_reader.IsInitialized) return;
        int slotCount = _reader.SlotCount;
        EnsureSlotCount(slotCount);

        for (int i = 0; i < _slots.Count; i++)
        {
            UIItemSlot slot = _slots[i];
            if (i >= slotCount)
            {
                slot.Unbind();
                slot.gameObject.SetActive(false);
                continue;
            }

            if (_reader.Slots[i].IsEmpty)
            {
                slot.EmptySlot();
            }
            else
            {
                ConsumableItemData data = _reader.Slots[i].Item;
                int slotIndex = i;
                slot.Bind(data.Icon, data.DisplayName, "1", false, true,
                    () => HandleSlotClicked(slotIndex));
            }
            slot.gameObject.SetActive(true);
        }
    }
}
