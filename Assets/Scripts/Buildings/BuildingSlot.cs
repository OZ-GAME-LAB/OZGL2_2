// Current date KDH 2026-09-08
// 미리 배치한 건설 칸. 빈 칸은 건설, 점유된 칸은 업그레이드입니다.
// 씬에 미리 둔 코어는 Start에서 자식 Building을 칸에 연결합니다.
// allowedBuildings를 채우면 칸마다 다른 목록, 비우면 Database 기본 목록을 씁니다.
using System.Collections.Generic;
using UnityEngine;

namespace OZGL.KDH
{
    [RequireComponent(typeof(Collider2D))]
    public class BuildingSlot : MonoBehaviour
    {
        [Tooltip("비우면 BuildingDatabase의 건설 가능 목록을 씁니다. 채우면 이 칸 전용 목록입니다.")]
        [SerializeField] private BuildingData[] allowedBuildings;
        [SerializeField] private Transform buildAnchor;
        [SerializeField] private SpriteRenderer emptyMarker;

        private Collider2D _collider;
        private BuildingData _preplacedData;

        public bool IsOccupied => CurrentBuilding != null;
        public Building CurrentBuilding { get; private set; }
        public Vector3 BuildPosition => buildAnchor != null ? buildAnchor.position : transform.position;
        public bool HasAllowedOverride => allowedBuildings != null && allowedBuildings.Length > 0;

        private void Reset()
        {
            if (GetComponent<Collider2D>() == null)
                gameObject.AddComponent<BoxCollider2D>();
        }

        private void Awake()
        {
            CacheRefs();
            RefreshMarker();
        }

        private void Start()
        {
            // Current date KDH 2026-09-17
            // 미리 배치한 코어는 TryBuild를 안 타서, 자식 Building을 여기서 칸에 연결합니다.
            ClaimPreplacedBuilding();
        }

        private void ClaimPreplacedBuilding()
        {
            if (IsOccupied)
                return;

            Building building = GetComponentInChildren<Building>(true);
            if (building == null)
                return;

            if (!TryOccupy(building))
            {
                Debug.LogWarning("[BuildingSlot] 미리 배치된 건물을 칸에 연결하지 못했습니다.", this);
                return;
            }

            _preplacedData = building.Data;
        }

        // Current date KDH 2026-09-18
        // 리셋 때 플레이어가 지은 건물은 지우고, 미리 둔 코어는 1단계 데이터로 되돌립니다.
        // CoreBuilding 프리팹 전체를 Instantiate하지 않습니다. 슬롯이 안에 또 생기면 안 됩니다.
        public void RestorePreplacedState()
        {
            if (IsOriginalPreplaced())
                return;

            if (IsOccupied)
            {
                Building extra = ReleaseCurrent();
                if (extra != null)
                    Destroy(extra.gameObject);
            }

            if (_preplacedData == null)
                return;

            Building restored = CreatePreplacedBuilding();
            if (!TryOccupy(restored))
            {
                Debug.LogWarning("[BuildingSlot] 미리 배치된 건물을 되돌리지 못했습니다.", this);
                Destroy(restored.gameObject);
            }
        }

        private bool IsOriginalPreplaced()
        {
            if (_preplacedData == null || CurrentBuilding == null || CurrentBuilding.Data == null)
                return false;

            return CurrentBuilding.Data.IsSameBuilding(_preplacedData);
        }

        private Building CreatePreplacedBuilding()
        {
            GameObject go = new GameObject(_preplacedData.DisplayName);
            go.transform.position = BuildPosition;
            Building building = go.AddComponent<Building>();
            building.Initialize(_preplacedData);
            return building;
        }

        public void CollectCandidates(List<BuildingData> results, BuildingDatabase database, int currentCoreLevel)
        {
            if (results == null)
            {
                Debug.LogWarning("[BuildingSlot] CollectCandidates에 results List가 null입니다.", this);
                return;
            }

            results.Clear();

            if (allowedBuildings != null && allowedBuildings.Length > 0)
            {
                CollectFromAllowed(results, currentCoreLevel);
                return;
            }

            if (database == null)
            {
                Debug.LogWarning("[BuildingSlot] Database가 없어 기본 목록을 만들 수 없습니다.", this);
                return;
            }

            database.CollectBuildable(results, currentCoreLevel);
        }

        public bool TryOccupy(Building building)
        {
            if (building == null)
            {
                Debug.LogWarning("[BuildingSlot] TryOccupy에 Building이 null입니다.", this);
                return false;
            }

            if (IsOccupied)
            {
                Debug.LogWarning("[BuildingSlot] 이미 건물이 있어 건설할 수 없습니다.", this);
                return false;
            }

            CurrentBuilding = building;
            building.transform.SetParent(transform, true);
            RefreshMarker();
            return true;
        }

        public void ClearOccupation()
        {
            CurrentBuilding = null;
            RefreshMarker();
        }

        // Current date KDH 2026-09-09
        // 점유만 풀고 건물 오브젝트는 지우지 않습니다. Destroy는 컨트롤러가 합니다.
        public Building ReleaseCurrent()
        {
            if (CurrentBuilding == null)
            {
                Debug.LogWarning("[BuildingSlot] 비어 있는 칸을 해제하려고 했습니다.", this);
                return null;
            }

            Building released = CurrentBuilding;
            CurrentBuilding = null;
            RefreshMarker();
            return released;
        }

        private void CollectFromAllowed(List<BuildingData> results, int currentCoreLevel)
        {
            for (int i = 0; i < allowedBuildings.Length; i++)
            {
                BuildingData data = allowedBuildings[i];
                if (data == null)
                {
                    Debug.LogWarning($"[BuildingSlot] allowedBuildings[{i}]가 비어 있습니다.", this);
                    continue;
                }

                if (!data.CanBuildFromEmptySlot(currentCoreLevel))
                    continue;

                results.Add(data);
            }

            if (results.Count == 0)
            {
                Debug.LogWarning("[BuildingSlot] 칸 전용 목록이 모두 비어 있거나, 코어 레벨로 아직 해금되지 않았습니다.", this);
            }
        }

        private void CacheRefs()
        {
            _collider = GetComponent<Collider2D>();
            if (_collider == null)
                Debug.LogWarning("[BuildingSlot] Collider2D가 없습니다. 클릭으로 칸을 찾을 수 없습니다.", this);

            if (emptyMarker == null)
                emptyMarker = GetComponent<SpriteRenderer>();
        }

        private void RefreshMarker()
        {
            if (emptyMarker == null)
                return;

            // Current date KDH 2026-09-17
            // 코어 프리팹은 건물 스프라이트를 emptyMarker에 넣어 두므로, 점유 때 끄면 코어가 사라집니다.
            if (IsOccupied && IsMarkerOnCurrentBuilding())
                return;

            emptyMarker.enabled = !IsOccupied;
        }

        private bool IsMarkerOnCurrentBuilding()
        {
            if (CurrentBuilding == null || emptyMarker == null)
                return false;

            SpriteRenderer buildingRenderer = CurrentBuilding.GetComponent<SpriteRenderer>();
            if (buildingRenderer == emptyMarker)
                return true;

            SpriteRenderer parentRenderer = CurrentBuilding.GetComponentInParent<SpriteRenderer>();
            return parentRenderer == emptyMarker;
        }
    }
}
