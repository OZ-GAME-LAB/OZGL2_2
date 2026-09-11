// Current date KDH 2026-09-08
// 건물의 설계도(ScriptableObject).
// 현재 HP·슬롯 점유 같은 플레이 중 값은 여기에 두지 않습니다.
// SO를 여러 실물이 공유하므로, 여기서 HP를 깎으면 같은 종류가 전부 같이 깎입니다.
using UnityEngine;

namespace OZGL.KDH
{
    [CreateAssetMenu(fileName = "BuildingData", menuName = "OZGL/Buildings/Building Data")]
    public class BuildingData : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("세이브/해금/건설 메뉴 조회용. 한 번 정하면 바꾸지 마세요. 예: barracks_melee_1")]
        [SerializeField] private string buildingId = "building_id";
        [SerializeField] private string displayName = "새 건물";
        [TextArea(2, 4)]
        [SerializeField] private string description;
        [SerializeField] private BuildingType buildingType = BuildingType.Barracks;

        [Header("표시")]
        [SerializeField] private Sprite icon;
        [SerializeField] private Sprite worldSprite;
        [SerializeField] private GameObject prefab;

        [Header("건설")]
        [SerializeField] private BuildingResourceCost[] buildCost;

        [Header("모듈 - 쓰는 기능만 enabled")]
        [SerializeField] private BuildingProductionSettings production = new BuildingProductionSettings();
        [SerializeField] private BuildingSpawnSettings spawn = new BuildingSpawnSettings();

        public string BuildingId => buildingId;
        public string DisplayName => displayName;
        public string Description => description;
        public BuildingType BuildingType => buildingType;
        public Sprite Icon => icon;
        public Sprite WorldSprite => worldSprite;
        public GameObject Prefab => prefab;
        public BuildingResourceCost[] BuildCost => buildCost;
        public BuildingProductionSettings Production => production;
        public BuildingSpawnSettings Spawn => spawn;
        public bool HasProduction => production != null && production.enabled;
        public bool HasSpawn => spawn != null && spawn.enabled;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(buildingId))
                buildingId = name;

            if (buildCost != null)
            {
                // 음수 비용이 들어가면 경제 차감이 깨지므로 에디터에서만 보정합니다.
                for (int i = 0; i < buildCost.Length; i++)
                {
                    if (buildCost[i].amount < 0)
                        buildCost[i].amount = 0;
                }
            }

            WarnIfInvalid();
        }

        // Current date KDH 2026-09-11
        // OnEnable과 같이 호출하면 같은 경고가 두 번 떠서, OnValidate에서만 검사합니다.
        private void WarnIfInvalid()
        {
            if (string.IsNullOrWhiteSpace(buildingId))
            {
                Debug.LogWarning($"[BuildingData] buildingId가 비어 있습니다. 에셋: {name}", this);
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                Debug.LogWarning($"[BuildingData] displayName이 비어 있습니다. 에셋: {name}", this);
            }

            if (icon == null)
            {
                Debug.LogWarning($"[BuildingData] icon이 없습니다. 건설 메뉴가 비어 보일 수 있습니다. 에셋: {name}", this);
            }

            if (prefab == null && buildingType != BuildingType.Core)
            {
                Debug.LogWarning($"[BuildingData] prefab이 없습니다. 건설 시 Instantiate가 실패합니다. 에셋: {name}", this);
            }

            if (prefab == null && worldSprite == null && buildingType != BuildingType.Core)
            {
                Debug.LogWarning($"[BuildingData] prefab과 worldSprite가 모두 없습니다. 월드에 그릴 대상이 없습니다. 에셋: {name}", this);
            }

            if (buildCost == null || buildCost.Length == 0)
            {
                Debug.LogWarning($"[BuildingData] buildCost가 비어 있습니다. 무료 건물로 처리될 수 있습니다. 에셋: {name}", this);
            }
            else
            {
                for (int i = 0; i < buildCost.Length; i++)
                {
                    if (buildCost[i].amount <= 0)
                    {
                        Debug.LogWarning($"[BuildingData] buildCost[{i}]의 amount가 0 이하입니다. type: {buildCost[i].type}. 에셋: {name}", this);
                    }

                    for (int j = i + 1; j < buildCost.Length; j++)
                    {
                        if (buildCost[i].type != buildCost[j].type)
                            continue;

                        Debug.LogWarning($"[BuildingData] 같은 ResourceType이 중복입니다: {buildCost[i].type}. 경제 차감이 두 번 일어날 수 있습니다. 에셋: {name}", this);
                        break;
                    }
                }
            }

            WarnModules();
        }

        private void WarnModules()
        {
            if (buildingType == BuildingType.Producer && !HasProduction)
            {
                Debug.LogWarning($"[BuildingData] Producer인데 Production.enabled가 꺼져 있습니다. 에셋: {name}", this);
            }

            if (buildingType == BuildingType.Barracks && !HasSpawn)
            {
                Debug.LogWarning($"[BuildingData] Barracks인데 Spawn.enabled가 꺼져 있습니다. 에셋: {name}", this);
            }

            if (HasProduction && production.amount <= 0)
            {
                Debug.LogWarning($"[BuildingData] Production.amount가 0 이하입니다. 에셋: {name}", this);
            }

            if (HasSpawn && spawn.unitPrefab == null)
            {
                Debug.LogWarning($"[BuildingData] Spawn.unitPrefab이 없습니다. 에셋: {name}", this);
            }

            if (HasSpawn && spawn.countPerWave > spawn.maxAlive)
            {
                Debug.LogWarning($"[BuildingData] countPerWave가 maxAlive보다 큽니다. 에셋: {name}", this);
            }
        }
#endif
    }
}
