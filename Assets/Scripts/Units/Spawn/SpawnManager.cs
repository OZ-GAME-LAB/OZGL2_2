using System;
using System.Collections.Generic;
using UnityEngine;


namespace Units
{
    public class SpawnManager : MonoBehaviour
    {
        // ============================================================
        // References
        // ============================================================

        [SerializeField]
        private RuntimeUnitManager _runtimeUnitManager;

        [SerializeField]
        private AllyUnitSpawnDatabaseSO _allyUnitDatabase;

        [SerializeField]
        private EnemyUnitSpawnDatabaseSO _enemyUnitDatabase;

        [SerializeField]
        private GameObject _groupPrefab;


        // ============================================================
        // Ally Spawn Settings
        // ============================================================

        [Header("Ally Spawn")]

        [SerializeField]
        private Vector2 _allyUnitSpacing =
            new Vector2(1f, 0f);


        // ============================================================
        // Enemy Spawn Settings
        // ============================================================

        [Header("Enemy Spawn")]

        [SerializeField]
        private Transform _enemySpawnPoint;

        [SerializeField]
        private Vector2 _enemyUnitSpacing =
            new Vector2(1f, 0f);

        [SerializeField]
        private Vector2 _enemyGroupSpacing =
            new Vector2(0f, 1f);


        // ============================================================
        // Prefab Maps
        //
        // SpawnManager가 실제 Dictionary 원본을 소유한다.
        // ============================================================

        private readonly Dictionary<UnitType, GameObject>
            _allyUnitPrefabs =
                new Dictionary<UnitType, GameObject>();

        private readonly Dictionary<UnitType, GameObject>
            _enemyUnitPrefabs =
                new Dictionary<UnitType, GameObject>();


        // ============================================================
        // Internal Controllers
        // ============================================================

        private UnitPrefabMapper
            _prefabMapper;

        private AllyGroupSpawner
            _allyGroupSpawner;

        private EnemyWaveSpawner
            _enemyWaveSpawner;


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Awake()
        {
            InitializePrefabMapper();
            InitializeSpawners();
        }


        // ============================================================
        // Initialize
        // ============================================================

        private void InitializePrefabMapper()
        {
            _prefabMapper =
                new UnitPrefabMapper(
                    _allyUnitDatabase,
                    _enemyUnitDatabase,
                    _allyUnitPrefabs,
                    _enemyUnitPrefabs
                );


            _prefabMapper.Initialize();
        }


        private void InitializeSpawners()
        {
            _allyGroupSpawner =
                new AllyGroupSpawner(
                    _runtimeUnitManager,
                    _allyUnitPrefabs,
                    _groupPrefab,
                    _allyUnitSpacing
                );


            _enemyWaveSpawner =
                new EnemyWaveSpawner(
                    _runtimeUnitManager,
                    _enemyUnitPrefabs,
                    _groupPrefab,
                    _enemySpawnPoint,
                    _enemyUnitSpacing,
                    _enemyGroupSpacing
                );
        }


        // ============================================================
        // Ally Spawn
        // ============================================================

        public Unit_GroupAI SpawnAllyGroup(
            UnitType unitType,
            Vector2 spawnPosition,
            int count,
            Vector2 rallyPoint)
        {
            if (_allyGroupSpawner == null)
            {
                Debug.LogError(
                    "[SpawnManager] " +
                    "AllyGroupSpawner가 초기화되지 않았습니다."
                );

                return null;
            }


            return _allyGroupSpawner.SpawnGroup(
                unitType,
                spawnPosition,
                count,
                rallyPoint
            );
        }


        // ============================================================
        // Enemy Spawn
        // ============================================================

        public void SpawnEnemyWave(
            IReadOnlyList<EnemySpawnRequest> requests)
        {
            if (_enemyWaveSpawner == null)
            {
                Debug.LogError(
                    "[SpawnManager] " +
                    "EnemyWaveSpawner가 초기화되지 않았습니다."
                );

                return;
            }


            _enemyWaveSpawner.SpawnWave(
                requests
            );
        }
    }


    // ================================================================
    // Enemy Spawn Request
    // ================================================================

    [Serializable]
    public class EnemySpawnRequest
    {
        [SerializeField]
        private UnitType _unitType;

        [SerializeField]
        private int _count;


        public UnitType UnitType
            => _unitType;

        public int Count
            => _count;
    }
}