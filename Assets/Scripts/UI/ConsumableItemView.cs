using System.Collections.Generic;
using Game.UI.InGame;
using UnityEngine;

public class ConsumableItemView : MonoBehaviour
{
    [SerializeField] private UIItemSlot _slotPrefab; // 풀링용 오브젝트

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
        Unsubscribe();
        ClearSelection();
    }

    private void OnDestroy()
    {
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
        if (!TryGetSelection(out int slotIndex)) return;
        // 사용 중 재고 알림이 즉시 발생하므로 선택을 먼저 정리한다.
        ClearSelection(UICloseReason.Completed);
        // 범위 아이템은 별도 위치 선택 후 TryUse(slotIndex, worldPosition)가 필요하다.
        _user.TryUse(slotIndex);
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
