// Current date KDH 2026-09-08
// 빈 건설 칸 클릭 → 목록 표시 → 지갑이 충분하면 건설.
// 클릭한 순간에만 OverlapPoint를 호출합니다. Update에서 매 프레임 땅을 훑지 않습니다.
using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace OZGL.KDH
{
    public class BuildingBuildController : MonoBehaviour
    {
        private const int HitBufferSize = 8;

        [SerializeField] private BuildingDatabase database;
        [SerializeField] private RunCurrencyManager wallet;
        [SerializeField] private GameFlowController gameFlow;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private LayerMask slotMask = ~0;
        [Range(0f, 1f)]
        [SerializeField] private float refundRate = 1f;

        private BuildingBuildMenu _menu;
        private Camera _camera;
        private BuildingSlot _focusedSlot;
        private BuildingCoreProgress _coreProgress;
        private readonly List<BuildingData> _candidates = new List<BuildingData>(16);
        private readonly Collider2D[] _hits = new Collider2D[HitBufferSize];
        private ContactFilter2D _filter;

        public float RefundRate => refundRate;

        // Current date KDH 2026-09-16
        // 카메라가 슬롯으로 확대/복귀할 수 있게, 칸 선택만 알려 줍니다. 확대 자체는 하지 않습니다.
        // 위치는 slot.BuildPosition로 잡으실 수 있습니다.
        public event Action<BuildingSlot> SlotSelected;
        public event Action SlotDeselected;

        private void Awake()
        {
            CacheRefs();
            SetupFilter();

            if (_menu == null)
                _menu = gameObject.AddComponent<BuildingBuildMenu>();

            _menu.Bind(this);

            if (wallet != null)
                wallet.BalanceChanged += OnWalletChanged;

            if (gameFlow != null)
                gameFlow.PhaseChanged += OnPhaseChanged;
        }

        private void OnDestroy()
        {
            if (wallet != null)
                wallet.BalanceChanged -= OnWalletChanged;

            if (gameFlow != null)
                gameFlow.PhaseChanged -= OnPhaseChanged;
        }

        private void Update()
        {
            // 입력 엣지만 봅니다. 슬롯 점유 여부를 매 프레임 검사하지 않습니다.
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return;

            HandleClick();
        }

        public bool TryBuild(BuildingSlot slot, BuildingData data)
        {
            if (slot == null)
            {
                Debug.LogWarning("[BuildingBuildController] TryBuild에 BuildingSlot이 null입니다.", this);
                return false;
            }

            if (data == null)
            {
                Debug.LogWarning("[BuildingBuildController] TryBuild에 BuildingData가 null입니다.", this);
                return false;
            }

            if (!CanBuildNow())
                return false;

            //if (slot.IsOccupied)
            //{
            //    Debug.LogWarning("[BuildingBuildController] 이미 건물이 있는 칸입니다.", this);
            //    return false;
            //}

            // Current date KDH 2026-09-09
            // 점유된 칸은 교체로 보냅니다. 위 주석은 그대로 두고, TryOccupy 전에 옛 건물을 해제합니다.
            if (slot.IsOccupied)
                return TryReplace(slot, data);

            if (data.IsCore)
            {
                Debug.LogWarning("[BuildingBuildController] 코어는 빈 칸에 건설할 수 없습니다.", this);
                return false;
            }

            if (!data.CanBuildFromEmptySlot(GetCurrentCoreLevel()))
            {
                Debug.LogWarning($"[BuildingBuildController] 아직 해금되지 않았거나 빈 칸에서 지을 수 없는 건물입니다: {data.DisplayName}", this);
                return false;
            }

            if (!CanCreateVisual(data))
                return false;

            if (!IsWalletReady())
            {
                Debug.LogWarning("[BuildingBuildController] RunCurrencyManager가 없어 건설할 수 없습니다.", this);
                return false;
            }

            if (!TrySpendCosts(data.BuildCost))
                return false;

            Building building = SpawnBuilding(data, slot.BuildPosition);
            if (building == null)
            {
                Debug.LogWarning("[BuildingBuildController] 건물 생성에 실패했습니다. 재화는 이미 차감되었을 수 있습니다.", this);
                return false;
            }

            if (!slot.TryOccupy(building))
            {
                Debug.LogWarning("[BuildingBuildController] 슬롯 점유에 실패해 생성한 건물을 제거합니다.", this);
                Destroy(building.gameObject);
                return false;
            }

            HideMenu();
            return true;
        }

        // Current date KDH 2026-09-09
        public bool TryDemolish(BuildingSlot slot)
        {
            if (slot == null)
            {
                Debug.LogWarning("[BuildingBuildController] TryDemolish에 BuildingSlot이 null입니다.", this);
                return false;
            }

            if (!slot.IsOccupied)
            {
                Debug.LogWarning("[BuildingBuildController] 빈 칸은 철거할 수 없습니다.", this);
                return false;
            }

            if (IsCoreSlot(slot))
            {
                Debug.LogWarning("[BuildingBuildController] 코어는 철거할 수 없습니다.", this);
                return false;
            }

            if (!CanBuildNow())
                return false;

            if (!IsWalletReady())
            {
                Debug.LogWarning("[BuildingBuildController] RunCurrencyManager가 없어 환불할 수 없습니다.", this);
                return false;
            }

            Building oldBuilding = slot.ReleaseCurrent();
            if (oldBuilding == null)
                return false;

            BuildingData oldData = oldBuilding.Data;
            if (oldData == null)
            {
                Debug.LogWarning("[BuildingBuildController] 철거할 건물에 BuildingData가 없어 환불하지 않습니다.", this);
            }
            else
            {
                RefundCosts(oldData.BuildCost, refundRate);
            }

            Destroy(oldBuilding.gameObject);

            HideMenu();
            return true;
        }

        // Current date KDH 2026-09-16
        // 업그레이드는 환불 차액이 아니라, 이 건물이 적어 둔 cost를 전부 소비합니다. Gold와 Gem이 같이 있으면 동시에 깎입니다.
        public bool TryUpgrade(BuildingSlot slot, BuildingData next)
        {
            if (slot == null)
            {
                Debug.LogWarning("[BuildingBuildController] TryUpgrade에 BuildingSlot이 null입니다.", this);
                return false;
            }

            if (next == null)
            {
                Debug.LogWarning("[BuildingBuildController] TryUpgrade에 BuildingData가 null입니다.", this);
                return false;
            }

            if (!slot.IsOccupied)
            {
                Debug.LogWarning("[BuildingBuildController] 빈 칸은 업그레이드할 수 없습니다.", this);
                return false;
            }

            if (!CanBuildNow())
                return false;

            Building current = slot.CurrentBuilding;
            if (current == null || current.Data == null)
            {
                Debug.LogWarning("[BuildingBuildController] 현재 건물에 BuildingData가 없어 업그레이드할 수 없습니다.", this);
                return false;
            }

            if (!current.Data.TryGetUpgradeCost(next, GetCurrentCoreLevel(), out BuildingResourceCost[] cost))
            {
                Debug.LogWarning($"[BuildingBuildController] {current.Data.DisplayName}에서 {next.DisplayName}(으)로 업그레이드할 수 없습니다.", this);
                return false;
            }

            if (current.Data.IsSameBuilding(next))
            {
                Debug.LogWarning("[BuildingBuildController] 같은 건물로는 업그레이드하지 않습니다.", this);
                return false;
            }

            if (!CanCreateVisual(next))
                return false;

            if (!IsWalletReady())
            {
                Debug.LogWarning("[BuildingBuildController] RunCurrencyManager가 없어 업그레이드할 수 없습니다.", this);
                return false;
            }

            if (!TrySpendCosts(cost))
                return false;

            Building spawned = SpawnBuilding(next, slot.BuildPosition);
            if (spawned == null)
            {
                Debug.LogWarning("[BuildingBuildController] 업그레이드 건물 생성에 실패했습니다. 재화는 이미 차감되었을 수 있습니다.", this);
                return false;
            }

            Building oldBuilding = slot.ReleaseCurrent();
            if (oldBuilding != null)
                Destroy(oldBuilding.gameObject);

            if (!slot.TryOccupy(spawned))
            {
                Debug.LogWarning("[BuildingBuildController] 업그레이드 후 슬롯 점유에 실패해 생성한 건물을 제거합니다.", this);
                Destroy(spawned.gameObject);
                return false;
            }

            HideMenu();
            return true;
        }

        public bool IsSameAsCurrent(BuildingSlot slot, BuildingData data)
        {
            if (slot == null || !slot.IsOccupied || data == null)
                return false;

            Building current = slot.CurrentBuilding;
            if (current == null || current.Data == null)
                return false;

            return current.Data.BuildingId == data.BuildingId;
        }

        public bool CanAffordCandidate(BuildingSlot slot, BuildingData data)
        {
            if (!HasWallet() || data == null)
                return false;

            if (slot == null || !slot.IsOccupied)
                return data.CanBuildFromEmptySlot(GetCurrentCoreLevel()) && CanAffordCosts(data.BuildCost);

            if (IsSameAsCurrent(slot, data))
                return false;

            BuildingResourceCost[] credit = GetCurrentBuildCost(slot);
            return CanAffordNet(data.BuildCost, credit, refundRate);
        }

        public bool CanAffordUpgrade(BuildingSlot slot, BuildingData next)
        {
            if (!HasWallet() || slot == null || next == null || !slot.IsOccupied)
                return false;

            Building current = slot.CurrentBuilding;
            if (current == null || current.Data == null)
                return false;

            if (!current.Data.TryGetUpgradeCost(next, GetCurrentCoreLevel(), out BuildingResourceCost[] cost))
                return false;

            return CanAffordCosts(cost);
        }

        public BuildingResourceCost[] GetUpgradeCost(BuildingSlot slot, BuildingData next)
        {
            if (slot == null || next == null || slot.CurrentBuilding == null || slot.CurrentBuilding.Data == null)
                return null;

            slot.CurrentBuilding.Data.TryGetUpgradeCost(next, GetCurrentCoreLevel(), out BuildingResourceCost[] cost);
            return cost;
        }

        public bool IsCoreSlot(BuildingSlot slot)
        {
            if (slot == null || slot.CurrentBuilding == null || slot.CurrentBuilding.Data == null)
                return false;

            return slot.CurrentBuilding.Data.IsCore;
        }

        public int GetCandidateNet(BuildingSlot slot, BuildingData data, BuildingResourceType type)
        {
            if (data == null)
                return 0;

            if (slot == null || !slot.IsOccupied)
                return SumCost(data.BuildCost, type);

            BuildingResourceCost[] credit = GetCurrentBuildCost(slot);
            return GetNetAmount(type, data.BuildCost, credit, refundRate);
        }

        private bool TryReplace(BuildingSlot slot, BuildingData data)
        {
            if (IsSameAsCurrent(slot, data))
            {
                Debug.LogWarning("[BuildingBuildController] 같은 건물로는 교체하지 않습니다.", this);
                return false;
            }

            if (!CanCreateVisual(data))
                return false;

            if (!IsWalletReady())
            {
                Debug.LogWarning("[BuildingBuildController] RunCurrencyManager가 없어 교체할 수 없습니다.", this);
                return false;
            }

            BuildingResourceCost[] credit = GetCurrentBuildCost(slot);

            Building spawned = SpawnBuilding(data, slot.BuildPosition);
            if (spawned == null)
            {
                Debug.LogWarning("[BuildingBuildController] 교체 건물 생성에 실패했습니다.", this);
                return false;
            }

            if (!TrySettleNet(data.BuildCost, credit, refundRate))
            {
                Destroy(spawned.gameObject);
                return false;
            }

            Building oldBuilding = slot.ReleaseCurrent();
            if (oldBuilding != null)
                Destroy(oldBuilding.gameObject);

            if (!slot.TryOccupy(spawned))
            {
                Debug.LogWarning("[BuildingBuildController] 교체 후 슬롯 점유에 실패해 생성한 건물을 제거합니다.", this);
                Destroy(spawned.gameObject);
                return false;
            }

            HideMenu();
            return true;
        }

        private static BuildingResourceCost[] GetCurrentBuildCost(BuildingSlot slot)
        {
            if (slot == null || slot.CurrentBuilding == null)
                return null;

            BuildingData data = slot.CurrentBuilding.Data;
            if (data == null)
            {
                Debug.LogWarning("[BuildingBuildController] 점유 건물에 BuildingData가 없습니다.", slot);
                return null;
            }

            return data.BuildCost;
        }

        private static int SumCost(BuildingResourceCost[] costs, BuildingResourceType type)
        {
            if (costs == null)
                return 0;

            int sum = 0;
            for (int i = 0; i < costs.Length; i++)
            {
                if (costs[i].type == type && costs[i].amount > 0)
                    sum += costs[i].amount;
            }

            return sum;
        }

        private void HandleClick()
        {
            if (IsPointerOverUi())
                return;

            if (!IsBuildPhase())
            {
                HideMenu();
                return;
            }

            BuildingSlot slot = FindSlotUnderCursor();
            if (slot == null)
            {
                HideMenu();
                return;
            }

            //if (slot.IsOccupied)
            //{
            //    if (_menu != null)
            //        _menu.Hide();
            //    return;
            //}

            if (!HasWallet())
            {
                Debug.LogWarning("[BuildingBuildController] RunCurrencyManager가 없거나 아직 초기화되지 않았습니다.", this);
                return;
            }

            // Current date KDH 2026-09-16
            // 점유된 칸은 건설 목록 대신 현재 건물의 업그레이드만 보여 줍니다. 최종 단계면 철거만 나옵니다.
            if (slot.IsOccupied)
            {
                CollectOccupiedMenuRows(slot);
                _menu.Show(slot, _candidates, wallet);
                NotifySlotSelected(slot);
                return;
            }

            if (database == null && !slot.HasAllowedOverride)
            {
                Debug.LogWarning("[BuildingBuildController] BuildingDatabase가 없고, 이 칸의 전용 목록도 비어 있습니다.", this);
                return;
            }

            slot.CollectCandidates(_candidates, database, GetCurrentCoreLevel());
            if (_candidates.Count == 0)
            {
                Debug.LogWarning("[BuildingBuildController] 이 칸에 건설 가능한 건물이 없습니다.", this);
                return;
            }

            _menu.Show(slot, _candidates, wallet);
            NotifySlotSelected(slot);
        }

        private void CollectOccupiedMenuRows(BuildingSlot slot)
        {
            _candidates.Clear();
            if (slot == null || slot.CurrentBuilding == null || slot.CurrentBuilding.Data == null)
            {
                Debug.LogWarning("[BuildingBuildController] 점유 건물에 BuildingData가 없어 업그레이드 목록을 만들 수 없습니다.", this);
                return;
            }

            slot.CurrentBuilding.Data.CollectUpgrades(_candidates, GetCurrentCoreLevel());
        }

        private int GetCurrentCoreLevel()
        {
            return _coreProgress != null ? _coreProgress.CurrentLevel : 0;
        }

        private BuildingSlot FindSlotUnderCursor()
        {
            Camera cam = _camera;
            if (cam == null)
            {
                _camera = Camera.main;
                cam = _camera;
            }

            if (cam == null)
            {
                Debug.LogWarning("[BuildingBuildController] Camera가 없어 클릭 위치를 변환할 수 없습니다.", this);
                return null;
            }

            Vector2 screen = Mouse.current.position.ReadValue();
            Vector3 screen3 = screen;
            screen3.z = Mathf.Abs(cam.transform.position.z);
            Vector3 world = cam.ScreenToWorldPoint(screen3);
            world.z = 0f;

            int count = Physics2D.OverlapPoint(world, _filter, _hits);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _hits[i];
                if (hit == null)
                    continue;

                BuildingSlot slot = hit.GetComponentInParent<BuildingSlot>();
                if (slot != null)
                    return slot;
            }

            return null;
        }

        private Building SpawnBuilding(BuildingData data, Vector3 position)
        {
            GameObject go;
            if (data.Prefab != null)
            {
                go = Instantiate(data.Prefab, position, Quaternion.identity);
            }
            else
            {
                go = new GameObject(data.DisplayName);
                go.transform.position = position;
                SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = data.WorldSprite;
            }

            if (!go.TryGetComponent(out Building building))
                building = go.AddComponent<Building>();

            building.Initialize(data);
            return building;
        }

        private bool CanCreateVisual(BuildingData data)
        {
            if (data.Prefab != null || data.WorldSprite != null)
                return true;

            Debug.LogWarning($"[BuildingBuildController] Prefab과 worldSprite가 없어 생성할 수 없습니다. 건물: {data.DisplayName}", this);
            return false;
        }

        private void OnWalletChanged(CurrencyData currency, int previous, int current)
        {
            if (_menu != null)
                _menu.RefreshAffordability();
        }

        // Current date KDH 2026-09-14
        // 준비 페이즈가 아니면 메뉴를 닫아, 전투 중 버튼으로 짓지 못하게 합니다.
        private void OnPhaseChanged(GamePhase phase)
        {
            if (gameFlow != null && gameFlow.CanEnterBuildMode())
                return;

            HideMenu();
        }

        // Current date KDH 2026-09-16
        // 메뉴를 닫을 때만 해제합니다. 다른 칸을 누르면 SlotSelected만 다시 올립니다.
        private void HideMenu()
        {
            if (_menu != null)
                _menu.Hide();

            if (_focusedSlot == null)
                return;

            _focusedSlot = null;
            SlotDeselected?.Invoke();
        }

        private void NotifySlotSelected(BuildingSlot slot)
        {
            _focusedSlot = slot;
            SlotSelected?.Invoke(slot);
        }

        private bool IsBuildPhase()
        {
            return gameFlow != null && gameFlow.CanEnterBuildMode();
        }

        private bool CanBuildNow()
        {
            if (gameFlow == null)
            {
                Debug.LogWarning("[BuildingBuildController] GameFlowController가 없어 건설할 수 없습니다.", this);
                return false;
            }

            if (IsBuildPhase())
                return true;

            Debug.LogWarning("[BuildingBuildController] 준비 페이즈가 아니라 건설할 수 없습니다.", this);
            return false;
        }

        private void CacheRefs()
        {
            if (worldCamera != null)
                _camera = worldCamera;
            else
                _camera = Camera.main;

            if (wallet == null)
                wallet = GetComponent<RunCurrencyManager>();

            if (wallet == null)
                wallet = FindFirstObjectByType<RunCurrencyManager>();

            if (gameFlow == null)
                gameFlow = FindFirstObjectByType<GameFlowController>();

            if (_menu == null)
                _menu = GetComponent<BuildingBuildMenu>();

            if (_coreProgress == null)
                _coreProgress = GetComponent<BuildingCoreProgress>();

            if (_coreProgress == null)
                _coreProgress = FindFirstObjectByType<BuildingCoreProgress>();

            if (_coreProgress == null)
                _coreProgress = gameObject.AddComponent<BuildingCoreProgress>();

            if (gameFlow == null)
                Debug.LogWarning("[BuildingBuildController] GameFlowController를 찾지 못했습니다. 준비 페이즈 검사를 할 수 없습니다.", this);

            if (database == null)
                Debug.LogWarning("[BuildingBuildController] BuildingDatabase가 비어 있습니다. 인스펙터에 연결하세요.", this);

            if (wallet == null)
                Debug.LogWarning("[BuildingBuildController] RunCurrencyManager를 찾지 못했습니다. 인스펙터에 연결하세요.", this);
        }

        // Current date KDH 2026-09-14
        // BuildingResourceType.Gold는 0, CurrencyType.Gold는 1이라 숫자를 그대로 넣으면 안 됩니다.
        private static CurrencyType ToCurrency(BuildingResourceType type)
        {
            switch (type)
            {
                case BuildingResourceType.Gold:
                    return CurrencyType.Gold;
                case BuildingResourceType.Gem:
                    return CurrencyType.Gem;
                default:
                    return CurrencyType.None;
            }
        }

        private bool HasWallet()
        {
            return wallet != null && wallet.IsInitialized;
        }

        private bool IsWalletReady()
        {
            if (wallet == null)
                return false;

            if (!wallet.IsInitialized)
            {
                Debug.LogWarning("[BuildingBuildController] RunCurrencyManager가 아직 초기화되지 않았습니다.", this);
                return false;
            }

            return true;
        }

        private bool CanAffordCosts(BuildingResourceCost[] costs)
        {
            if (!HasWallet())
                return false;

            if (costs == null || costs.Length == 0)
                return true;

            for (int i = 0; i < costs.Length; i++)
            {
                int need = costs[i].amount;
                if (need <= 0)
                    continue;

                CurrencyType currency = ToCurrency(costs[i].type);
                if (currency == CurrencyType.None)
                    return false;

                if (!wallet.CanSpend(currency, need))
                    return false;
            }

            return true;
        }

        private bool TrySpendCosts(BuildingResourceCost[] costs)
        {
            if (!CanAffordCosts(costs))
            {
                Debug.LogWarning("[BuildingBuildController] 재화가 부족해 차감하지 않았습니다.", this);
                return false;
            }

            if (costs == null || costs.Length == 0)
                return true;

            for (int i = 0; i < costs.Length; i++)
            {
                int need = costs[i].amount;
                if (need <= 0)
                    continue;

                if (!wallet.TrySpend(ToCurrency(costs[i].type), need))
                {
                    Debug.LogWarning("[BuildingBuildController] 차감 중 실패했습니다.", this);
                    return false;
                }
            }

            return true;
        }

        private void RefundCosts(BuildingResourceCost[] costs, float rate)
        {
            if (costs == null || !HasWallet())
                return;

            rate = Mathf.Clamp01(rate);
            for (int i = 0; i < costs.Length; i++)
            {
                int refund = Mathf.FloorToInt(costs[i].amount * rate);
                if (refund <= 0)
                    continue;

                CurrencyType currency = ToCurrency(costs[i].type);
                if (currency == CurrencyType.None)
                {
                    Debug.LogWarning($"[BuildingBuildController] 환불할 수 없는 재화 종류입니다: {costs[i].type}", this);
                    continue;
                }

                wallet.TryAdd(currency, refund);
            }
        }

        private static int GetNetAmount(BuildingResourceType type, BuildingResourceCost[] pay, BuildingResourceCost[] credit, float creditRate)
        {
            int payAmount = SumCost(pay, type);
            int creditAmount = Mathf.FloorToInt(SumCost(credit, type) * Mathf.Clamp01(creditRate));
            return payAmount - creditAmount;
        }

        private bool CanAffordNet(BuildingResourceCost[] pay, BuildingResourceCost[] credit, float creditRate)
        {
            return CanAffordNetType(BuildingResourceType.Gold, pay, credit, creditRate)
                && CanAffordNetType(BuildingResourceType.Gem, pay, credit, creditRate);
        }

        private bool CanAffordNetType(BuildingResourceType type, BuildingResourceCost[] pay, BuildingResourceCost[] credit, float creditRate)
        {
            int net = GetNetAmount(type, pay, credit, creditRate);
            if (net <= 0)
                return true;

            CurrencyType currency = ToCurrency(type);
            return currency != CurrencyType.None && wallet.CanSpend(currency, net);
        }

        private bool TrySettleNet(BuildingResourceCost[] pay, BuildingResourceCost[] credit, float creditRate)
        {
            if (!CanAffordNet(pay, credit, creditRate))
            {
                Debug.LogWarning("[BuildingBuildController] 교체 차액이 부족해 정산하지 않았습니다.", this);
                return false;
            }

            return ApplyNet(BuildingResourceType.Gold, pay, credit, creditRate)
                && ApplyNet(BuildingResourceType.Gem, pay, credit, creditRate);
        }

        private bool ApplyNet(BuildingResourceType type, BuildingResourceCost[] pay, BuildingResourceCost[] credit, float creditRate)
        {
            int net = GetNetAmount(type, pay, credit, creditRate);
            if (net == 0)
                return true;

            CurrencyType currency = ToCurrency(type);
            if (currency == CurrencyType.None)
            {
                Debug.LogWarning($"[BuildingBuildController] 정산할 수 없는 재화 종류입니다: {type}", this);
                return false;
            }

            if (net > 0)
                return wallet.TrySpend(currency, net);

            return wallet.TryAdd(currency, -net);
        }

        private void SetupFilter()
        {
            _filter = new ContactFilter2D();
            _filter.useTriggers = true;
            _filter.useLayerMask = true;
            _filter.SetLayerMask(slotMask);
        }

        private static bool IsPointerOverUi()
        {
            EventSystem eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            refundRate = Mathf.Clamp01(refundRate);
        }
#endif
    }
}
