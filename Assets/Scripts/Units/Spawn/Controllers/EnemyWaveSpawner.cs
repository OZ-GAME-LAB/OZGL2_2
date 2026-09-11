using System.Collections.Generic;
using UnityEngine;


namespace Units
{
    internal class EnemyWaveSpawner
    {
        // ============================================================
        // Constants
        // ============================================================

        private const int MaxGroupSize =
            5;


        // ============================================================
        // References
        // ============================================================

        private readonly RuntimeUnitManager
            _runtimeUnitManager;

        private readonly IReadOnlyDictionary<UnitType, GameObject>
            _prefabMap;

        private readonly GameObject
            _groupPrefab;

        private readonly Transform
            _spawnPoint;


        // ============================================================
        // Settings
        // ============================================================

        private readonly Vector2
            _unitSpacing;

        private readonly Vector2
            _groupSpacing;


        // ============================================================
        // Constructor
        // ============================================================

        internal EnemyWaveSpawner(
            RuntimeUnitManager runtimeUnitManager,
            IReadOnlyDictionary<UnitType, GameObject> prefabMap,
            GameObject groupPrefab,
            Transform spawnPoint,
            Vector2 unitSpacing,
            Vector2 groupSpacing)
        {
            _runtimeUnitManager =
                runtimeUnitManager;

            _prefabMap =
                prefabMap;

            _groupPrefab =
                groupPrefab;

            _spawnPoint =
                spawnPoint;

            _unitSpacing =
                unitSpacing;

            _groupSpacing =
                groupSpacing;
        }


        // ============================================================
        // Spawn Wave
        // ============================================================

        internal void SpawnWave(
            IReadOnlyList<EnemySpawnRequest> requests)
        {
            if (!ValidateWaveRequest(
                requests))
            {
                return;
            }


            Vector2 basePosition =
                _spawnPoint.position;


            int spawnedGroupCount =
                0;


            for (int i = 0;
                 i < requests.Count;
                 i++)
            {
                EnemySpawnRequest request =
                    requests[i];


                if (request == null)
                {
                    Debug.LogWarning(
                        $"[EnemyWaveSpawner] " +
                        $"EnemySpawnRequest가 null입니다. " +
                        $"Index={i}"
                    );

                    continue;
                }


                if (!ValidateSpawnRequest(
                    request.UnitType,
                    request.Count))
                {
                    continue;
                }


                SpawnRequest(
                    request.UnitType,
                    request.Count,
                    basePosition,
                    ref spawnedGroupCount
                );
            }


            Debug.Log(
                $"[EnemyWaveSpawner] " +
                $"적 웨이브 생성 완료 : " +
                $"{spawnedGroupCount}개 그룹"
            );
        }


        // ============================================================
        // Spawn Request
        // ============================================================

        private void SpawnRequest(
            UnitType unitType,
            int totalCount,
            Vector2 basePosition,
            ref int spawnedGroupCount)
        {
            int remainingCount =
                totalCount;


            while (remainingCount > 0)
            {
                int groupSize =
                    Mathf.Min(
                        MaxGroupSize,
                        remainingCount
                    );


                Vector2 groupPosition =
                    basePosition +
                    _groupSpacing *
                    spawnedGroupCount;


                Unit_GroupAI group =
                    SpawnGroup(
                        unitType,
                        groupSize,
                        groupPosition
                    );


                if (group != null)
                {
                    spawnedGroupCount++;
                }


                remainingCount -=
                    groupSize;
            }
        }


        // ============================================================
        // Group
        // ============================================================

        private Unit_GroupAI SpawnGroup(
            UnitType unitType,
            int count,
            Vector2 position)
        {
            Unit_GroupAI group =
                CreateGroup(
                    unitType,
                    position
                );


            if (group == null)
                return null;


            int spawnedCount =
                SpawnUnits(
                    unitType,
                    count,
                    position,
                    group
                );


            if (spawnedCount <= 0)
            {
                DestroyGroup(
                    group
                );

                return null;
            }


            // ========================================================
            // Runtime Registration
            // ========================================================

            if (!_runtimeUnitManager.RegisterGroup(
                group))
            {
                Debug.LogError(
                    $"[EnemyWaveSpawner] " +
                    $"Runtime Group 등록 실패 : " +
                    $"{group.name}"
                );


                DestroyGroup(
                    group
                );

                return null;
            }


            Debug.Log(
                $"[EnemyWaveSpawner] " +
                $"적 그룹 생성 완료 : " +
                $"{group.name} / " +
                $"{spawnedCount}명"
            );


            return group;
        }


