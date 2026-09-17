using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Threading;
using Game.Core;



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
            new Vector2(
                5f,
                10f
            );

        [SerializeField]
        private float _rallyFormationSpacing =
            2f;


        // ============================================================
        // Ally Rally Settings
        // ============================================================

        [Header("Ally Rally")]

        [SerializeField]
        private Transform _allyRallyAreaPointA;

        [SerializeField]
        private Transform _allyRallyAreaPointB;


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
        private Transform _enemyRallyAreaPointA;

        [SerializeField]
        private Transform _enemyRallyAreaPointB;


        // ============================================================
        // Prefab Maps
        //
        // SpawnManager가 실제 Dictionary 원본을 소유한다.
        // ============================================================

        private readonly Dictionary<AllyUnitType, AllySpawnEntry>
            _allySpawnEntries =
                new Dictionary<AllyUnitType, AllySpawnEntry>();

        private readonly Dictionary<EnemyUnitType, EnemySpawnEntry>
            _enemySpawnEntries =
                new Dictionary<EnemyUnitType, EnemySpawnEntry>();


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
                    _allySpawnEntries,
                    _enemySpawnEntries
                );


            _prefabMapper.Initialize();
        }


        private void InitializeRallyGridAllocators()
        {
            _allyRallyGridAllocator =
                new RallyGridAllocator(
                    _allyRallyAreaPointA,
                    _allyRallyAreaPointB,
                    _rallySectorSize,
                    _rallyFormationSpacing
                );

            _enemyRallyGridAllocator =
                new RallyGridAllocator(
                    _enemyRallyAreaPointA,
                    _enemyRallyAreaPointB,
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
                    _allySpawnEntries,
                    _groupPrefab,
                    _allyRallyGridAllocator,
                    _groupSpawnDuration
                );


            _enemyWaveSpawner =
                new EnemyWaveSpawner(
                    _runtimeUnitManager,
                    _unitStatModifierManager,
                    _enemySpawnEntries,
                    _groupPrefab,
                    _enemySpawnPoints,
                    _enemyRallyGridAllocator,
                    _groupSpawnDuration
                );
        }


        // ============================================================
        // Prefab
        // ============================================================

        public bool TryGetAllySpawnEntry(
            AllyUnitType unitType,
            out AllySpawnEntry entry)
        {
            return _allySpawnEntries.TryGetValue(
                unitType,
                out entry
            );
        }

        public bool TryGetEnemySpawnEntry(
            EnemyUnitType unitType,
            out EnemySpawnEntry entry)
        {
            return _enemySpawnEntries.TryGetValue(
                unitType,
                out entry
            );
        }

        public bool TryGetAllyPrefab(
            AllyUnitType unitType,
            out GameObject prefab)
        {
            prefab = null;

            if (!_allySpawnEntries.TryGetValue(
                unitType,
                out var entry
            ))
                return false;

            prefab = entry.Prefab;

            return true;
        }


        public bool TryGetEnemyPrefab(
            EnemyUnitType unitType,
            out GameObject prefab)
        {
            prefab = null;

            if (!_enemySpawnEntries.TryGetValue(
                unitType,
                out var entry
            ))
                return false;

            prefab = entry.Prefab;

            return true;
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

        // ============================================================
        // Enemy Spawn
        // ============================================================

        public async UniTask SpawnEnemyWaveAsync(
            int cost,
            SpawnContext context,
            CancellationToken cancellationToken)
        {
            if (_enemyWaveSpawner == null)
            {
                Debug.LogError(
                    "[SpawnManager] " +
                    "EnemyWaveSpawner가 초기화되지 않았습니다."
                );

                return;
            }


            cancellationToken.ThrowIfCancellationRequested();


            await _enemyWaveSpawner.SpawnWaveAsync(
                cost,
                context,
                cancellationToken
            );


            cancellationToken.ThrowIfCancellationRequested();


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


            if (_allyRallyAreaPointA == null ||
    _allyRallyAreaPointB == null)
            {
                Debug.LogError(
                    "[SpawnManager] " +
                    "Ally Rally Area Point가 없습니다."
                );

                isValid =
                    false;
            }
            else
            {
                Vector2 pointA =
                    _allyRallyAreaPointA.position;

                Vector2 pointB =
                    _allyRallyAreaPointB.position;


                if (Mathf.Approximately(
                        pointA.x,
                        pointB.x
                    ) ||
                    Mathf.Approximately(
                        pointA.y,
                        pointB.y
                    ))
                {
                    Debug.LogError(
                        "[SpawnManager] " +
                        "Ally Rally Area의 크기가 올바르지 않습니다."
                    );

                    isValid =
                        false;
                }
            }


            if (_enemyRallyAreaPointA == null ||
                _enemyRallyAreaPointB == null)
            {
                Debug.LogError(
                    "[SpawnManager] " +
                    "Enemy Rally Area Point가 없습니다."
                );

                isValid =
                    false;
            }
            else
            {
                Vector2 pointA =
                    _enemyRallyAreaPointA.position;

                Vector2 pointB =
                    _enemyRallyAreaPointB.position;


                if (Mathf.Approximately(
                        pointA.x,
                        pointB.x
                    ) ||
                    Mathf.Approximately(
                        pointA.y,
                        pointB.y
                    ))
                {
                    Debug.LogError(
                        "[SpawnManager] " +
                        "Enemy Rally Area의 크기가 올바르지 않습니다."
                    );

                    isValid =
                        false;
                }
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
