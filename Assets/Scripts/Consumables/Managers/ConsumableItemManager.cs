using System;
using System.Collections.Generic;
using Game.Core;
using Units;
using Units.Skills;
using UnityEngine;

// 소모성 아이템 획득, 슬롯 관리 및 사용 요청
public class ConsumableItemManager : MonoBehaviour, IConsumableItemReader, IConsumableItemInventory, IConsumableItemUser, ISaveDataProvider<ConsumableItemSaveData>
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
    public bool CanUse => !IsUsing && CanUseInBattle() &&
        _targetSelector != null && _effectExecutor != null;

    public event Action InventoryChanged;
    public event Action<ConsumableItemData, ICombatTarget> TargetEffectApplying;
    public event Action<ConsumableItemData, ICombatTarget, IReadOnlyList<CombatApplicationResult>> TargetEffectApplied;

    [SerializeField] private ConsumableItemCatalog _itemCatalog;
    [SerializeField] private ConsumableItemCaster _itemCasterPrefab;
    [Header("소모성 아이템 획득 테스트")]
    [Tooltip("플레이 중 F6으로 카탈로그 획득 창을 엽니다. 일반 플레이에서는 꺼두세요.")]
    [SerializeField] private bool _enableCatalogTest = true;
    private bool _catalogTestOpen;
    private Vector2 _catalogTestScroll;
    private string _catalogTestSearch = "";
    private string _catalogTestMessage = "목록에서 획득 버튼을 누르세요.";
    private GUIStyle _catalogTestDescriptionStyle;
    private ConsumableItemCaster _itemCaster;

    private ConsumableItemInventory _inventory;
    private EffectManager _effectManager;
    private GameFlowController _gameFlow;
    private ConsumableItemTargetSelector _targetSelector;
    private ConsumableItemEffectExecutor _effectExecutor;
    private bool _capacityRefreshPending;

    // 별도 패널이나 Canvas 연결 없이 사용하는 획득 테스트입니다.
    private void Update()
    {
        if (!_enableCatalogTest)
        {
            _catalogTestOpen = false;
            return;
        }
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        bool toggle = keyboard != null && keyboard.f6Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        bool toggle = Input.GetKeyDown(KeyCode.F6);
#else
        bool toggle = false;
#endif
        if (toggle) _catalogTestOpen = !_catalogTestOpen;
    }

    private void OnGUI()
    {
        if (!_enableCatalogTest || !_catalogTestOpen) return;
        if (_catalogTestDescriptionStyle == null)
            _catalogTestDescriptionStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };

        float width = Mathf.Min(720f, Screen.width - 20f);
        float height = Mathf.Min(720f, Screen.height - 20f);
        GUI.Window(GetInstanceID(), new Rect((Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f, width, height), DrawCatalogTestWindow,
            "소모성 아이템 획득 테스트 — F6으로 열기 / 닫기");
    }

    private void DrawCatalogTestWindow(int windowId)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("이름 / ID 검색", GUILayout.Width(100f));
        string search = GUILayout.TextField(_catalogTestSearch);
        if (search != _catalogTestSearch)
        {
            _catalogTestSearch = search;
            _catalogTestScroll = Vector2.zero;
        }
        if (GUILayout.Button("닫기", GUILayout.Width(60f))) _catalogTestOpen = false;
        GUILayout.EndHorizontal();

        GUILayout.Label(IsInitialized ? $"보유 {ItemCount} / 수용량 {Capacity}" : "초기화 후 획득할 수 있습니다.");
        GUILayout.Label(IsInitialized && !HasEmptySlot ? "빈 슬롯이 없습니다. 아이템을 사용하거나 제거해주세요." : _catalogTestMessage,
            _catalogTestDescriptionStyle);
        if (_itemCatalog == null)
        {
            GUILayout.Label("Consumable Item Catalog가 연결되어 있지 않습니다.");
            return;
        }

        _catalogTestScroll = GUILayout.BeginScrollView(_catalogTestScroll);
        int shown = 0;
        string filter = _catalogTestSearch.Trim();
        foreach (var item in _itemCatalog.Items)
        {
            if (item == null) continue;
            if (filter.Length > 0 &&
                (item.Id ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                (item.DisplayName ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            shown++;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"{item.Id} · {item.DisplayName} · {item.TargetTeam} / {item.TargetMode}");
            GUILayout.Label(item.Description ?? "", _catalogTestDescriptionStyle);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && IsInitialized && !IsUsing && HasEmptySlot;
            if (GUILayout.Button("1개 획득", GUILayout.Height(28f)))
            {
                _catalogTestMessage = TryAdd(item)
                    ? $"{item.DisplayName} 1개 획득 완료"
                    : $"{item.DisplayName} 획득 실패 — 빈 슬롯과 초기화 상태를 확인하세요.";
            }
            GUI.enabled = enabled;
            GUILayout.EndVertical();
        }
        if (shown == 0) GUILayout.Label("표시할 아이템이 없습니다.");
        GUILayout.EndScrollView();
    }

    public bool TryUse(int slotIndex, Vector2? selectedPosition = null)
    {
        if (!CanUse ||
            !TryGetItem(slotIndex, out ConsumableItemData item) || item.Effects == null || item.Effects.Count == 0)
            return false;

        List<ICombatTarget> targets = _targetSelector.SelectTargets(item, selectedPosition);
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
        if (applied && item.IsAreaItem && selectedPosition.HasValue && item.UseVFX != null)
        {
            // 대상별 FX와 별개로, 아이템 사용 위치에 한 번만 재생합니다.
            var vfx = Instantiate(item.UseVFX);
            vfx.Play(selectedPosition.Value, item.Radius);
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

    public ConsumableItemSaveData CaptureSaveData()
    {
        if (!IsInitialized || IsUsing) throw new InvalidOperationException("아이템 초기화 및 사용 처리가 끝난 뒤 저장하세요.");
        var data = new ConsumableItemSaveData { Capacity = Capacity };
        foreach (var slot in Slots) data.SlotItemIds.Add(slot.IsEmpty ? "" : slot.Item.Id);
        return data;
    }

    // 아티팩트·제단·토템 효과를 먼저 복원한 다음 호출하세요.
    public void RestoreSaveData(ConsumableItemSaveData data)
    {
        if (!IsInitialized || IsUsing) throw new InvalidOperationException("아이템 초기화 및 사용 처리가 끝난 뒤 복원하세요.");
        if (data == null || data.SlotItemIds == null || data.Capacity < 0 || data.SlotItemIds.Count < data.Capacity)
            throw new ArgumentException("아이템 저장 데이터의 슬롯 수가 올바르지 않습니다.");
        var items = new List<ConsumableItemData>();
        int count = 0;
        foreach (string id in data.SlotItemIds)
        {
            if (string.IsNullOrEmpty(id)) { items.Add(null); continue; }
            if (!_itemCatalog.TryGetById(id, out var item)) throw new ArgumentException("카탈로그에 없는 아이템 ID: " + id);
            items.Add(item);
            count++;
        }
        if (items.Count > data.Capacity && (count <= data.Capacity || items[items.Count - 1] == null))
            throw new ArgumentException("아이템 초과 슬롯 상태가 올바르지 않습니다.");
        var restored = new ConsumableItemInventory(data.Capacity, items);
        _inventory.InventoryChanged -= HandleInventoryChanged;
        _inventory = restored;
        _inventory.InventoryChanged += HandleInventoryChanged;
        _capacityRefreshPending = false;
        // 저장된 최종 Capacity를 다시 기본 슬롯 + 효과로 더하지 않습니다.
        IsUsing = true;
        try { HandleInventoryChanged(); }
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
}
