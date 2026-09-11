using System.Collections.Generic;
using UnityEngine;


namespace Units
{
    internal class AllyGroupSpawner
    {
        // ============================================================
        // References
        // ============================================================

        private readonly RuntimeUnitManager
            _runtimeUnitManager;

        private readonly IReadOnlyDictionary<UnitType, GameObject>
            _prefabMap;

        private readonly GameObject
            _groupPrefab;


        // ============================================================
        // Settings
        // ============================================================

        private readonly Vector2
            _unitSpacing;


        // ============================================================
        // Constructor
        // ============================================================

        internal AllyGroupSpawner(
            RuntimeUnitManager runtimeUnitManager,
            IReadOnlyDictionary<UnitType, GameObject> prefabMap,
            GameObject groupPrefab,
            Vector2 unitSpacing)
        {
            _runtimeUnitManager =
                runtimeUnitManager;

            _prefabMap =
                prefabMap;

            _groupPrefab =
                groupPrefab;

            _unitSpacing =
                unitSpacing;
        }


        // ============================================================
        // Spawn
        // ============================================================

        internal Unit_GroupAI SpawnGroup(
            UnitType unitType,
            Vector2 spawnPosition,
            int count,
            Vector2 rallyPoint)
        {
            if (!ValidateSpawnRequest(
                unitType,
                count))
            {
                return null;
            }


            Unit_GroupAI group =
                CreateGroup(
                    unitType,
                    spawnPosition
                );


            if (group == null)
                return null;


            int spawnedCount =
                SpawnUnits(
                    unitType,
                    count,
                    spawnPosition,
                    group
                );


            if (spawnedCount <= 0)
            {
                DestroyGroup(
                    group
                );

                return null;
            }


            ApplyRallyPoint(
                group,
                rallyPoint
            );


            // ========================================================
            // Runtime Registration
            //
            // Group + Members가 완성된 다음
            // 마지막에 RuntimeUnitManager에 전달한다.
            // ========================================================

            if (!_runtimeUnitManager.RegisterGroup(
                group))
            {
                Debug.LogError(
                    $"[AllyGroupSpawner] " +
                    $"Runtime Group 등록 실패 : " +
                    $"{group.name}"
                );


                DestroyGroup(
                    group
                );

                return null;
            }


            Debug.Log(
                $"[AllyGroupSpawner] " +
                $"아군 그룹 생성 완료 : " +
                $"{group.name} / " +
                $"{spawnedCount}명"
            );


            return group;
        }


        // ============================================================
        // Group
        // ============================================================

        private Unit_GroupAI CreateGroup(
            UnitType unitType,
            Vector2 position)
        {
            int groupId =
                _runtimeUnitManager.GetNextGroupId(
                    UnitTeam.Ally
                );


            if (groupId < 0)
            {
                Debug.LogError(
                    "[AllyGroupSpawner] " +
                    "Ally Group ID 발급 실패."
                );

                return null;
            }


            GameObject groupObject =
                Object.Instantiate(
                    _groupPrefab,
                    position,
                    Quaternion.identity
                );


            Unit_GroupAI group =
                groupObject.GetComponent<Unit_GroupAI>();


            if (group == null)
            {
                Debug.LogError(
                    $"[AllyGroupSpawner] {_groupPrefab.name}에 " +
                    $"Unit_GroupAI가 없습니다."
                );


                Object.Destroy(
                    groupObject
                );

                return null;
            }


            groupObject.name =
                $"Ally_{unitType}_{groupId}";


            group.Initialize(
                UnitTeam.Ally
            );


            if (group.Team !=
                UnitTeam.Ally)
            {
                Debug.LogError(
                    $"[AllyGroupSpawner] " +
                    $"Group Team 초기화 실패 : " +
                    $"{groupObject.name}"
                );


                Object.Destroy(
                    groupObject
                );

                return null;
            }


            return group;
        }


        // ============================================================
        // Units
        // ============================================================

        private int SpawnUnits(
            UnitType unitType,
            int count,
            Vector2 startPosition,
            Unit_GroupAI group)
        {
            int spawnedCount =
                0;


            for (int i = 0;
                 i < count;
                 i++)
            {
                Vector2 spawnPosition =
                    startPosition +
                    _unitSpacing * i;


                Unit_Gateway unit =
                    SpawnUnit(
                        unitType,
                        spawnPosition,
                        group.transform
                    );


                if (unit == null)
                    continue;


                if (!group.AddMember(
                    unit))
                {
                    Debug.LogError(
                        $"[AllyGroupSpawner] " +
                        $"Group Member 등록 실패 : " +
                        $"{group.name} / {unit.name}"
                    );


                    Object.Destroy(
                        unit.gameObject
                    );

                    continue;
                }


                spawnedCount++;
            }


            return spawnedCount;
        }


