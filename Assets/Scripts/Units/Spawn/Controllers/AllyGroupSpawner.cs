using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Units.UnitDatas;
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

        private readonly UnitStatModifierManager
            _unitStatModifierManager;

        private readonly IReadOnlyDictionary<AllyUnitType, GameObject>
            _prefabMap;

        private readonly GameObject
            _groupPrefab;

        private readonly RallyGridAllocator
            _rallyGridAllocator;


        // ============================================================
        // Settings
        // ============================================================

        private readonly float
            _groupSpawnDuration;


        // ============================================================
        // Constructor
        // ============================================================

        internal AllyGroupSpawner(
            RuntimeUnitManager runtimeUnitManager,
            UnitStatModifierManager unitStatModifierManager,
            IReadOnlyDictionary<AllyUnitType, GameObject> prefabMap,
            GameObject groupPrefab,
            RallyGridAllocator rallyGridAllocator,
            float groupSpawnDuration)
        {
            _runtimeUnitManager =
                runtimeUnitManager;

            _unitStatModifierManager =
                unitStatModifierManager;

            _prefabMap =
                prefabMap;

            _groupPrefab =
                groupPrefab;

            _rallyGridAllocator =
                rallyGridAllocator;

            _groupSpawnDuration =
                groupSpawnDuration;
        }


        // ============================================================
        // Spawn
        // ============================================================

        internal async UniTask<Unit_GroupAI> SpawnGroupAsync(
            AllyUnitType unitType,
            Vector2 spawnPosition,
            int count,
            Vector2 rallyPoint,
            CancellationToken cancellationToken)
        {
            if (!ValidateSpawnRequest(
                unitType,
                count))
            {
                return null;
            }


            cancellationToken.ThrowIfCancellationRequested();


            Unit_GroupAI group =
                CreateGroup(
                    unitType,
                    spawnPosition
                );


            if (group == null)
                return null;


            IReadOnlyList<Vector2> rallyPositions =
                AllocateRallyPositions(
                    count,
                    rallyPoint
                );


            if (rallyPositions == null)
            {
                DestroyGroup(
                    group
                );

                return null;
            }


            try
            {
                int spawnedCount =
                    await SpawnUnitsAsync(
                        unitType,
                        count,
                        spawnPosition,
                        rallyPositions,
                        group,
                        cancellationToken
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


                // ========================================================
                // Spawn Complete
                //
                // 모든 Unit 생성이 끝나고
                // Runtime 등록까지 완료된 시점에
                // Group에 Spawn 완료를 전달한다.
                //
                // Rally 완료 여부는 GroupAI에서 별도로 집계하며,
                // Spawn + Rally가 모두 완료되었을 때
                // Spawning 상태를 종료한다.
                // ========================================================

                group.NotifySpawnCompleted();


                Debug.Log(
                    $"[AllyGroupSpawner] " +
                    $"아군 그룹 생성 완료 : " +
                    $"{group.name} / " +
                    $"{spawnedCount}명"
                );


                return group;
            }
            catch (OperationCanceledException)
            {
                DestroyGroup(
                    group
                );


                throw;
            }
        }


        // ============================================================
        // Group
        // ============================================================

        private Unit_GroupAI CreateGroup(
            AllyUnitType unitType,
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
                UnityEngine.Object.Instantiate(
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


                UnityEngine.Object.Destroy(
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


                UnityEngine.Object.Destroy(
                    groupObject
                );

                return null;
            }


            return group;
        }


        // ============================================================
        // Units
        // ============================================================

        private async UniTask<int> SpawnUnitsAsync(
            AllyUnitType unitType,
            int count,
            Vector2 spawnPosition,
            IReadOnlyList<Vector2> rallyPositions,
            Unit_GroupAI group,
            CancellationToken cancellationToken)
        {
            int spawnedCount =
                0;


            float spawnInterval =
                CalculateSpawnInterval(
                    count
                );


            for (int i = 0;
                 i < count;
                 i++)
            {
                cancellationToken.ThrowIfCancellationRequested();


                Unit_Gateway unit =
                    SpawnUnit(
                        unitType,
                        spawnPosition,
                        group.transform
                    );


                if (unit != null)
                {
                    if (!group.AddMember(
                        unit))
                    {
                        Debug.LogError(
                            $"[AllyGroupSpawner] " +
                            $"Group Member 등록 실패 : " +
                            $"{group.name} / {unit.name}"
                        );


                        UnityEngine.Object.Destroy(
                            unit.gameObject
                        );
                    }
                    else
                    {
                        spawnedCount++;


                        // Group 등록 후 Rally 이동을 시작한다.
                        // AddMember 시점에 RallyCompleted 이벤트가
                        // 먼저 구독되어 있어야 완료 이벤트를 놓치지 않는다.
                        unit.MoveToRally(
                            rallyPositions[i]
                        );
                    }
                }


                if (i >= count - 1)
                    continue;

                if (spawnInterval <= 0f)
                    continue;


                await UniTask.Delay(
                    TimeSpan.FromSeconds(
                        spawnInterval
                    ),
                    cancellationToken:
                        cancellationToken
                );
            }


            return spawnedCount;
        }


        private Unit_Gateway SpawnUnit(
            AllyUnitType unitType,
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
                UnityEngine.Object.Instantiate(
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


                UnityEngine.Object.Destroy(
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


                UnityEngine.Object.Destroy(
                    unitObject
                );

                return null;
            }


            Unit_RuntimeStatus runtimeStatus =
                unitObject.GetComponent<Unit_RuntimeStatus>();


            if (runtimeStatus == null)
            {
                Debug.LogError(
                    $"[AllyGroupSpawner] " +
                    $"RuntimeStatus가 없습니다 : " +
                    $"{unitObject.name}"
                );


                UnityEngine.Object.Destroy(
                    unitObject
                );

                return null;
            }


            UnitData unitData =
                runtimeStatus.UnitData;


            if (unitData == null)
            {
                Debug.LogError(
                    $"[AllyGroupSpawner] " +
                    $"UnitData가 없습니다 : " +
                    $"{unitObject.name}"
                );


                UnityEngine.Object.Destroy(
                    unitObject
                );

                return null;
            }


            FinalStatModifier finalModifier =
                _unitStatModifierManager.GetAllyFinalModifier(
                    unitData.GetAllyClass(),
                    unitData.GetAllyType()
                );


            // ========================================================
            // Initialize
            // ========================================================

            core.Initialize(
                finalModifier
            );


            if (!ValidateInitializedUnit(
                core,
                gateway,
                unitObject))
            {
                UnityEngine.Object.Destroy(
                    unitObject
                );

                return null;
            }


            return gateway;
        }


        // ============================================================
        // Spawn Interval
        // ============================================================

        private float CalculateSpawnInterval(
            int count)
        {
            if (count <= 1)
                return 0f;


            if (_groupSpawnDuration <= 0f)
                return 0f;


            return _groupSpawnDuration /
                (count - 1);
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
            AllyUnitType unitType,
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


            if (_unitStatModifierManager == null)
            {
                Debug.LogError(
                    "[AllyGroupSpawner] " +
                    "UnitStatModifierManager가 없습니다."
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


            if (_rallyGridAllocator == null)
            {
                Debug.LogError(
                    "[AllyGroupSpawner] " +
                    "RallyGridAllocator가 없습니다."
                );

                return false;
            }


            if (count <= 0 ||
                count > 5)
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
                    $"등록되지 않은 AllyUnitType : " +
                    $"{unitType}"
                );

                return false;
            }


            return true;
        }


        // ============================================================
        // Rally Point
        // ============================================================

        private IReadOnlyList<Vector2> AllocateRallyPositions(
            int count,
            Vector2 rallyPoint)
        {
            IReadOnlyList<Vector2> rallyPositions =
                _rallyGridAllocator.Allocate(
                    count,
                    rallyPoint
                );


            if (rallyPositions == null ||
                rallyPositions.Count != count)
            {
                Debug.LogError(
                    $"[AllyGroupSpawner] " +
                    $"Rally Position 할당 실패 : " +
                    $"Count={count}"
                );

                return null;
            }


            return rallyPositions;
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


                    UnityEngine.Object.Destroy(
                        member.gameObject
                    );
                }
            }


            UnityEngine.Object.Destroy(
                group.gameObject
            );
        }
    }
}