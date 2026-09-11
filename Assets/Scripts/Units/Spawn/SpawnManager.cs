using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;



namespace Units
{
    public class SpawnManager : MonoBehaviour, ISpawnManager
    {
        // ============================================================
        // References
        // ============================================================

        [SerializeField]
        private RuntimeUnitManager _runtimeUnitManager;

        [SerializeField]
        private UnitStatModifierManager _unitStatModifierManager;

        [SerializeField]
        private AllyUnitSpawnDatabaseSO _allyUnitDatabase;

        [SerializeField]
        private EnemyUnitSpawnDatabaseSO _enemyUnitDatabase;

        [SerializeField]
        private GameObject _groupPrefab;


        // ============================================================
        // Spawn Settings
        // ============================================================

        [Header("Spawn Timing")]

        [SerializeField]
        private float _groupSpawnDuration =
            2f;


        // ============================================================
        // Rally Settings
        // ============================================================

        [Header("Rally")]

        [SerializeField]
        private Vector2 _rallySectorSize =
            new Vector2(5f, 10f);

        [SerializeField]
        private float _rallyFormationSpacing =
            2f;


        // ============================================================
        // Ally Rally Settings
        // ============================================================

        [Header("Ally Rally")]

        [SerializeField]
        private Vector2 _allyRallyAreaMin;

        [SerializeField]
        private Vector2 _allyRallyAreaMax;


        // ============================================================
        // Enemy Spawn Settings
        // ============================================================

        [Header("Enemy Spawn")]

        [SerializeField]
        private List<Transform> _enemySpawnPoints =
            new List<Transform>();


        // ============================================================
        // Enemy Rally Settings
        // ============================================================

        [Header("Enemy Rally")]

        [SerializeField]
        private Vector2 _enemyRallyAreaMin;

        [SerializeField]
        private Vector2 _enemyRallyAreaMax;


        // ============================================================
        // Prefab Maps
        //
        // SpawnManager가 실제 Dictionary 원본을 소유한다.
        // ============================================================

        private readonly Dictionary<AllyUnitType, GameObject>
            _allyUnitPrefabs =
                new Dictionary<AllyUnitType, GameObject>();

        private readonly Dictionary<EnemyUnitType, GameObject>
            _enemyUnitPrefabs =
                new Dictionary<EnemyUnitType, GameObject>();


        // ============================================================
        // Spawn State
        // ============================================================

        private int
            _activeAllySpawnCount;


        // ============================================================
        // Internal Controllers
        // ============================================================

        private UnitPrefabMapper
            _prefabMapper;

        private RallyGridAllocator
            _allyRallyGridAllocator;

        private RallyGridAllocator
            _enemyRallyGridAllocator;

        private AllyGroupSpawner
            _allyGroupSpawner;

        private EnemyWaveSpawner
            _enemyWaveSpawner;


        // ============================================================
        // Events
        // ============================================================

        public event Action
            AllySpawnCompleted;

        public event Action
            EnemySpawnCompleted;


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Awake()
        {
            if (!ValidateReferences())
                return;


            InitializePrefabMapper();
            InitializeRallyGridAllocators();
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


        private void InitializeRallyGridAllocators()
        {
            _allyRallyGridAllocator =
                new RallyGridAllocator(
                    _allyRallyAreaMin,
                    _allyRallyAreaMax,
                    _rallySectorSize,
                    _rallyFormationSpacing
                );


            _enemyRallyGridAllocator =
                new RallyGridAllocator(
                    _enemyRallyAreaMin,
                    _enemyRallyAreaMax,
                    _rallySectorSize,
                    _rallyFormationSpacing
                );
        }


        private void InitializeSpawners()
        {
            _allyGroupSpawner =
                new AllyGroupSpawner(
                    _runtimeUnitManager,
                    _unitStatModifierManager,
                    _allyUnitPrefabs,
                    _groupPrefab,
                    _allyRallyGridAllocator,
                    _groupSpawnDuration
                );


            _enemyWaveSpawner =
                new EnemyWaveSpawner(
                    _runtimeUnitManager,
                    _unitStatModifierManager,
                    _enemyUnitPrefabs,
                    _groupPrefab,
                    _enemySpawnPoints,
                    _enemyRallyGridAllocator,
                    _groupSpawnDuration
                );
        }


        // ============================================================
        // Prefab
        // ============================================================

        public bool TryGetAllyPrefab(
            AllyUnitType unitType,
            out GameObject prefab)
        {
            return _allyUnitPrefabs.TryGetValue(
                unitType,
                out prefab
            );
        }


        public bool TryGetEnemyPrefab(
            EnemyUnitType unitType,
            out GameObject prefab)
        {
            return _enemyUnitPrefabs.TryGetValue(
                unitType,
                out prefab
            );
        }


        // ============================================================
        // Ally Spawn
        // ============================================================

        public void SpawnAllyGroup(
            AllyUnitType unitType,
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

                return;
            }


            _activeAllySpawnCount++;


            SpawnAllyGroupAsync(
                unitType,
                spawnPosition,
                count,
                rallyPoint
            ).Forget();
        }


