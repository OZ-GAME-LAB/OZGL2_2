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
    // Current date KDH 2026-10-01
    // 건설 상태 저장/복원을 위해 ISaveDataProvider를 구현합니다. 파일 입출력은 SaveManager 쪽이 합니다.
    public partial class BuildingBuildController : MonoBehaviour, ISaveDataProvider<BuildingSaveData>
    {
        private const int HitBufferSize = 8;

        [SerializeField] private BuildingDatabase database;
        private RunCurrencyManager wallet;
        private GameFlowController gameFlow;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private LayerMask slotMask = ~0;
        [Range(0f, 1f)]
        [SerializeField] private float refundRate = 1f;
        // Current date KDH 2026-10-01
        [Tooltip("코어가 아직 등록되지 않았을 때 쓰는 건설 한도입니다. 0이면 제한 없음. 평소에는 코어 BuildingData의 buildLimit을 씁니다.")]
        [Min(0)] [SerializeField] private int fallbackBuildLimit = 0;

        private BuildingBuildMenu _menu;
        private Camera _camera;
        private BuildingSlot _focusedSlot;
        private BuildingCoreProgress _coreProgress;
        private BuildingCensus _census;
        private readonly List<BuildingData> _candidates = new List<BuildingData>(16);
        private readonly Collider2D[] _hits = new Collider2D[HitBufferSize];
        private ContactFilter2D _filter;

        // Current date KDH 2026-10-01
        // CaptureSaveData를 부를 때마다 new List가 생기지 않도록 재사용합니다.
        private readonly List<BuildingSlot> _saveBuffer = new List<BuildingSlot>(16);

        public float RefundRate => refundRate;
        public BuildingCensus Census => _census;

        // Current date KDH 2026-10-01
        // 건설 한도. 코어를 뺀 일반 건물 수가 현재 코어의 buildLimit보다 작아야 빈 칸에 지을 수 있습니다.
        public int BuiltCount => _census != null ? _census.BuiltCount : 0;

        public int BuildLimit
        {
            get
            {
                Building core = _coreProgress != null ? _coreProgress.CurrentCore : null;
                if (core == null || core.Data == null)
                    return fallbackBuildLimit;

                return core.Data.BuildLimit;
            }
        }

        // 한도 0은 "제한 없음"입니다. 값을 넣지 않은 기존 코어/테스트 씬이 갑자기 건설 불가가 되지 않게 합니다.
        public bool HasBuildCapacity => BuildLimit <= 0 || BuiltCount < BuildLimit;

        // (현재 개수, 한도). 건물 수나 코어가 바뀔 때만 울립니다. HUD는 Update에서 폴링하지 말고 이걸 구독하세요.
        public event Action<int, int> BuildCapacityChanged;

        // Current date KDH 2026-09-16
        // 카메라가 슬롯으로 확대/복귀할 수 있게, 칸 선택만 알려 줍니다. 확대 자체는 하지 않습니다.
        // 위치는 slot.BuildPosition로 잡으실 수 있습니다.
        public event Action<BuildingSlot> SlotSelected;
        public event Action SlotDeselected;

        private void Awake()
        {
            CacheRefs();
            SetupFilter();

            // if (_menu == null)
            //     _menu = gameObject.AddComponent<BuildingBuildMenu>();

            //_menu.Bind(this);
        }

        private void OnDestroy()
        {
            if (wallet != null)
                wallet.BalanceChanged -= OnWalletChanged;

            if (gameFlow != null)
                gameFlow.PhaseChanged -= OnPhaseChanged;

            UnsubscribeCapacitySources();
        }

        private void Update()
        {
            // 입력 엣지만 봅니다. 슬롯 점유 여부를 매 프레임 검사하지 않습니다.
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return;

            HandleClick();
        }

        // Current date KDH 2026-09-18
        // 지갑과 게임 진행은 Find하지 않고 BootStrap.Initialize로만 받습니다.
        public void Initialize(
            RunCurrencyManager runCurrencyManager,
            GameFlowController gameFlowController,
            BuildingCoreProgress buildingCoreProgress,
            BuildingCensus buildingCensus = null)
        {
            if (wallet != null)
                wallet.BalanceChanged -= OnWalletChanged;

            if (gameFlow != null)
                gameFlow.PhaseChanged -= OnPhaseChanged;

            // Current date KDH 2026-10-01
            // 재초기화 시 중복 구독을 막습니다.
            UnsubscribeCapacitySources();

            wallet = runCurrencyManager;
            gameFlow = gameFlowController;
            _coreProgress = buildingCoreProgress;
            _census = buildingCensus;

            if (wallet == null)
            {
                Debug.LogWarning("[BuildingBuildController] Initialize에 RunCurrencyManager가 null입니다.", this);
            }
            else
            {
                wallet.BalanceChanged += OnWalletChanged;
            }

            if (gameFlow == null)
            {
                Debug.LogWarning("[BuildingBuildController] Initialize에 GameFlowController가 null입니다.", this);
            }
            else
            {
                gameFlow.PhaseChanged += OnPhaseChanged;
            }

            if (_coreProgress == null)
                Debug.LogWarning("[BuildingBuildController] Initialize에 BuildingCoreProgress가 null입니다.", this);

            // Current date KDH 2026-09-22
            // 씬에 Census가 없어도 같은 오브젝트에 붙여 슬롯을 모읍니다.
            if (_census == null)
                _census = GetComponent<BuildingCensus>();

            if (_census == null)
                _census = gameObject.AddComponent<BuildingCensus>();

            _census.Initialize();

            // Current date KDH 2026-10-01
            // 건설 한도는 건물 수(Census)와 코어(CoreProgress)가 바뀔 때만 다시 알립니다.
            if (_coreProgress != null)
                _coreProgress.Changed += OnCapacitySourceChanged;

            _census.Changed += OnCapacitySourceChanged;
            OnCapacitySourceChanged();
        }

        // Current date KDH 2026-10-01
        private void OnCapacitySourceChanged()
        {
            BuildCapacityChanged?.Invoke(BuiltCount, BuildLimit);
        }

        private void UnsubscribeCapacitySources()
        {
            if (_coreProgress != null)
                _coreProgress.Changed -= OnCapacitySourceChanged;

            if (_census != null)
                _census.Changed -= OnCapacitySourceChanged;
        }

        // Current date KDH 2026-10-01
        // 현재 칸에 지어진 건물 → BuildingSaveData. 매번 새 객체를 반환해 런타임 상태와 참조를 공유하지 않습니다.
        public BuildingSaveData CaptureSaveData()
        {
            BuildingSaveData data = new BuildingSaveData();
            if (_census == null)
                return data;

            _census.CollectOccupied(_saveBuffer);
            for (int i = 0; i < _saveBuffer.Count; i++)
            {
                Building building = _saveBuffer[i].CurrentBuilding;
                if (building == null || building.Data == null)
                    continue;

                data.buildings.Add(new BuildingSaveEntry
                {
                    buildingId = building.Data.BuildingId,
                    position = _saveBuffer[i].BuildPosition
                });
            }

            return data;
        }

        // Current date KDH 2026-10-01
        // BuildingSaveData → 실제 건물 복원. 이미 지불한 건물이므로 재화는 차감하지 않습니다.
        // 미리 배치된 코어는 BuildingSlot.Start에서 칸에 연결되므로, 그 이후에 호출해야 합니다.
        // 로드 때 한 번만 실행되므로 FindObjectsByType과 배열 생성 비용은 문제가 되지 않습니다.
        public void RestoreSaveData(BuildingSaveData data)
        {
            if (data == null || data.buildings == null)
                throw new ArgumentNullException(nameof(data));

            if (database == null)
                throw new InvalidOperationException("BuildingDatabase가 없어 건물을 복원할 수 없습니다.");

            BuildingSlot[] slots = FindObjectsByType<BuildingSlot>(FindObjectsSortMode.None);
            int count = data.buildings.Count;
            BuildingSlot[] targetSlots = new BuildingSlot[count];
            BuildingData[] targetDatas = new BuildingData[count];

            // 1) 먼저 확인만 합니다. 하나라도 틀리면 아무것도 바꾸지 않고 예외를 던집니다.
            for (int i = 0; i < count; i++)
            {
                BuildingSaveEntry entry = data.buildings[i];
                targetDatas[i] = entry != null ? database.GetById(entry.buildingId) : null;
                targetSlots[i] = entry != null ? FindSlotAt(slots, entry.position) : null;

                if (targetDatas[i] == null || targetSlots[i] == null || !CanCreateVisual(targetDatas[i]))
                    throw new ArgumentException($"복원할 수 없는 건물 정보입니다: buildings[{i}]", nameof(data));
            }

            // 2) 모든 칸을 기본 상태(미리 배치된 코어 / 빈 칸)로 돌립니다. 리셋 로직을 재사용합니다.
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                    slots[i].RestorePreplacedState();
            }

            // 3) 저장된 건물을 세웁니다. 같은 건물이 이미 있으면(1단계 코어 등) 다시 만들지 않습니다.
            for (int i = 0; i < count; i++)
            {
                BuildingSlot slot = targetSlots[i];
                if (IsSameAsCurrent(slot, targetDatas[i]))
                    continue;

                Building spawned = SpawnBuilding(targetDatas[i], slot.BuildPosition);
                if (slot.IsOccupied)
                {
                    Building old = slot.ReleaseCurrent();
                    if (old != null)
                        Destroy(old.gameObject);
                }

                if (!slot.TryOccupy(spawned))
                {
                    Debug.LogWarning($"[BuildingBuildController] 저장된 건물을 칸에 복원하지 못했습니다: {targetDatas[i].DisplayName}", slot);
                    Destroy(spawned.gameObject);
                }
            }

            HideMenu();
        }

        // Current date KDH 2026-10-01
        // 저장된 위치와 같은 칸을 찾습니다. float 오차를 감안해 거리 제곱으로 비교합니다(제곱근 계산 생략).
        private static BuildingSlot FindSlotAt(BuildingSlot[] slots, Vector3 position)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && (slots[i].BuildPosition - position).sqrMagnitude < 0.0001f)
                    return slots[i];
            }

            return null;
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

            // Current date KDH 2026-10-01
            // 빈 칸에 새로 짓는 경우만 한도를 봅니다. 교체/업그레이드는 건물 수가 그대로라 위에서 이미 빠졌습니다.
            // 재화를 차감하기 전에 막아야 돈만 빠지는 일이 없습니다.
            if (!HasBuildCapacity)
            {
                Debug.LogWarning($"[BuildingBuildController] 건설 한도에 도달했습니다. ({BuiltCount}/{BuildLimit}) 코어를 업그레이드하세요.", this);
                return false;
            }

            if (!data.CanBuildFromEmptySlot(GetCurrentCoreLevel(), _census))
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

            // Current date KDH 2026-10-01
            // 빈 칸이면 건설 한도도 함께 봅니다. 두 건설 UI의 버튼이 이 값으로 꺼집니다.
            if (slot == null || !slot.IsOccupied)
                return HasBuildCapacity
                    && data.CanBuildFromEmptySlot(GetCurrentCoreLevel(), _census)
                    && CanAffordCosts(data.BuildCost);

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

            if (!data.MeetsFamilyRequirements(_census))
            {
                Debug.LogWarning($"[BuildingBuildController] 선행 건물 가문이 없어 교체할 수 없습니다: {data.DisplayName}", this);
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
                //_menu.Show(slot, _candidates, wallet);
                NotifySlotSelected(slot);
                return;
            }

            if (database == null && !slot.HasAllowedOverride)
            {
                Debug.LogWarning("[BuildingBuildController] BuildingDatabase가 없고, 이 칸의 전용 목록도 비어 있습니다.", this);
                return;
            }

            slot.CollectCandidates(_candidates, database, GetCurrentCoreLevel(), _census);
            if (_candidates.Count == 0)
            {
                Debug.LogWarning("[BuildingBuildController] 이 칸에 건설 가능한 건물이 없습니다.", this);
                return;
            }

            //_menu.Show(slot, _candidates, wallet);
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

        // Current date KDH 2026-09-18
        // 리셋 버튼은 None으로 들어옵니다. 메뉴를 닫고 칸을 짓기 전으로 되돌립니다.
        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.None)
                ResetPlacedBuildings();

            if (gameFlow != null && gameFlow.CanEnterBuildMode())
                return;

            HideMenu();
        }

        private void ResetPlacedBuildings()
        {
            BuildingSlot[] slots = FindObjectsByType<BuildingSlot>(FindObjectsSortMode.None);
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                    slots[i].RestorePreplacedState();
            }
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

            // if (_menu == null)
            //     _menu = GetComponent<BuildingBuildMenu>();

            //if (_coreProgress == null)
            //    _coreProgress = GetComponent<BuildingCoreProgress>();
            //
            //if (_coreProgress == null)
            //    _coreProgress = FindFirstObjectByType<BuildingCoreProgress>();
            //
            //if (_coreProgress == null)
            //    _coreProgress = gameObject.AddComponent<BuildingCoreProgress>();

            if (database == null)
                Debug.LogWarning("[BuildingBuildController] BuildingDatabase가 비어 있습니다. 인스펙터에 연결하세요.", this);
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
