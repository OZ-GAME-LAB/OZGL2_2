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

        private readonly Dictionary<AllyUnitType, AllySpawnEntry>
            _allySpawnEntries;

        private readonly Dictionary<EnemyUnitType, EnemySpawnEntry>
            _enemySpawnEntries;


        // ============================================================
        // Constructor
        // ============================================================

        internal UnitPrefabMapper(
            AllyUnitSpawnDatabaseSO allyUnitDatabase,
            EnemyUnitSpawnDatabaseSO enemyUnitDatabase,
            Dictionary<AllyUnitType, AllySpawnEntry> allySpawnEntries,
            Dictionary<EnemyUnitType, EnemySpawnEntry> enemySpawnEntries)
        {
            _allyUnitDatabase =
                allyUnitDatabase;

            _enemyUnitDatabase =
                enemyUnitDatabase;

            _allySpawnEntries =
                allySpawnEntries;

            _enemySpawnEntries =
                enemySpawnEntries;
        }


        // ============================================================
        // Initialize
        // ============================================================

        internal void Initialize()
        {
            if (_allySpawnEntries == null ||
                _enemySpawnEntries == null)
            {
                Debug.LogError(
                    "[PrefabMapper] Prefab Map이 없습니다."
                );

                return;
            }


            _allySpawnEntries.Clear();
            _enemySpawnEntries.Clear();


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


                RegisterAllyPrefabs(
                    classData.UnitPrefabs
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


                RegisterEnemyPrefabs(
                    factionData.UnitPrefabs
                );
            }
        }


        // ============================================================
        // Register - Ally
        // ============================================================

        private void RegisterAllyPrefabs(
            IReadOnlyList<GameObject> prefabs)
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


                RegisterAllyPrefab(
                    prefab
                );
            }
        }


        private void RegisterAllyPrefab(
            GameObject prefab)
        {
            if (!TryGetUnitData(
                prefab,
                out UnitData unitData))
            {
                return;
            }


            if (unitData.Team !=
                UnitTeam.Ally)
            {
                Debug.LogError(
                    $"[PrefabMapper] {prefab.name}의 Team이 " +
                    $"Database와 일치하지 않습니다. " +
                    $"Database : {UnitTeam.Ally}, " +
                    $"UnitData : {unitData.Team}"
                );

                return;
            }


            AllyUnitType unitType =
                unitData.GetAllyType();


            if (_allySpawnEntries.ContainsKey(
                unitType))
            {
                Debug.LogError(
                    $"[PrefabMapper] " +
                    $"{UnitTeam.Ally} / {unitType} " +
                    $"Prefab이 중복 등록되어 있습니다."
                );

                return;
            }


            _allySpawnEntries.Add(
                unitType,
                new AllySpawnEntry(
                    unitData.GetAllyTier(),
                    unitData.GetAllyClass(),
                    prefab
                )
            );
        }


        // ============================================================
        // Register - Enemy
        // ============================================================

        private void RegisterEnemyPrefabs(
            IReadOnlyList<GameObject> prefabs)
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


                RegisterEnemyPrefab(
                    prefab
                );
            }
        }


        private void RegisterEnemyPrefab(
            GameObject prefab)
        {
            if (!TryGetUnitData(
                prefab,
                out UnitData unitData))
            {
                return;
            }


            if (unitData.Team !=
                UnitTeam.Enemy)
            {
                Debug.LogError(
                    $"[PrefabMapper] {prefab.name}의 Team이 " +
                    $"Database와 일치하지 않습니다. " +
                    $"Database : {UnitTeam.Enemy}, " +
                    $"UnitData : {unitData.Team}"
                );

                return;
            }


            EnemyUnitType unitType =
                unitData.GetEnemyType();


            if (_enemySpawnEntries.ContainsKey(
                unitType))
            {
                Debug.LogError(
                    $"[PrefabMapper] " +
                    $"{UnitTeam.Enemy} / {unitType} " +
                    $"Prefab이 중복 등록되어 있습니다."
                );

                return;
            }


            _enemySpawnEntries.Add(
                unitType,
                new EnemySpawnEntry(
                    unitData.GetEnemyFaction(),
                    unitData.GetEnemyClass(),
                    prefab
                )
            );
        }


        // ============================================================
        // Utility
        // ============================================================

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