        private Unit_Gateway SpawnUnit(
            UnitType unitType,
            Vector2 position,
            Transform parent)
        {
            if (!_prefabMap.TryGetValue(
                unitType,
                out GameObject prefab))
            {
                Debug.LogError(
                    $"[AllyGroupSpawner] " +
                    $"등록되지 않은 아군 UnitType : " +
                    $"{unitType}"
                );

                return null;
            }


            if (prefab == null)
            {
                Debug.LogError(
                    $"[AllyGroupSpawner] " +
                    $"Prefab이 null입니다 : " +
                    $"{unitType}"
                );

                return null;
            }


            GameObject unitObject =
                Object.Instantiate(
                    prefab,
                    position,
                    Quaternion.identity,
                    parent
                );


            Unit_Core core =
                unitObject.GetComponent<Unit_Core>();


            if (core == null)
            {
                Debug.LogError(
                    $"[AllyGroupSpawner] {prefab.name}에 " +
                    $"Unit_Core가 없습니다."
                );


                Object.Destroy(
                    unitObject
                );

                return null;
            }


            Unit_Gateway gateway =
                unitObject.GetComponent<Unit_Gateway>();


            if (gateway == null)
            {
                Debug.LogError(
                    $"[AllyGroupSpawner] {prefab.name}에 " +
                    $"Unit_Gateway가 없습니다."
                );


                Object.Destroy(
                    unitObject
                );

                return null;
            }


            // ========================================================
            // Initialize
            // ========================================================

            core.Initialize();


            if (!ValidateInitializedUnit(
                core,
                gateway,
                unitObject))
            {
                Object.Destroy(
                    unitObject
                );

                return null;
            }


            return gateway;
        }


        // ============================================================
        // Validation
        // ============================================================

        private bool ValidateInitializedUnit(
            Unit_Core core,
            Unit_Gateway gateway,
            GameObject unitObject)
        {
            if (core.RuntimeStatus == null)
            {
                Debug.LogError(
                    $"[AllyGroupSpawner] " +
                    $"RuntimeStatus 초기화 실패 : " +
                    $"{unitObject.name}"
                );

                return false;
            }


            if (core.RuntimeStatus.UnitData == null)
            {
                Debug.LogError(
                    $"[AllyGroupSpawner] " +
                    $"UnitData가 없습니다 : " +
                    $"{unitObject.name}"
                );

                return false;
            }


            if (core.Team !=
                UnitTeam.Ally)
            {
                Debug.LogError(
                    $"[AllyGroupSpawner] " +
                    $"Core Team 불일치 : " +
                    $"{unitObject.name}"
                );

                return false;
            }


            if (gateway.Team !=
                UnitTeam.Ally)
            {
                Debug.LogError(
                    $"[AllyGroupSpawner] " +
                    $"Gateway Team 불일치 : " +
                    $"{unitObject.name}"
                );

                return false;
            }


            if (!gateway.IsAlive)
            {
                Debug.LogError(
                    $"[AllyGroupSpawner] " +
                    $"초기 Unit이 살아있지 않습니다 : " +
                    $"{unitObject.name}"
                );

                return false;
            }


            return true;
        }


        private bool ValidateSpawnRequest(
            UnitType unitType,
            int count)
        {
            if (_runtimeUnitManager == null)
            {
                Debug.LogError(
                    "[AllyGroupSpawner] " +
                    "RuntimeUnitManager가 없습니다."
                );

                return false;
            }


            if (_prefabMap == null)
            {
                Debug.LogError(
                    "[AllyGroupSpawner] " +
                    "Prefab Map이 없습니다."
                );

                return false;
            }


            if (_groupPrefab == null)
            {
                Debug.LogError(
                    "[AllyGroupSpawner] " +
                    "GroupPrefab이 없습니다."
                );

                return false;
            }


            if (count <= 0)
            {
                Debug.LogWarning(
                    $"[AllyGroupSpawner] " +
                    $"잘못된 Spawn Count : {count}"
                );

                return false;
            }


            if (!_prefabMap.ContainsKey(
                unitType))
            {
                Debug.LogError(
                    $"[AllyGroupSpawner] " +
                    $"등록되지 않은 UnitType : " +
                    $"{unitType}"
                );

                return false;
            }


            return true;
        }


        // ============================================================
        // Rally Point
        // ============================================================

        private void ApplyRallyPoint(
            Unit_GroupAI group,
            Vector2 rallyPoint)
        {
            if (group == null)
                return;


            // TODO:
            // 아군 배치 기능 구현 시 연결.
            //
            // group.SetRallyPoint(rallyPoint);
        }


        // ============================================================
        // Destroy
        // ============================================================

        private void DestroyGroup(
            Unit_GroupAI group)
        {
            if (group == null)
                return;


            IReadOnlyList<Unit_Gateway> members =
                group.Members;


            if (members != null)
            {
                for (int i = 0;
                     i < members.Count;
                     i++)
                {
                    Unit_Gateway member =
                        members[i];


                    if (member == null)
                        continue;


                    Object.Destroy(
                        member.gameObject
                    );
                }
            }


            Object.Destroy(
                group.gameObject
            );
        }
    }
}