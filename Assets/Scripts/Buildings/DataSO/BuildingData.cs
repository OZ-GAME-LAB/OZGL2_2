// Current date KDH 2026-09-08
// 건물의 설계도(ScriptableObject).
// 현재 HP·슬롯 점유 같은 플레이 중 값은 여기에 두지 않습니다.
// SO를 여러 실물이 공유하므로, 여기서 HP를 깎으면 같은 종류가 전부 같이 깎입니다.
using System.Collections.Generic;
using UnityEngine;
using Units;

namespace OZGL.KDH
{
    [CreateAssetMenu(fileName = "BuildingData", menuName = "Buildings/Building Data")]
    public class BuildingData : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("세이브/해금/건설 메뉴 조회용. 한 번 정하면 바꾸지 마세요. 예: barracks_melee_1")]
        [SerializeField] private string buildingId = "building_id";
        // Current date KDH 2026-09-22
        // 업그레이드해도 같은 계열로 조회합니다. melee1/melee2 → melee_barracks.
        [Tooltip("업그레이드 전후를 묶는 계열 ID입니다. 예: melee_barracks. 비우면 가문 조회에 안 잡힙니다.")]
        [SerializeField] private string buildingFamilyId;
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
        [Tooltip("끄면 빈 칸 건설 목록에 안 나옵니다. 업그레이드로만 나오는 T2 이상에 사용합니다.")]
        [SerializeField] private bool buildFromEmptySlot = true;
        [Tooltip("빈 칸에서 이 건물을 지을 때 필요한 코어 레벨입니다. 0이면 처음부터 가능합니다.")]
        [Min(0)] [SerializeField] private int requiredCoreLevel;
        // Current date KDH 2026-09-22
        // A가 있어야 B를 짓는 조건입니다. melee1이 아니라 melee_barracks처럼 가문 ID를 적습니다.
        [Tooltip("이 가문이 하나 이상 있어야 빈 칸에 지을 수 있습니다. 비우면 제한 없습니다.")]
        [SerializeField] private string[] requiredFamilyIds;

        [Header("코어")]
        [Tooltip("Core 타입일 때만 사용합니다. Core2면 2처럼, 도달 비교에 씁니다.")]
        [Min(0)] [SerializeField] private int coreLevel;

        [Header("모듈 - 쓰는 기능만 enabled")]
        [SerializeField] private BuildingProductionSettings production = new BuildingProductionSettings();
        [SerializeField] private BuildingSpawnSettings spawn = new BuildingSpawnSettings();

        // Current date KDH 2026-09-16
        // 칸 전체 목록이 아니라, 이 건물이 될 수 있는 다음 건물만 적습니다. 비우면 최종 단계입니다.
        [Header("업그레이드")]
        [SerializeField] private BuildingUpgradeOption[] upgrades;

        public string BuildingId => buildingId;
        public string BuildingFamilyId => buildingFamilyId;
        public string[] RequiredFamilyIds => requiredFamilyIds;
        public bool HasFamilyId => !string.IsNullOrWhiteSpace(buildingFamilyId);
        public string DisplayName => displayName;
        public string Description => description;
        public BuildingType BuildingType => buildingType;
        public Sprite Icon => icon;
        public Sprite WorldSprite => worldSprite;
        public GameObject Prefab => prefab;
        public BuildingResourceCost[] BuildCost => buildCost;
        public bool BuildFromEmptySlot => buildFromEmptySlot;
        public int RequiredCoreLevel => requiredCoreLevel;
        public int CoreLevel => coreLevel;
        public BuildingProductionSettings Production => production;
        public BuildingSpawnSettings Spawn => spawn;
        public BuildingUpgradeOption[] Upgrades => upgrades;
        public bool HasProduction => production != null && production.enabled;
        public bool HasSpawn => spawn != null && spawn.enabled;
        public bool HasUpgrades => upgrades != null && upgrades.Length > 0;
        public bool IsCore => buildingType == BuildingType.Core;
        public bool HasWorldVisual => prefab != null || worldSprite != null;

        public bool CanBuildFromEmptySlot(int currentCoreLevel)
        {
            if (IsCore)
                return false;

            if (!buildFromEmptySlot)
                return false;

            return currentCoreLevel >= requiredCoreLevel;
        }

        // Current date KDH 2026-09-22
        // 코어 해금과 가문 선행을 한 번에 봅니다. 기존 1인자 호출은 그대로 둡니다.
        public bool CanBuildFromEmptySlot(int currentCoreLevel, BuildingCensus census)
        {
            if (!CanBuildFromEmptySlot(currentCoreLevel))
                return false;

            return MeetsFamilyRequirements(census);
        }

        public bool MeetsFamilyRequirements(BuildingCensus census)
        {
            if (requiredFamilyIds == null || requiredFamilyIds.Length == 0)
                return true;

            for (int i = 0; i < requiredFamilyIds.Length; i++)
            {
                string familyId = requiredFamilyIds[i];
                if (string.IsNullOrWhiteSpace(familyId))
                    continue;

                if (census == null || !census.HasFamily(familyId))
                    return false;
            }

            return true;
        }

        public bool IsSameFamily(BuildingData other)
        {
            if (other == null || !HasFamilyId || !other.HasFamilyId)
                return false;

            return buildingFamilyId == other.buildingFamilyId;
        }

        public void CollectUpgrades(List<BuildingData> results, int currentCoreLevel)
        {
            if (results == null)
            {
                Debug.LogWarning($"[BuildingData] CollectUpgrades에 results List가 null입니다. 에셋: {name}", this);
                return;
            }

            results.Clear();
            if (upgrades == null)
                return;

            for (int i = 0; i < upgrades.Length; i++)
            {
                BuildingUpgradeOption option = upgrades[i];
                if (!IsUpgradeAvailable(option, currentCoreLevel))
                    continue;

                results.Add(option.nextBuilding);
            }
        }

        public bool TryGetUpgradeCost(BuildingData next, int currentCoreLevel, out BuildingResourceCost[] cost)
        {
            cost = null;
            if (next == null || upgrades == null)
                return false;

            for (int i = 0; i < upgrades.Length; i++)
            {
                BuildingUpgradeOption option = upgrades[i];
                if (!IsUpgradeAvailable(option, currentCoreLevel))
                    continue;

                if (!next.IsSameBuilding(option.nextBuilding))
                    continue;

                cost = option.cost;
                return true;
            }

            return false;
        }

        public bool IsUpgradeAvailable(BuildingUpgradeOption option, int currentCoreLevel)
        {
            if (option == null || option.nextBuilding == null)
                return false;

            if (IsSameBuilding(option.nextBuilding))
                return false;

            return currentCoreLevel >= option.requiredCoreLevel;
        }

        public bool IsSameBuilding(BuildingData other)
        {
            if (other == null)
                return false;

            if (other == this)
                return true;

            if (string.IsNullOrWhiteSpace(buildingId) || string.IsNullOrWhiteSpace(other.buildingId))
                return false;

            return buildingId == other.buildingId;
        }

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

            if (coreLevel < 0)
                coreLevel = 0;

            if (requiredCoreLevel < 0)
                requiredCoreLevel = 0;

            if (upgrades != null)
            {
                for (int i = 0; i < upgrades.Length; i++)
                {
                    BuildingUpgradeOption option = upgrades[i];
                    if (option == null)
                        continue;

                    if (option.requiredCoreLevel < 0)
                        option.requiredCoreLevel = 0;

                    if (option.cost == null)
                        continue;

                    for (int j = 0; j < option.cost.Length; j++)
                    {
                        if (option.cost[j].amount < 0)
                            option.cost[j].amount = 0;
                    }
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

            // Current date KDH 2026-09-22
            // 가문이 비어 있으면 HasFamily로 찾을 수 없습니다. 업그레이드 계열은 같은 ID를 넣습니다.
            if (string.IsNullOrWhiteSpace(buildingFamilyId))
            {
                Debug.LogWarning($"[BuildingData] buildingFamilyId가 비어 있습니다. 가문 조회에서 빠집니다. 에셋: {name}", this);
            }

            if (requiredFamilyIds != null)
            {
                for (int i = 0; i < requiredFamilyIds.Length; i++)
                {
                    string familyId = requiredFamilyIds[i];
                    if (string.IsNullOrWhiteSpace(familyId))
                    {
                        Debug.LogWarning($"[BuildingData] requiredFamilyIds[{i}]가 비어 있습니다. 에셋: {name}", this);
                        continue;
                    }

                    if (buildFromEmptySlot && HasFamilyId && familyId == buildingFamilyId)
                    {
                        Debug.LogWarning($"[BuildingData] 자기 가문을 선행 조건으로 두면 첫 건물을 지을 수 없습니다: {familyId}. 에셋: {name}", this);
                    }
                }
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

            if (buildingType == BuildingType.Core && coreLevel <= 0)
            {
                Debug.LogWarning($"[BuildingData] Core인데 coreLevel이 0입니다. 해금 비교가 안 됩니다. 에셋: {name}", this);
            }

            if (buildingType != BuildingType.Core && coreLevel > 0)
            {
                Debug.LogWarning($"[BuildingData] Core가 아닌데 coreLevel이 들어 있습니다. 에셋: {name}", this);
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

            // Current date KDH 2026-09-15
            // 소환은 unitType만 있으면 됩니다. Default면 SpawnManager가 고를 프리팹이 없습니다.
            if (HasSpawn && spawn.unitType == AllyUnitType.Default)
            {
                Debug.LogWarning($"[BuildingData] Spawn.unitType이 Default라 소환할 수 없습니다. 에셋: {name}", this);
            }

            if (HasSpawn && spawn.countPerWave > spawn.maxAlive)
            {
                Debug.LogWarning($"[BuildingData] countPerWave가 maxAlive보다 큽니다. 에셋: {name}", this);
            }

            WarnUpgrades();
        }

        // Current date KDH 2026-09-16
        // 보석은 마지막 단계 cost에만 넣거나, Gold와 같이 넣으면 동시에 소비됩니다.
        private void WarnUpgrades()
        {
            if (upgrades == null)
                return;

            for (int i = 0; i < upgrades.Length; i++)
            {
                BuildingUpgradeOption option = upgrades[i];
                if (option == null)
                {
                    Debug.LogWarning($"[BuildingData] upgrades[{i}]가 비어 있습니다. 에셋: {name}", this);
                    continue;
                }

                if (option.nextBuilding == null)
                {
                    Debug.LogWarning($"[BuildingData] upgrades[{i}]의 nextBuilding이 없습니다. 에셋: {name}", this);
                    continue;
                }

                if (IsSameBuilding(option.nextBuilding))
                {
                    Debug.LogWarning($"[BuildingData] upgrades[{i}]가 자기 자신을 가리킵니다. 에셋: {name}", this);
                }

                if (IsCore && !option.nextBuilding.IsCore)
                {
                    Debug.LogWarning($"[BuildingData] Core 업그레이드 대상이 Core가 아닙니다: {option.nextBuilding.DisplayName}. 에셋: {name}", this);
                }

                if (IsCore && option.requiredCoreLevel > coreLevel)
                {
                    Debug.LogWarning($"[BuildingData] upgrades[{i}]의 requiredCoreLevel이 현재 coreLevel보다 커서 이 코어를 업그레이드할 수 없습니다. 에셋: {name}", this);
                }

                if (!option.nextBuilding.HasWorldVisual)
                {
                    Debug.LogWarning($"[BuildingData] upgrades[{i}]의 nextBuilding에 prefab과 worldSprite가 없습니다. 에셋: {name}", this);
                }

                for (int j = i + 1; j < upgrades.Length; j++)
                {
                    BuildingUpgradeOption other = upgrades[j];
                    if (other == null || other.nextBuilding == null)
                        continue;

                    if (!option.nextBuilding.IsSameBuilding(other.nextBuilding))
                        continue;

                    Debug.LogWarning($"[BuildingData] 같은 다음 건물이 중복입니다: {option.nextBuilding.DisplayName}. 에셋: {name}", this);
                    break;
                }

                WarnUpgradeCost(option.cost, i);
            }
        }

        private void WarnUpgradeCost(BuildingResourceCost[] cost, int optionIndex)
        {
            if (cost == null || cost.Length == 0)
            {
                Debug.LogWarning($"[BuildingData] upgrades[{optionIndex}]의 cost가 비어 있습니다. 무료 업그레이드로 처리될 수 있습니다. 에셋: {name}", this);
                return;
            }

            bool anyPaid = false;
            for (int i = 0; i < cost.Length; i++)
            {
                if (cost[i].amount <= 0)
                {
                    Debug.LogWarning($"[BuildingData] upgrades[{optionIndex}].cost[{i}]의 amount가 0 이하입니다. type: {cost[i].type}. 에셋: {name}", this);
                    continue;
                }

                anyPaid = true;

                for (int j = i + 1; j < cost.Length; j++)
                {
                    if (cost[i].type != cost[j].type)
                        continue;

                    Debug.LogWarning($"[BuildingData] upgrades[{optionIndex}]에 같은 ResourceType이 중복입니다: {cost[i].type}. 에셋: {name}", this);
                    break;
                }
            }

            if (!anyPaid)
            {
                Debug.LogWarning($"[BuildingData] upgrades[{optionIndex}]의 cost가 모두 0입니다. 에셋: {name}", this);
            }
        }
#endif
    }
}
