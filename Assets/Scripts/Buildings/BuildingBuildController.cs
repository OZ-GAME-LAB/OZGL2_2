// Current date KDH 2026-09-08
// 빈 건설 칸 클릭 → 목록 표시 → 지갑이 충분하면 건설.
// 클릭한 순간에만 OverlapPoint를 호출합니다. Update에서 매 프레임 땅을 훑지 않습니다.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace OZGL.KDH
{
    public class BuildingBuildController : MonoBehaviour
    {
        private const int HitBufferSize = 8;

        [SerializeField] private BuildingDatabase database;
        [SerializeField] private PlayerWallet wallet;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private LayerMask slotMask = ~0;
        [Range(0f, 1f)]
        [SerializeField] private float refundRate = 1f;

        private BuildingBuildMenu _menu;
        private Camera _camera;
        private readonly List<BuildingData> _candidates = new List<BuildingData>(16);
        private readonly Collider2D[] _hits = new Collider2D[HitBufferSize];
        private ContactFilter2D _filter;

        public float RefundRate => refundRate;

        private void Awake()
        {
            CacheRefs();
            SetupFilter();

            if (_menu == null)
                _menu = gameObject.AddComponent<BuildingBuildMenu>();

            _menu.Bind(this);

            if (wallet != null)
                wallet.Changed += OnWalletChanged;
        }

        private void OnDestroy()
        {
            if (wallet != null)
                wallet.Changed -= OnWalletChanged;
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

            //if (slot.IsOccupied)
            //{
            //    Debug.LogWarning("[BuildingBuildController] 이미 건물이 있는 칸입니다.", this);
            //    return false;
            //}

            // Current date KDH 2026-09-09
            // 점유된 칸은 교체로 보냅니다. 위 주석은 그대로 두고, TryOccupy 전에 옛 건물을 해제합니다.
            if (slot.IsOccupied)
                return TryReplace(slot, data);

            if (!CanCreateVisual(data))
                return false;

            if (wallet == null)
            {
                Debug.LogWarning("[BuildingBuildController] PlayerWallet이 없어 건설할 수 없습니다.", this);
                return false;
            }

            if (!wallet.TrySpend(data.BuildCost))
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

            if (_menu != null)
                _menu.Hide();

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

            if (wallet == null)
            {
                Debug.LogWarning("[BuildingBuildController] PlayerWallet이 없어 환불할 수 없습니다.", this);
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
                wallet.Refund(oldData.BuildCost, refundRate);
            }

            Destroy(oldBuilding.gameObject);

            if (_menu != null)
                _menu.Hide();

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
            if (wallet == null || data == null)
                return false;

            if (slot == null || !slot.IsOccupied)
                return wallet.CanAfford(data.BuildCost);

            if (IsSameAsCurrent(slot, data))
                return false;

            BuildingResourceCost[] credit = GetCurrentBuildCost(slot);
            return wallet.CanAffordNet(data.BuildCost, credit, refundRate);
        }

        public int GetCandidateNet(BuildingSlot slot, BuildingData data, BuildingResourceType type)
        {
            if (wallet == null || data == null)
                return 0;

            if (slot == null || !slot.IsOccupied)
                return SumCost(data.BuildCost, type);

            BuildingResourceCost[] credit = GetCurrentBuildCost(slot);
            return wallet.GetNetAmount(type, data.BuildCost, credit, refundRate);
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

            if (wallet == null)
            {
                Debug.LogWarning("[BuildingBuildController] PlayerWallet이 없어 교체할 수 없습니다.", this);
                return false;
            }

            BuildingResourceCost[] credit = GetCurrentBuildCost(slot);

            Building spawned = SpawnBuilding(data, slot.BuildPosition);
            if (spawned == null)
            {
                Debug.LogWarning("[BuildingBuildController] 교체 건물 생성에 실패했습니다.", this);
                return false;
            }

            if (!wallet.TrySettleNet(data.BuildCost, credit, refundRate))
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

            if (_menu != null)
                _menu.Hide();

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

            BuildingSlot slot = FindSlotUnderCursor();
            if (slot == null)
            {
                if (_menu != null)
                    _menu.Hide();
                return;
            }

            //if (slot.IsOccupied)
            //{
            //    if (_menu != null)
            //        _menu.Hide();
            //    return;
            //}

            if (database == null && !slot.HasAllowedOverride)
            {
                Debug.LogWarning("[BuildingBuildController] BuildingDatabase가 없고, 이 칸의 전용 목록도 비어 있습니다.", this);
                return;
            }

            slot.CollectCandidates(_candidates, database);
            if (_candidates.Count == 0)
            {
                Debug.LogWarning("[BuildingBuildController] 이 칸에 건설 가능한 건물이 없습니다.", this);
                return;
            }

            if (wallet == null)
            {
                Debug.LogWarning("[BuildingBuildController] PlayerWallet이 없습니다.", this);
                return;
            }

            _menu.Show(slot, _candidates, wallet);
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

        private void OnWalletChanged(BuildingResourceType type, int amount)
        {
            if (_menu != null)
                _menu.RefreshAffordability();
        }

        private void CacheRefs()
        {
            if (worldCamera != null)
                _camera = worldCamera;
            else
                _camera = Camera.main;

            if (wallet == null)
                wallet = GetComponent<PlayerWallet>();

            if (wallet == null)
                wallet = FindFirstObjectByType<PlayerWallet>();

            if (_menu == null)
                _menu = GetComponent<BuildingBuildMenu>();

            if (database == null)
                Debug.LogWarning("[BuildingBuildController] BuildingDatabase가 비어 있습니다. 인스펙터에 연결하세요.", this);

            if (wallet == null)
                Debug.LogWarning("[BuildingBuildController] PlayerWallet을 찾지 못했습니다. 같은 오브젝트에 붙이거나 인스펙터에 연결하세요.", this);
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
