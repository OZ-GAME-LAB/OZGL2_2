using System.Collections.Generic;
using Units.UnitDatas;
using UnityEngine;


namespace Units
{
    internal class UnitPrefabMapper
    {
        // ============================================================
        // Databases
        // ============================================================

        private readonly AllyUnitSpawnDatabaseSO
            _allyUnitDatabase;

        private readonly EnemyUnitSpawnDatabaseSO
            _enemyUnitDatabase;


        // ============================================================
        // Prefab Maps
        //
        // SpawnManager가 원본을 소유한다.
        // Mapper는 전달받은 Dictionary에 데이터만 작성한다.
        // ============================================================

        private readonly Dictionary<UnitType, GameObject>
            _allyUnitPrefabs;

        private readonly Dictionary<UnitType, GameObject>
            _enemyUnitPrefabs;


        // ============================================================
        // Constructor
        // ============================================================

        internal UnitPrefabMapper(
            AllyUnitSpawnDatabaseSO allyUnitDatabase,
            EnemyUnitSpawnDatabaseSO enemyUnitDatabase,
            Dictionary<UnitType, GameObject> allyUnitPrefabs,
            Dictionary<UnitType, GameObject> enemyUnitPrefabs)
        {
            _allyUnitDatabase =
                allyUnitDatabase;

            _enemyUnitDatabase =
                enemyUnitDatabase;

            _allyUnitPrefabs =
                allyUnitPrefabs;

            _enemyUnitPrefabs =
                enemyUnitPrefabs;
        }


        // ============================================================
        // Initialize
        // ============================================================

        internal void Initialize()
        {
            if (_allyUnitPrefabs == null ||
                _enemyUnitPrefabs == null)
            {
                Debug.LogError(
                    "[PrefabMapper] Prefab Map이 없습니다."
                );

                return;
            }


            _allyUnitPrefabs.Clear();
            _enemyUnitPrefabs.Clear();


            InitializeAllyPrefabs();
            InitializeEnemyPrefabs();
        }


        private void InitializeAllyPrefabs()
        {
            if (_allyUnitDatabase == null)
            {
                Debug.LogError(
                    "[PrefabMapper] AllyUnitSpawnDatabase가 없습니다."
                );

                return;
            }


            IReadOnlyList<AllyUnitClassData> classes =
                _allyUnitDatabase.Classes;


            if (classes == null)
                return;


            for (int i = 0;
                 i < classes.Count;
                 i++)
            {
                AllyUnitClassData classData =
                    classes[i];


                if (classData == null)
                    continue;


                RegisterPrefabs(
                    classData.UnitPrefabs,
                    UnitTeam.Ally
                );
            }
        }


        private void InitializeEnemyPrefabs()
        {
            if (_enemyUnitDatabase == null)
            {
                Debug.LogError(
                    "[PrefabMapper] EnemyUnitSpawnDatabase가 없습니다."
                );

                return;
            }


            IReadOnlyList<EnemyUnitFactionData> factions =
                _enemyUnitDatabase.Factions;


            if (factions == null)
                return;


            for (int i = 0;
                 i < factions.Count;
                 i++)
            {
                EnemyUnitFactionData factionData =
                    factions[i];


                if (factionData == null)
                    continue;


                RegisterPrefabs(
                    factionData.UnitPrefabs,
                    UnitTeam.Enemy
                );
            }
        }


        // ============================================================
        // Register
        // ============================================================

        private void RegisterPrefabs(
            IReadOnlyList<GameObject> prefabs,
            UnitTeam expectedTeam)
        {
            if (prefabs == null)
                return;


            for (int i = 0;
                 i < prefabs.Count;
                 i++)
            {
                GameObject prefab =
                    prefabs[i];


                if (prefab == null)
                    continue;


                RegisterPrefab(
                    prefab,
                    expectedTeam
                );
            }
        }


        private void RegisterPrefab(
            GameObject prefab,
            UnitTeam expectedTeam)
        {
            if (!TryGetUnitData(
                prefab,
                out UnitData unitData))
            {
                return;
            }


            if (unitData.Team !=
                expectedTeam)
            {
                Debug.LogError(
                    $"[PrefabMapper] {prefab.name}의 Team이 " +
                    $"Database와 일치하지 않습니다. " +
                    $"Database : {expectedTeam}, " +
                    $"UnitData : {unitData.Team}"
                );

                return;
            }


            Dictionary<UnitType, GameObject> prefabMap =
                GetPrefabMap(
                    expectedTeam
                );


            if (prefabMap.ContainsKey(
                unitData.UnitType))
            {
                Debug.LogError(
                    $"[PrefabMapper] " +
                    $"{expectedTeam} / {unitData.UnitType} " +
                    $"Prefab이 중복 등록되어 있습니다."
                );

                return;
            }


            prefabMap.Add(
                unitData.UnitType,
                prefab
            );
        }


        // ============================================================
        // Utility
        // ============================================================

        private Dictionary<UnitType, GameObject>
            GetPrefabMap(
                UnitTeam team)
        {
            return team ==
                UnitTeam.Ally
                ? _allyUnitPrefabs
                : _enemyUnitPrefabs;
        }


        private bool TryGetUnitData(
            GameObject prefab,
            out UnitData unitData)
        {
            unitData =
                null;


            Unit_RuntimeStatus runtimeStatus =
                prefab.GetComponent<Unit_RuntimeStatus>();


            if (runtimeStatus == null)
            {
                Debug.LogError(
                    $"[PrefabMapper] {prefab.name}에 " +
                    $"Unit_RuntimeStatus가 없습니다."
                );

                return false;
            }


            unitData =
                runtimeStatus.UnitData;


            if (unitData == null)
            {
                Debug.LogError(
                    $"[PrefabMapper] {prefab.name}에 " +
                    $"UnitData가 설정되어 있지 않습니다."
                );

                return false;
            }


            return true;
        }
    }
}