using System;
using System.Collections.Generic;
using UnityEngine;

// 소모성 아이템 획득과 슬롯 관리. 실제 사용 효과는 추후 연결
public class ConsumableItemManager : MonoBehaviour, IConsumableItemReader, IConsumableItemInventory
{
    public bool IsInitialized => _inventory != null;
    public ConsumableItemCatalog Catalog => _itemCatalog;
    public IReadOnlyList<IConsumableItemSlotReader> Slots => _inventory != null ? _inventory.Slots : null;
    public int SlotCount => _inventory != null ? _inventory.SlotCount : 0;
    public int Capacity => _inventory != null ? _inventory.Capacity : 0;
    public bool HasEmptySlot => _inventory != null && _inventory.HasEmptySlot;
    public int ItemCount => _inventory != null ? _inventory.ItemCount : 0;
    public int OverflowCount => _inventory != null ? _inventory.OverflowCount : 0;
    public bool HasOverflow => OverflowCount > 0;

    public event Action InventoryChanged;

    [SerializeField] private ConsumableItemCatalog _itemCatalog;

    private ConsumableItemInventory _inventory;
    private EffectManager _effectManager;

    // Catalog 확인 후 공통 효과 매니저를 연결하고 현재 슬롯 효과 반영
    public void Initialize(EffectManager effectManager)
    {
        if (IsInitialized)
        {
            Debug.LogWarning("[Consumables/ConsumableItemManager] 이미 초기화되어 있습니다.", this);
            return;
        }

        if (_itemCatalog == null)
        {
            Debug.LogError("[Consumables/ConsumableItemManager] Catalog를 Inspector에서 연결해주세요.", this);
            return;
        }

        if (effectManager == null)
        {
            Debug.LogError("[Consumables/ConsumableItemManager] EffectManager 참조가 없습니다.", this);
            return;
        }

        _effectManager = effectManager;
        _effectManager.EffectsChanged += HandleEffectsChanged;
        CreateInventory();
    }

    public bool TryGetItem(int slotIndex, out ConsumableItemData item)
    {
        item = null;
        return IsInitialized && _inventory.TryGetItem(slotIndex, out item);
    }

    // Catalog에 등록된 아이템만 획득 가능
    public bool TryAdd(ConsumableItemData item)
    {
        if (!IsInitialized || _itemCatalog == null || !_itemCatalog.Contains(item))
        {
            return false;
        }

        return _inventory.TryAdd(item);
    }

    // 지정 슬롯만 비우기. 사용 효과를 실행하는 함수는 아님
    public bool TryRemove(int slotIndex)
    {
        return IsInitialized && _inventory.TryRemove(slotIndex);
    }

    // 기본 슬롯과 추가 슬롯을 합한 최종 개수 전달
    public bool TrySetCapacity(int capacity)
    {
        return IsInitialized && _inventory.TrySetCapacity(capacity);
    }

    // 슬롯 개수는 유지하고 보유 아이템만 제거
    public void Clear()
    {
        if (!IsInitialized)
        {
            return;
        }

        _inventory.Clear();
    }

    // 보유 아이템을 비우고 기본 3칸에 현재 등록된 효과를 다시 반영
    // 이전 Run의 효과 제거는 각 효과 출처에서 처리
    public void ResetRun()
    {
        if (!IsInitialized)
        {
            return;
        }

        CreateInventory();
    }

    private void CreateInventory()
    {

        if (_inventory != null)
        {
            _inventory.InventoryChanged -= HandleInventoryChanged;
        }

        _inventory = new ConsumableItemInventory();
        // 구독 전에 슬롯 수를 반영하여 초기화 완료 상태만 한 번 알림
        HandleEffectsChanged();
        _inventory.InventoryChanged += HandleInventoryChanged;
        HandleInventoryChanged();
    }

    private void HandleEffectsChanged()
    {
        if (!IsInitialized || _effectManager == null)
        {
            return;
        }

        long capacity = (long)ConsumableItemInventory.DefaultSlotCount
            + _effectManager.ConsumableSlotAdjustment;
        int finalCapacity = (int)Math.Max(0L, Math.Min(int.MaxValue, capacity));
        TrySetCapacity(finalCapacity);
    }

    private void HandleInventoryChanged()
    {
        InventoryChanged?.Invoke();
    }

    private void OnDestroy()
    {

        if (_effectManager != null)
        {
            _effectManager.EffectsChanged -= HandleEffectsChanged;
        }
        if (_inventory != null)
        {
            _inventory.InventoryChanged -= HandleInventoryChanged;
        }
    }

}
