using System;
using System.Collections.Generic;
using Game.Core;
using Units;
using Units.Skills;
using UnityEngine;

// 소모성 아이템 획득, 슬롯 관리 및 사용 요청
public class ConsumableItemManager : MonoBehaviour, IConsumableItemReader, IConsumableItemInventory, IConsumableItemUser
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
    public bool IsUsing { get; private set; }

    public event Action InventoryChanged;
    public event Action<ConsumableItemData, ICombatTarget> TargetEffectApplying;
    public event Action<ConsumableItemData, ICombatTarget, IReadOnlyList<CombatApplicationResult>> TargetEffectApplied;

    [SerializeField] private ConsumableItemCatalog _itemCatalog;
    [SerializeField] private ConsumableItemCaster _itemCasterPrefab;
    private ConsumableItemCaster _itemCaster;

    private ConsumableItemInventory _inventory;
    private EffectManager _effectManager;
    private GameFlowController _gameFlow;
    private ConsumableItemTargetSelector _targetSelector;
    private ConsumableItemEffectExecutor _effectExecutor;
    private bool _capacityRefreshPending;

    public bool TryUse(int slotIndex, ICombatTarget selectedTarget = null, Vector2? selectedPosition = null)
    {
        if (IsUsing || !CanUseInBattle() || _targetSelector == null || _effectExecutor == null ||
            !TryGetItem(slotIndex, out ConsumableItemData item) || item.Effects == null || item.Effects.Count == 0)
            return false;

        List<ICombatTarget> targets = _targetSelector.SelectTargets(item, selectedTarget, selectedPosition);
        if (targets.Count == 0) return false;

        bool applied = false;
        IsUsing = true;
        try
        {
            _effectExecutor.Execute(item, targets, CanUseInBattle, out applied);
        }
        finally
        {
            try
            {
                // 전체 대상 처리 후 적용된 효과가 있으면 선택 슬롯을 한 번만 소모합니다.
                if (applied) _inventory.TryRemove(slotIndex);
            }
            finally
            {
                IsUsing = false;
                if (_capacityRefreshPending)
                {
                    _capacityRefreshPending = false;
                    HandleEffectsChanged();
                }
            }
        }
        return applied;
    }

    private bool CanUseInBattle()
    {
        return this != null && isActiveAndEnabled && IsInitialized && _gameFlow != null && _gameFlow.CanBattle();
    }

    // 인벤토리와 전투 중 사용에 필요한 참조를 함께 초기화합니다.
    public void Initialize(EffectManager effectManager, GameFlowController gameFlow,
        RuntimeUnitManager unitManager, SkillEffectResolver resolver)
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

        if (gameFlow == null || unitManager == null || resolver == null)
        {
            Debug.LogError("[Consumables/ConsumableItemManager] GameFlowController·RuntimeUnitManager·SkillEffectResolver 참조를 확인해주세요.", this);
            return;
        }

        if (_itemCasterPrefab == null)
        {
            Debug.LogError("[Consumables/ConsumableItemManager] 아이템 전용 Caster 프리팹을 연결해주세요.", this);
            return;
        }
        _itemCaster = Instantiate(_itemCasterPrefab, transform);
        if (!_itemCaster.IsAlive)
        {
            Debug.LogError("[Consumables/ConsumableItemManager] Caster 초기화에 실패했습니다.", this);
            Destroy(_itemCaster.gameObject);
            return;
        }

        _gameFlow = gameFlow;
        _targetSelector = new ConsumableItemTargetSelector(unitManager);
        _effectExecutor = new ConsumableItemEffectExecutor(resolver, _itemCaster);
        _effectExecutor.TargetEffectApplying += (item, target) => TargetEffectApplying?.Invoke(item, target);
        _effectExecutor.TargetEffectApplied += (item, target, results) => TargetEffectApplied?.Invoke(item, target, results);
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
        if (IsUsing || !IsInitialized || _itemCatalog == null || !_itemCatalog.Contains(item))
        {
            return false;
        }

        return _inventory.TryAdd(item);
    }

    // 지정 슬롯만 비우기. 사용 효과를 실행하는 함수는 아님
    public bool TryRemove(int slotIndex)
    {
        return !IsUsing && IsInitialized && _inventory.TryRemove(slotIndex);
    }

    // 기본 슬롯과 추가 슬롯을 합한 최종 개수 전달
    public bool TrySetCapacity(int capacity)
    {
        return !IsUsing && IsInitialized && _inventory.TrySetCapacity(capacity);
    }

    // 슬롯 개수는 유지하고 보유 아이템만 제거
    public void Clear()
    {
        if (IsUsing || !IsInitialized)
        {
            return;
        }

        _inventory.Clear();
    }

    // 보유 아이템을 비우고 기본 3칸에 현재 등록된 효과를 다시 반영
    // 이전 Run의 효과 제거는 각 효과 출처에서 처리
    public void ResetRun()
    {
        if (IsUsing || !IsInitialized)
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
        if (IsUsing)
        {
            _capacityRefreshPending = true;
            return;
        }
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
