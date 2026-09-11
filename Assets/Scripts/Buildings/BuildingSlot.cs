// Current date KDH 2026-09-08
// 미리 배치한 건설 칸. 빈 칸만 클릭해서 건물을 올립니다.
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

        public void CollectCandidates(List<BuildingData> results, BuildingDatabase database)
        {
            if (results == null)
            {
                Debug.LogWarning("[BuildingSlot] CollectCandidates에 results List가 null입니다.", this);
                return;
            }

            results.Clear();

            if (allowedBuildings != null && allowedBuildings.Length > 0)
            {
                CollectFromAllowed(results);
                return;
            }

            if (database == null)
            {
                Debug.LogWarning("[BuildingSlot] Database가 없어 기본 목록을 만들 수 없습니다.", this);
                return;
            }

            database.CollectBuildable(results);
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

        private void CollectFromAllowed(List<BuildingData> results)
        {
            for (int i = 0; i < allowedBuildings.Length; i++)
            {
                BuildingData data = allowedBuildings[i];
                if (data == null)
                {
                    Debug.LogWarning($"[BuildingSlot] allowedBuildings[{i}]가 비어 있습니다.", this);
                    continue;
                }

                results.Add(data);
            }

            if (results.Count == 0)
            {
                Debug.LogWarning("[BuildingSlot] 칸 전용 목록이 모두 비어 있습니다.", this);
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

            emptyMarker.enabled = !IsOccupied;
        }
    }
}
