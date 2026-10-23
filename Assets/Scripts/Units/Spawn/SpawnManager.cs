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
        private UnitPoolManager _unitPoolManager;

        private long _allySpawnGeneration;
        private bool _allySpawnFailed;
        private bool _enemySpawning;
        private const double SpawnRequestDelaySeconds = 0.2;
        private double _allySpawnStartsAt;
        private CancellationTokenSource _allySpawnCancellation;

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
        // Test Access
        // ============================================================

        // 테스트 등록도 실제 스폰에 사용하는 매니저로 전달한다.
        internal UnitStatModifierManager StatModifierManager => _unitStatModifierManager;


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
        private RallySector _rallySectorPrefab;

        [SerializeField]
        private Vector2 _rallySectorSize =
            new Vector2(
                5f,
                5f
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

        [SerializeField]
        private Transform _allyRallySectorRoot;

        [Tooltip("아군이 최종 Rally 자리로 이동하기 전에 통과할 좌표. 미지정 시 직접 이동합니다.")]
        [SerializeField]
        private Transform _allyRallyViaPoint;


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

        [SerializeField]
        private Transform _enemyRallySectorRoot;


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


            if (_unitPoolManager == null)
            {
                var poolObject = new GameObject("UnitPoolManager");
                poolObject.transform.SetParent(transform, false);
                _unitPoolManager = poolObject.AddComponent<UnitPoolManager>();
            }

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
                    _rallyFormationSpacing,
                    _rallySectorPrefab,
                    _allyRallySectorRoot
                );

            _enemyRallyGridAllocator =
                new RallyGridAllocator(
                    _enemyRallyAreaPointA,
                    _enemyRallyAreaPointB,
                    _rallySectorSize,
                    _rallyFormationSpacing,
                    _rallySectorPrefab,
                    _enemyRallySectorRoot
                );
        }


        private void InitializeSpawners()
        {
            _allyGroupSpawner =
                new AllyGroupSpawner(
                    _runtimeUnitManager,
                    _unitPoolManager,
                    _unitStatModifierManager,
                    _allySpawnEntries,
                    _groupPrefab,
                    _allyRallyGridAllocator,
                    _groupSpawnDuration,
                    _allyRallyViaPoint
                );


            _enemyWaveSpawner =
                new EnemyWaveSpawner(
                    _runtimeUnitManager,
                    _unitPoolManager,
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


            // ========================================================
            // Rally Allocation Reset
            //
            // 이전 Ally Spawn Cycle에서 사용한 Sector 할당은
            // 다음 Spawn Cycle이 시작되는 시점에 초기화한다.
            //
            // 같은 Spawn Cycle의 여러 Group Spawn 요청은
            // 짧은 시간 안에 연속으로 들어오므로
            // 첫 번째 요청에서만 초기화한다.
            // ========================================================

            if (_activeAllySpawnCount > 0 && _allySpawnCancellation.IsCancellationRequested)
            {
                Debug.LogWarning("[SpawnManager] 이전 아군 스폰 취소를 정리 중입니다.");
                return;
            }

            if (_activeAllySpawnCount == 0)
            {
                _allySpawnStartsAt = Time.realtimeSinceStartupAsDouble + SpawnRequestDelaySeconds;
                // 첫 요청에서만 아군 아티펙트 스탯·패시브를 최신화한다.
                // 참조가 없는 독립 테스트 씬은 기존 등록을 보존한 채 스폰한다.
                _unitStatModifierManager.RefreshAllyArtifactEffects();

                _allySpawnGeneration = _unitPoolManager.BeginSpawn(UnitTeam.Ally);
                _allySpawnFailed = false;
                _allyGroupSpawner.ResetSpawnResult();
                _allyRallyGridAllocator.Clear();
                _allySpawnCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    this.GetCancellationTokenOnDestroy(), _runtimeUnitManager.PreparationToken);
            }


            _activeAllySpawnCount++;


            SpawnAllyGroupAsync(
                unitType,
                spawnPosition,
                count,
                rallyPoint,
                _allySpawnCancellation.Token
            ).Forget();
        }


        private static async UniTask WaitForSpawnStartAsync(double startsAt,
            CancellationToken cancellationToken)
        {
            // 절대 시각을 검사하여 요청 프레임의 deltaTime 때문에 일찍 시작하지 않는다.
            while (Time.realtimeSinceStartupAsDouble < startsAt)
                await UniTask.NextFrame(cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        private async UniTaskVoid SpawnAllyGroupAsync(
            AllyUnitType unitType,
            Vector2 spawnPosition,
            int count,
            Vector2 rallyPoint,
            CancellationToken cancellationToken)
        {
            try
            {
                // 첫 요청의 마감 시간을 공유한다. 후속 요청으로 0.2초를 다시 시작하지 않는다.
                // 대기 중인 요청도 카운트에 포함하므로 즉시 끝나는 1인 그룹도 조기 완료되지 않는다.
                await WaitForSpawnStartAsync(_allySpawnStartsAt, cancellationToken);

                var group = await _allyGroupSpawner.SpawnGroupAsync(
                    unitType,
                    spawnPosition,
                    count,
                    rallyPoint,
                    cancellationToken
                );
                cancellationToken.ThrowIfCancellationRequested();
                if (group == null)
                    _allySpawnFailed = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // 리셋/파괴는 정상 취소다. 완료 이벤트를 보내지 않는다.
                _allySpawnFailed = true;
            }
            catch
            {
                _allySpawnFailed = true;
                throw;
            }
            finally
            {
                _activeAllySpawnCount--;

                if (_activeAllySpawnCount == 0)
                {
                    bool succeeded = !_allySpawnFailed && !_allyGroupSpawner.SpawnFailed &&
                        !cancellationToken.IsCancellationRequested;
                    _allySpawnCancellation.Dispose();
                    _allySpawnCancellation = null;
                    if (_unitPoolManager != null)
                        _unitPoolManager.EndSpawn(UnitTeam.Ally, _allySpawnGeneration, succeeded);
                    if (succeeded)
                    {
                        Debug.Log(
                            "[SpawnManager] " +
                            "모든 Ally Spawn 완료."
                        );

                        _runtimeUnitManager.NotifyAllySpawnCompleted();

                        AllySpawnCompleted?.Invoke();
                    }
                }
            }
        }


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


            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _runtimeUnitManager.PreparationToken,
                this.GetCancellationTokenOnDestroy());
            cancellationToken = linkedCancellation.Token;
            cancellationToken.ThrowIfCancellationRequested();


            // ========================================================
            // Rally Allocation Reset
            //
            // Enemy는 SpawnEnemyWaveAsync 한 번이
            // 하나의 Spawn Cycle 전체를 담당한다.
            //
            // 따라서 새로운 Wave Spawn이 시작되는 시점에
            // 이전 Wave의 Sector 할당을 초기화한다.
            // Sector 객체 자체는 유지된다.
            // ========================================================

            if (_enemySpawning)
                throw new InvalidOperationException("[SpawnManager] 적 스폰이 이미 진행 중입니다.");

            double enemySpawnStartsAt = Time.realtimeSinceStartupAsDouble + SpawnRequestDelaySeconds;
            _runtimeUnitManager.BeginEnemySpawnCycle();

            // 적 웨이브 시작 시 적 아티펙트 스탯·패시브만 한 번 최신화한다.
            _unitStatModifierManager.RefreshEnemyArtifactEffects();

            long generation = _unitPoolManager.BeginSpawn(UnitTeam.Enemy);
            _enemySpawning = true;
            bool succeeded = false;
            try
            {
                await WaitForSpawnStartAsync(enemySpawnStartsAt, cancellationToken);
                _enemyRallyGridAllocator.Clear();
                _enemyWaveSpawner.ResetSpawnResult();
                succeeded = await _enemyWaveSpawner.SpawnWaveAsync(cost, context, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch
            {
                succeeded = false;
                throw;
            }
            finally
            {
                _enemySpawning = false;
                if (_unitPoolManager != null)
                    _unitPoolManager.EndSpawn(UnitTeam.Enemy, generation, succeeded);
            }

            if (!succeeded)
                throw new InvalidOperationException("[SpawnManager] 적 스폰 실패. 생성한 그룹과 집결 배정을 정리했습니다.");

            if (succeeded)
            {
                _runtimeUnitManager.NotifyEnemySpawnCompleted();
                EnemySpawnCompleted?.Invoke();
            }
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


            if (_rallySectorPrefab == null)
            {
                Debug.LogError(
                    "[SpawnManager] " +
                    "Rally Sector Prefab이 없습니다."
                );

                isValid =
                    false;
            }


            if (_allyRallySectorRoot == null)
            {
                Debug.LogError(
                    "[SpawnManager] " +
                    "Ally Rally Sector Root가 없습니다."
                );

                isValid =
                    false;
            }


            if (_enemyRallySectorRoot == null)
            {
                Debug.LogError(
                    "[SpawnManager] " +
                    "Enemy Rally Sector Root가 없습니다."
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
