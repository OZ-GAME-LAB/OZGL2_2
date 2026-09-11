// Current date KDH 2026-09-08
// 건설 메뉴/해금에서 ID로 건물을 찾을 때 사용합니다.
// Dictionary는 OnEnable에서 한 번만 만들고, Update에서 new 하지 않습니다.
using System.Collections.Generic;
using UnityEngine;

namespace OZGL.KDH
{
    [CreateAssetMenu(fileName = "BuildingDatabase", menuName = "OZGL/Buildings/Building Database")]
    public class BuildingDatabase : ScriptableObject
    {
        [SerializeField] private BuildingData[] buildings;

        private Dictionary<string, BuildingData> _byId;

        public BuildingData[] Buildings => buildings;

        private void OnEnable()
        {
            RebuildLookup();
        }

        public BuildingData GetById(string buildingId)
        {
            if (_byId == null)
                RebuildLookup();

            if (string.IsNullOrEmpty(buildingId))
            {
                Debug.LogWarning($"[BuildingDatabase] GetById에 빈 ID가 들어왔습니다. 에셋: {name}", this);
                return null;
            }

            if (_byId.TryGetValue(buildingId, out BuildingData data))
                return data;

            Debug.LogWarning($"[BuildingDatabase] ID에 해당하는 건물이 없습니다: {buildingId}. 에셋: {name}", this);
            return null;
        }

        // 호출 쪽이 List를 재사용하면 클릭할 때마다 new List가 나지 않습니다.
        public void CollectBuildable(List<BuildingData> results)
        {
            if (results == null)
            {
                Debug.LogWarning($"[BuildingDatabase] CollectBuildable에 results List가 null입니다. 에셋: {name}", this);
                return;
            }

            results.Clear();

            if (buildings == null || buildings.Length == 0)
            {
                Debug.LogWarning($"[BuildingDatabase] buildings 배열이 비어 있어 건설 목록을 만들 수 없습니다. 에셋: {name}", this);
                return;
            }

            for (int i = 0; i < buildings.Length; i++)
            {
                BuildingData data = buildings[i];
                if (data == null)
                {
                    Debug.LogWarning($"[BuildingDatabase] buildings[{i}]가 비어 있습니다. 에셋: {name}", this);
                    continue;
                }

                // 본진은 미리 배치하는 건물로 보고 건설 목록에서 뺍니다.
                if (data.BuildingType == BuildingType.Core)
                    continue;

                // 타워 건물은 아직 만들 예정이 없습니다.
                if (data.BuildingType == BuildingType.Tower)
                    continue;

                results.Add(data);
            }
        }

        // Current date KDH 2026-09-08
        private void RebuildLookup()
        {
            int capacity = buildings != null ? buildings.Length : 0;
            _byId = new Dictionary<string, BuildingData>(capacity);

            if (buildings == null || buildings.Length == 0)
            {
                Debug.LogWarning($"[BuildingDatabase] buildings 배열이 비어 있습니다. 에셋: {name}", this);
                return;
            }

            for (int i = 0; i < buildings.Length; i++)
            {
                BuildingData data = buildings[i];
                if (data == null)
                {
                    Debug.LogWarning($"[BuildingDatabase] buildings[{i}]가 비어 있습니다. 에셋: {name}", this);
                    continue;
                }

                if (string.IsNullOrEmpty(data.BuildingId))
                {
                    Debug.LogWarning($"[BuildingDatabase] buildingId가 비어 있어 조회에서 제외합니다. 건물 에셋: {data.name}", this);
                    continue;
                }

                if (_byId.ContainsKey(data.BuildingId))
                {
                    Debug.LogWarning($"[BuildingDatabase] 같은 buildingId가 중복입니다: {data.BuildingId}. 뒤쪽 에셋이 앞쪽을 덮습니다. 에셋: {name}", this);
                }

                _byId[data.BuildingId] = data;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildLookup();
        }
#endif
    }
}
