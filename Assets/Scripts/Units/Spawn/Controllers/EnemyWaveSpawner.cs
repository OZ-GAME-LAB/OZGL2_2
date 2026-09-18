using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Units.UnitDatas;
using UnityEngine;
using Game.Core;


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

        private readonly UnitStatModifierManager
            _unitStatModifierManager;

        private readonly IReadOnlyDictionary<EnemyUnitType, EnemySpawnEntry>
            _spawnEntries;

        private readonly GameObject
            _groupPrefab;

        private readonly IReadOnlyList<Transform>
            _spawnPoints;

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

        internal EnemyWaveSpawner(
            RuntimeUnitManager runtimeUnitManager,
            UnitStatModifierManager unitStatModifierManager,
            IReadOnlyDictionary<EnemyUnitType, EnemySpawnEntry> spawnEntries,
            GameObject groupPrefab,
            IReadOnlyList<Transform> spawnPoints,
            RallyGridAllocator rallyGridAllocator,
            float groupSpawnDuration)
        {
            _runtimeUnitManager =
                runtimeUnitManager;

            _unitStatModifierManager =
                unitStatModifierManager;

            _spawnEntries =
                spawnEntries;

            _groupPrefab =
                groupPrefab;

            _spawnPoints =
                spawnPoints;

            _rallyGridAllocator =
                rallyGridAllocator;

            _groupSpawnDuration =
                groupSpawnDuration;
        }


        // ============================================================
        // Spawn Wave
        // ============================================================

        internal async UniTask SpawnWaveAsync(
            int cost,
            SpawnContext context,
            CancellationToken cancellationToken)
        {
            if (!ValidateWaveRequest(
                cost,
                context))
            {
                return;
            }


            cancellationToken.ThrowIfCancellationRequested();


            List<EnemyGroupSpawnData>
                groupSpawnDataList =
                    BuildGroupSpawnData(
                        cost,
                        context
                    );


            if (groupSpawnDataList.Count == 0)
            {
                Debug.LogWarning(
                    "[EnemyWaveSpawner] " +
                    "생성 가능한 Enemy Group이 없습니다."
                );

                return;
            }


            // ========================================================
            // Rally Allocation
            //
            // 전체 Enemy Group 수가 확정된 시점에
            // RallyGridAllocator가 전체 배치 계획을 생성한다.
            //
            // Enemy는 오른쪽에서 왼쪽으로 이동하므로
            // Allocator 내부에서 Rally Area 중앙을 기준으로
            // 전열부터 후열 방향으로 Sector를 준비한다.
            // ========================================================

            if (!_rallyGridAllocator.PrepareCenteredAllocation(
                groupSpawnDataList.Count))
            {
                Debug.LogError(
                    "[EnemyWaveSpawner] " +
                    "Enemy Rally 배치 계획 생성 실패."
                );

                return;
            }


            int spawnedGroupCount =
                0;


            for (int startIndex = 0;
                 startIndex < groupSpawnDataList.Count;
                 startIndex += _spawnPoints.Count)
            {
                cancellationToken.ThrowIfCancellationRequested();


                List<UniTask<Unit_GroupAI>>
                    batchTasks =
                        new();


                for (int spawnPointIndex = 0;
                     spawnPointIndex < _spawnPoints.Count;
                     spawnPointIndex++)
                {
                    int groupIndex =
                        startIndex +
                        spawnPointIndex;


                    if (groupIndex >=
                        groupSpawnDataList.Count)
                    {
                        break;
                    }


                    Transform spawnPoint =
                        _spawnPoints[
                            spawnPointIndex
                        ];


                    if (spawnPoint == null)
                    {
                        Debug.LogWarning(
                            $"[EnemyWaveSpawner] " +
                            $"Enemy SpawnPoint가 null입니다. " +
                            $"Index={spawnPointIndex}"
                        );

                        continue;
                    }


                    EnemyGroupSpawnData
                        groupSpawnData =
                            groupSpawnDataList[
                                groupIndex
                            ];


                    UniTask<Unit_GroupAI>
                        spawnTask =
                            SpawnGroupAsync(
                                groupSpawnData.UnitType,
                                groupSpawnData.Count,
                                spawnPoint.position,
                                cancellationToken
                            );


                    batchTasks.Add(
                        spawnTask
                    );
                }


                if (batchTasks.Count == 0)
                    continue;


                Unit_GroupAI[] spawnedGroups =
                    await UniTask.WhenAll(
                        batchTasks
                    );


                for (int i = 0;
                     i < spawnedGroups.Length;
                     i++)
                {
                    if (spawnedGroups[i] != null)
                    {
                        spawnedGroupCount++;
                    }
                }
            }


            Debug.Log(
                $"[EnemyWaveSpawner] " +
                $"적 웨이브 생성 완료 : " +
                $"{spawnedGroupCount}개 그룹"
            );
        }


        // ============================================================
        // Spawn Data Build
        // ============================================================

        private List<EnemyGroupSpawnData>
            BuildGroupSpawnData(
                int cost,
                SpawnContext context)
        {
            List<EnemyGroupSpawnData>
                groupSpawnDataList =
                    new();


            List<ClassSpawnData>
                classSpawnDataList =
                    BuildClassSpawnData(
                        context
                    );


            if (classSpawnDataList.Count == 0)
            {
                return groupSpawnDataList;
            }


            if (!AllocateCostToClasses(
                cost,
                classSpawnDataList))
            {
                return groupSpawnDataList;
            }


            for (int i = 0;
                 i < classSpawnDataList.Count;
                 i++)
            {
                ClassSpawnData classSpawnData =
                    classSpawnDataList[i];


                AddClassGroupSpawnData(
                    classSpawnData,
                    groupSpawnDataList
                );
            }


            return groupSpawnDataList;
        }


        private List<ClassSpawnData>
            BuildClassSpawnData(
                SpawnContext context)
        {
            List<ClassSpawnData>
                classSpawnDataList =
                    new();


            HashSet<EnemyUnitType>
                registeredUnitTypes =
                    new();


            for (int i = 0;
                 i < context.MonsterIDs.Count;
                 i++)
            {
                EnemyUnitType unitType =
                    context.MonsterIDs[i];


                // SpawnContext에 같은 ID가 중복으로 들어와도
                // 동일 UnitType은 한 번만 Spawn 후보에 등록한다.
                if (!registeredUnitTypes.Add(
                    unitType))
                {
                    continue;
                }


                if (!_spawnEntries.TryGetValue(
                    unitType,
                    out EnemySpawnEntry entry))
                {
                    Debug.LogWarning(
                        $"[EnemyWaveSpawner] " +
                        $"SpawnContext에 등록되지 않은 " +
                        $"EnemyUnitType이 포함되어 있습니다 : " +
                        $"{unitType}"
                    );

                    continue;
                }


                EnemyUnitClass unitClass =
                    entry.UnitClass;


                if (unitClass ==
                    EnemyUnitClass.Default)
                {
                    Debug.LogWarning(
                        $"[EnemyWaveSpawner] " +
                        $"Default Class는 Wave Spawn 대상이 아닙니다 : " +
                        $"{unitType}"
                    );

                    continue;
                }


                int weight =
                    GetClassWeight(
                        unitClass,
                        context.Weights
                    );


                // Weight가 0인 Class는
                // MonsterID가 존재해도 이번 Wave에서는 Spawn하지 않는다.
                if (weight <= 0)
                {
                    continue;
                }


                ClassSpawnData classSpawnData =
                    FindClassSpawnData(
                        classSpawnDataList,
                        unitClass
                    );


                if (classSpawnData == null)
                {
                    classSpawnData =
                        new ClassSpawnData(
                            unitClass,
                            weight
                        );


                    classSpawnDataList.Add(
                        classSpawnData
                    );
                }


                classSpawnData.UnitTypes.Add(
                    unitType
                );
            }


            return classSpawnDataList;
        }


        private ClassSpawnData FindClassSpawnData(
            IReadOnlyList<ClassSpawnData> classSpawnDataList,
            EnemyUnitClass unitClass)
        {
            for (int i = 0;
                 i < classSpawnDataList.Count;
                 i++)
            {
                if (classSpawnDataList[i].UnitClass ==
                    unitClass)
                {
                    return classSpawnDataList[i];
                }
            }


            return null;
        }


        private int GetClassWeight(
            EnemyUnitClass unitClass,
            ClassWeights weights)
        {
            switch (unitClass)
            {
                case EnemyUnitClass.Tanker:
                    return weights.Tanker;

                case EnemyUnitClass.Bruiser:
                    return weights.Bruiser;

                case EnemyUnitClass.Assassin:
                    return weights.Assassin;

                case EnemyUnitClass.RangedPhysical:
                    return weights.RangedPhysical;

                case EnemyUnitClass.RangedMagic:
                    return weights.RangedMagic;

                case EnemyUnitClass.Supporter:
                    return weights.Supporter;

                default:
                    return 0;
            }
        }


        private bool AllocateCostToClasses(
            int cost,
            List<ClassSpawnData> classSpawnDataList)
        {
            int totalWeight =
                0;


            for (int i = 0;
                 i < classSpawnDataList.Count;
                 i++)
            {
                totalWeight +=
                    classSpawnDataList[i].Weight;
            }


            if (totalWeight <= 0)
            {
                Debug.LogWarning(
                    "[EnemyWaveSpawner] " +
                    "Spawn 가능한 Class Weight의 합이 0입니다."
                );

                return false;
            }


            int allocatedCost =
                0;


            for (int i = 0;
                 i < classSpawnDataList.Count;
                 i++)
            {
                ClassSpawnData classSpawnData =
                    classSpawnDataList[i];


                float exactCost =
                    cost *
                    ((float)classSpawnData.Weight /
                     totalWeight);


                int baseCost =
                    Mathf.FloorToInt(
                        exactCost
                    );


                classSpawnData.AllocatedCost =
                    baseCost;

                classSpawnData.Remainder =
                    exactCost -
                    baseCost;


                allocatedCost +=
                    baseCost;
            }


            int remainingCost =
                cost -
                allocatedCost;


            while (remainingCost > 0)
            {
                int targetIndex =
                    FindHighestRemainderClassIndex(
                        classSpawnDataList
                    );


                if (targetIndex < 0)
                {
                    Debug.LogError(
                        "[EnemyWaveSpawner] " +
                        "남은 Cost를 배분할 Class를 찾지 못했습니다."
                    );

                    return false;
                }


                classSpawnDataList[
                    targetIndex
                ].AllocatedCost++;


                // 동일한 Class가 다시 선택되지 않도록 한다.
                // 나머지 Cost는 최대 Class 수보다 작기 때문에
                // Largest Remainder 방식에서는 한 번만 배정하면 된다.
                classSpawnDataList[
                    targetIndex
                ].Remainder =
                    -1f;


                remainingCost--;
            }


            return true;
        }


        private int FindHighestRemainderClassIndex(
            IReadOnlyList<ClassSpawnData> classSpawnDataList)
        {
            int targetIndex =
                -1;

            float highestRemainder =
                -1f;


            for (int i = 0;
                 i < classSpawnDataList.Count;
                 i++)
            {
                ClassSpawnData classSpawnData =
                    classSpawnDataList[i];


                if (classSpawnData.Remainder <=
                    highestRemainder)
                {
                    continue;
                }


                highestRemainder =
                    classSpawnData.Remainder;

                targetIndex =
                    i;
            }


            return targetIndex;
        }


        private void AddClassGroupSpawnData(
            ClassSpawnData classSpawnData,
            List<EnemyGroupSpawnData> groupSpawnDataList)
        {
            if (classSpawnData.AllocatedCost <= 0)
                return;


            if (classSpawnData.UnitTypes.Count == 0)
                return;


            int unitTypeCount =
                classSpawnData.UnitTypes.Count;


            int baseCount =
                classSpawnData.AllocatedCost /
                unitTypeCount;


            int remainingCount =
                classSpawnData.AllocatedCost %
                unitTypeCount;


            for (int i = 0;
                 i < unitTypeCount;
                 i++)
            {
                int spawnCount =
                    baseCount;


                // 동일 Class 안에서 나머지가 발생하면
                // SpawnContext의 MonsterIDs 등록 순서대로 배분한다.
                if (i < remainingCount)
                {
                    spawnCount++;
                }


                if (spawnCount <= 0)
                    continue;


                EnemyUnitType unitType =
                    classSpawnData.UnitTypes[i];


                AddGroupSpawnData(
                    unitType,
                    spawnCount,
                    groupSpawnDataList
                );
            }
        }


        private void AddGroupSpawnData(
            EnemyUnitType unitType,
            int totalCount,
            List<EnemyGroupSpawnData> groupSpawnDataList)
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


                groupSpawnDataList.Add(
                    new EnemyGroupSpawnData(
                        unitType,
                        groupSize
                    )
                );


                remainingCount -=
                    groupSize;
            }
        }


        // ============================================================
        // Group
        // ============================================================

        private async UniTask<Unit_GroupAI>
            SpawnGroupAsync(
                EnemyUnitType unitType,
                int count,
                Vector2 position,
                CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();


            Unit_GroupAI group =
                CreateGroup(
                    unitType,
                    position
                );


            if (group == null)
                return null;


            IReadOnlyList<Vector2> rallyPositions =
                AllocateRallyPositions(
                    count,
                    group
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
                        position,
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


                group.NotifySpawnCompleted();


                Debug.Log(
                    $"[EnemyWaveSpawner] " +
                    $"적 그룹 생성 완료 : " +
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


        private Unit_GroupAI CreateGroup(
            EnemyUnitType unitType,
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
                    $"[EnemyWaveSpawner] {_groupPrefab.name}에 " +
                    $"Unit_GroupAI가 없습니다."
                );


                UnityEngine.Object.Destroy(
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
            EnemyUnitType unitType,
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
                            $"[EnemyWaveSpawner] " +
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
            EnemyUnitType unitType,
            Vector2 position,
            Transform parent)
        {
            if (!_spawnEntries.TryGetValue(
                unitType,
                out EnemySpawnEntry entry))
            {
                Debug.LogError(
                    $"[EnemyWaveSpawner] " +
                    $"등록되지 않은 적 EnemyUnitType : " +
                    $"{unitType}"
                );

                return null;
            }


            GameObject prefab = entry.Prefab;

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
                    $"[EnemyWaveSpawner] {prefab.name}에 " +
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
                    $"[EnemyWaveSpawner] {prefab.name}에 " +
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
                    $"[EnemyWaveSpawner] " +
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
                    $"[EnemyWaveSpawner] " +
                    $"UnitData가 없습니다 : " +
                    $"{unitObject.name}"
                );


                UnityEngine.Object.Destroy(
                    unitObject
                );

                return null;
            }


            FinalStatModifier finalModifier =
                _unitStatModifierManager.GetEnemyFinalModifier(
                    unitData.GetEnemyClass(),
                    unitData.GetEnemyType(),
                    unitData.GetEnemyFaction()
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
        // Rally Point
        // ============================================================

        private IReadOnlyList<Vector2> AllocateRallyPositions(
            int count,
            Unit_GroupAI group)
        {
            IReadOnlyList<Vector2> rallyPositions =
                _rallyGridAllocator.Allocate(
                    count,
                    group
                );


            if (rallyPositions == null ||
                rallyPositions.Count != count)
            {
                Debug.LogError(
                    $"[EnemyWaveSpawner] " +
                    $"Rally Position 할당 실패 : " +
                    $"Count={count}"
                );

                return null;
            }


            return rallyPositions;
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
            int cost,
            SpawnContext context)
        {
            if (_runtimeUnitManager == null)
            {
                Debug.LogError(
                    "[EnemyWaveSpawner] " +
                    "RuntimeUnitManager가 없습니다."
                );

                return false;
            }


            if (_unitStatModifierManager == null)
            {
                Debug.LogError(
                    "[EnemyWaveSpawner] " +
                    "UnitStatModifierManager가 없습니다."
                );

                return false;
            }


            if (_spawnEntries == null)
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


            if (_spawnPoints == null ||
                _spawnPoints.Count == 0)
            {
                Debug.LogError(
                    "[EnemyWaveSpawner] " +
                    "Enemy SpawnPoint가 없습니다."
                );

                return false;
            }


            if (_rallyGridAllocator == null)
            {
                Debug.LogError(
                    "[EnemyWaveSpawner] " +
                    "RallyGridAllocator가 없습니다."
                );

                return false;
            }


            if (cost <= 0)
            {
                Debug.LogWarning(
                    $"[EnemyWaveSpawner] " +
                    $"잘못된 Spawn Cost : {cost}"
                );

                return false;
            }


            if (context.MonsterIDs == null ||
                context.MonsterIDs.Count == 0)
            {
                Debug.LogWarning(
                    "[EnemyWaveSpawner] " +
                    "Spawn 가능한 MonsterID가 없습니다."
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


                    UnityEngine.Object.Destroy(
                        member.gameObject
                    );
                }
            }


            UnityEngine.Object.Destroy(
                group.gameObject
            );
        }


        // ============================================================
        // Spawn Data
        // ============================================================

        private sealed class ClassSpawnData
        {
            internal EnemyUnitClass
                UnitClass
            { get; }

            internal int
                Weight
            { get; }

            internal List<EnemyUnitType>
                UnitTypes
            { get; }

            internal int
                AllocatedCost
            { get; set; }

            internal float
                Remainder
            { get; set; }


            internal ClassSpawnData(
                EnemyUnitClass unitClass,
                int weight)
            {
                UnitClass =
                    unitClass;

                Weight =
                    weight;

                UnitTypes =
                    new List<EnemyUnitType>();
            }
        }


        private readonly struct EnemyGroupSpawnData
        {
            internal EnemyUnitType
                UnitType
            { get; }

            internal int
                Count
            { get; }


            internal EnemyGroupSpawnData(
                EnemyUnitType unitType,
                int count)
            {
                UnitType =
                    unitType;

                Count =
                    count;
            }
        }
    }
}