        private Unit_GroupAI CreateGroup(
            UnitType unitType,
            Vector2 position)
        {
            int groupId =
                _runtimeUnitManager.GetNextGroupId(
                    UnitTeam.Enemy
                );


            if (groupId < 0)
            {
                Debug.LogError(
                    "[EnemyWaveSpawner] " +
                    "Enemy Group ID 발급 실패."
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
                    $"[EnemyWaveSpawner] {_groupPrefab.name}에 " +
                    $"Unit_GroupAI가 없습니다."
                );


                Object.Destroy(
                    groupObject
                );

                return null;
            }


            groupObject.name =
                $"Enemy_{unitType}_{groupId}";


            group.Initialize(
                UnitTeam.Enemy
            );


            if (group.Team !=
                UnitTeam.Enemy)
            {
                Debug.LogError(
                    $"[EnemyWaveSpawner] " +
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
                        $"[EnemyWaveSpawner] " +
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
                    $"[EnemyWaveSpawner] " +
                    $"등록되지 않은 적 UnitType : " +
                    $"{unitType}"
                );

                return null;
            }


            if (prefab == null)
            {
                Debug.LogError(
                    $"[EnemyWaveSpawner] " +
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
                    $"[EnemyWaveSpawner] {prefab.name}에 " +
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
                    $"[EnemyWaveSpawner] {prefab.name}에 " +
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
                    $"[EnemyWaveSpawner] " +
                    $"RuntimeStatus 초기화 실패 : " +
                    $"{unitObject.name}"
                );

                return false;
            }


            if (core.RuntimeStatus.UnitData == null)
            {
                Debug.LogError(
                    $"[EnemyWaveSpawner] " +
                    $"UnitData가 없습니다 : " +
                    $"{unitObject.name}"
                );

                return false;
            }


            if (core.Team !=
                UnitTeam.Enemy)
            {
                Debug.LogError(
                    $"[EnemyWaveSpawner] " +
                    $"Core Team 불일치 : " +
                    $"{unitObject.name}"
                );

                return false;
            }


            if (gateway.Team !=
                UnitTeam.Enemy)
            {
                Debug.LogError(
                    $"[EnemyWaveSpawner] " +
                    $"Gateway Team 불일치 : " +
                    $"{unitObject.name}"
                );

                return false;
            }


            if (!gateway.IsAlive)
            {
                Debug.LogError(
                    $"[EnemyWaveSpawner] " +
                    $"초기 Unit이 살아있지 않습니다 : " +
                    $"{unitObject.name}"
                );

                return false;
            }


            return true;
        }


        private bool ValidateWaveRequest(
            IReadOnlyList<EnemySpawnRequest> requests)
        {
            if (_runtimeUnitManager == null)
            {
                Debug.LogError(
                    "[EnemyWaveSpawner] " +
                    "RuntimeUnitManager가 없습니다."
                );

                return false;
            }


            if (_prefabMap == null)
            {
                Debug.LogError(
                    "[EnemyWaveSpawner] " +
                    "Prefab Map이 없습니다."
                );

                return false;
            }


            if (_groupPrefab == null)
            {
                Debug.LogError(
                    "[EnemyWaveSpawner] " +
                    "GroupPrefab이 없습니다."
                );

                return false;
            }


            if (_spawnPoint == null)
            {
                Debug.LogError(
                    "[EnemyWaveSpawner] " +
                    "Enemy SpawnPoint가 없습니다."
                );

                return false;
            }


            if (requests == null ||
                requests.Count == 0)
            {
                Debug.LogWarning(
                    "[EnemyWaveSpawner] " +
                    "Enemy Spawn Request가 없습니다."
                );

                return false;
            }


            return true;
        }


        private bool ValidateSpawnRequest(
            UnitType unitType,
            int count)
        {
            if (count <= 0)
            {
                Debug.LogWarning(
                    $"[EnemyWaveSpawner] " +
                    $"잘못된 Spawn Count : " +
                    $"{unitType} / {count}"
                );

                return false;
            }


            if (!_prefabMap.ContainsKey(
                unitType))
            {
                Debug.LogError(
                    $"[EnemyWaveSpawner] " +
                    $"등록되지 않은 UnitType : " +
                    $"{unitType}"
                );

                return false;
            }


            return true;
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