        private async UniTaskVoid SpawnAllyGroupAsync(
            AllyUnitType unitType,
            Vector2 spawnPosition,
            int count,
            Vector2 rallyPoint)
        {
            try
            {
                await _allyGroupSpawner.SpawnGroupAsync(
                    unitType,
                    spawnPosition,
                    count,
                    rallyPoint,
                    this.GetCancellationTokenOnDestroy()
                );
            }
            finally
            {
                _activeAllySpawnCount--;

                if (_activeAllySpawnCount == 0)
                {
                    Debug.Log(
                        "[SpawnManager] " +
                        "모든 Ally Spawn 완료."
                    );

                    _runtimeUnitManager.NotifyAllySpawnCompleted();

                    _allyRallyGridAllocator.Clear();

                    AllySpawnCompleted?.Invoke();
                }
            }
        }


        // ============================================================
        // Enemy Spawn
        // ============================================================

        public async UniTask SpawnEnemyWaveAsync(
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


            await _enemyWaveSpawner.SpawnWaveAsync(
                requests,
                this.GetCancellationTokenOnDestroy()
            );


            Debug.Log(
                "[SpawnManager] " +
                "모든 Enemy Spawn 완료."
            );

            _runtimeUnitManager.NotifyEnemySpawnCompleted();

            _enemyRallyGridAllocator.Clear();

            EnemySpawnCompleted?.Invoke();
        }


        // ============================================================
        // Validation
        // ============================================================

        private bool ValidateReferences()
        {
            bool isValid =
                true;


            if (_runtimeUnitManager == null)
            {
                Debug.LogError(
                    "[SpawnManager] " +
                    "RuntimeUnitManager가 없습니다."
                );

                isValid =
                    false;
            }


            if (_unitStatModifierManager == null)
            {
                Debug.LogError(
                    "[SpawnManager] " +
                    "UnitStatModifierManager가 없습니다."
                );

                isValid =
                    false;
            }


            if (_allyUnitDatabase == null)
            {
                Debug.LogError(
                    "[SpawnManager] " +
                    "AllyUnitSpawnDatabase가 없습니다."
                );

                isValid =
                    false;
            }


            if (_enemyUnitDatabase == null)
            {
                Debug.LogError(
                    "[SpawnManager] " +
                    "EnemyUnitSpawnDatabase가 없습니다."
                );

                isValid =
                    false;
            }


            if (_groupPrefab == null)
            {
                Debug.LogError(
                    "[SpawnManager] " +
                    "GroupPrefab이 없습니다."
                );

                isValid =
                    false;
            }


            if (_groupSpawnDuration < 0f)
            {
                Debug.LogError(
                    "[SpawnManager] " +
                    "Group Spawn Duration은 " +
                    "0보다 작을 수 없습니다."
                );

                isValid =
                    false;
            }


            if (_rallySectorSize.x <= 0f ||
                _rallySectorSize.y <= 0f)
            {
                Debug.LogError(
                    "[SpawnManager] " +
                    "Rally Sector Size는 " +
                    "0보다 커야 합니다."
                );

                isValid =
                    false;
            }


            if (_rallyFormationSpacing <= 0f)
            {
                Debug.LogError(
                    "[SpawnManager] " +
                    "Rally Formation Spacing은 " +
                    "0보다 커야 합니다."
                );

                isValid =
                    false;
            }


            if (_allyRallyAreaMax.x <=
                    _allyRallyAreaMin.x ||
                _allyRallyAreaMax.y <=
                    _allyRallyAreaMin.y)
            {
                Debug.LogError(
                    "[SpawnManager] " +
                    "Ally Rally Area가 올바르지 않습니다."
                );

                isValid =
                    false;
            }


            if (_enemyRallyAreaMax.x <=
                    _enemyRallyAreaMin.x ||
                _enemyRallyAreaMax.y <=
                    _enemyRallyAreaMin.y)
            {
                Debug.LogError(
                    "[SpawnManager] " +
                    "Enemy Rally Area가 올바르지 않습니다."
                );

                isValid =
                    false;
            }


            if (_enemySpawnPoints == null ||
                _enemySpawnPoints.Count == 0)
            {
                Debug.LogError(
                    "[SpawnManager] " +
                    "Enemy SpawnPoint가 없습니다."
                );

                isValid =
                    false;
            }


            for (int i = 0;
                 i < _enemySpawnPoints.Count;
                 i++)
            {
                if (_enemySpawnPoints[i] != null)
                    continue;


                Debug.LogError(
                    $"[SpawnManager] " +
                    $"Enemy SpawnPoint가 null입니다. " +
                    $"Index={i}"
                );


                isValid =
                    false;
            }


            return isValid;
        }
    }
}