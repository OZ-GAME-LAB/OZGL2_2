using Cysharp.Threading.Tasks;
using Game.Core;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;


namespace Units
{
    public class UnitSpawnTest : MonoBehaviour
    {
        // ============================================================
        // References
        // ============================================================

        [SerializeField]
        private SpawnManager _spawnManager;

        [SerializeField]
        private RuntimeUnitManager _runtimeUnitManager;


        // ============================================================
        // Ally Group 1
        // ============================================================

        [Header("Ally Group 1")]

        [SerializeField]
        private AllyUnitType _allyUnitType1;

        [SerializeField]
        private int _allyCount1 = 5;

        [SerializeField]
        private Transform _allySpawnPosition1;

        [SerializeField]
        private Vector2 _allyRallyPoint1;


        // ============================================================
        // Ally Group 2
        // ============================================================

        [Header("Ally Group 2")]

        [SerializeField]
        private AllyUnitType _allyUnitType2;

        [SerializeField]
        private int _allyCount2 = 5;

        [SerializeField]
        private Transform _allySpawnPosition2;

        [SerializeField]
        private Vector2 _allyRallyPoint2;


        // ============================================================
        // Ally Group 3
        // ============================================================

        [Header("Ally Group 3")]

        [SerializeField]
        private AllyUnitType _allyUnitType3;

        [SerializeField]
        private int _allyCount3 = 5;

        [SerializeField]
        private Transform _allySpawnPosition3;

        [SerializeField]
        private Vector2 _allyRallyPoint3;


        // ============================================================
        // Ally Group 4
        // ============================================================

        [Header("Ally Group 4")]

        [SerializeField]
        private AllyUnitType _allyUnitType4;

        [SerializeField]
        private int _allyCount4 = 5;

        [SerializeField]
        private Transform _allySpawnPosition4;

        [SerializeField]
        private Vector2 _allyRallyPoint4;


        // ============================================================
        // Enemy Wave
        // ============================================================

        [Header("Enemy Wave")]

        [SerializeField]
        private int _enemyCost = 20;


        [Header("Enemy Class Weights")]

        [SerializeField]
        private ClassWeights _enemyClassWeights;


        [Header("Enemy Unit Types")]

        [SerializeField]
        private List<EnemyUnitType> _enemyUnitTypes =
            new List<EnemyUnitType>();


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void OnEnable()
        {
            if (_runtimeUnitManager == null)
                return;


            _runtimeUnitManager.PreparationCompleted +=
                OnPreparationCompleted;
        }


        private void Start()
        {
            SpawnAll();
        }


        private void OnDisable()
        {
            if (_runtimeUnitManager == null)
                return;


            _runtimeUnitManager.PreparationCompleted -=
                OnPreparationCompleted;
        }


        // ============================================================
        // Test Spawn
        // ============================================================

        [ContextMenu("Spawn All")]
        private void SpawnAll()
        {
            if (!Validate())
                return;


            SpawnAllies();
            SpawnEnemies();


            Debug.Log(
                "[UnitSpawnTest] " +
                "전체 Spawn 요청 완료."
            );
        }


        // ============================================================
        // Ally Spawn
        // ============================================================

        [ContextMenu("Spawn Allies")]
        private void SpawnAllies()
        {
            if (_spawnManager == null)
            {
                Debug.LogError(
                    "[UnitSpawnTest] SpawnManager가 없습니다."
                );

                return;
            }


            _spawnManager.SpawnAllyGroup(
                _allyUnitType1,
                _allySpawnPosition1.position,
                _allyCount1,
                _allyRallyPoint1
            );


            _spawnManager.SpawnAllyGroup(
                _allyUnitType2,
                _allySpawnPosition2.position,
                _allyCount2,
                _allyRallyPoint2
            );


            _spawnManager.SpawnAllyGroup(
                _allyUnitType3,
                _allySpawnPosition3.position,
                _allyCount3,
                _allyRallyPoint3
            );


            _spawnManager.SpawnAllyGroup(
                _allyUnitType4,
                _allySpawnPosition4.position,
                _allyCount4,
                _allyRallyPoint4
            );


            Debug.Log(
                "[UnitSpawnTest] " +
                "아군 4개 그룹 Spawn 요청 완료."
            );
        }


        // ============================================================
        // Enemy Spawn
        // ============================================================

        [ContextMenu("Spawn Enemies")]
        private void SpawnEnemies()
        {
            if (_spawnManager == null)
            {
                Debug.LogError(
                    "[UnitSpawnTest] SpawnManager가 없습니다."
                );

                return;
            }


            SpawnContext context =
                new SpawnContext(
                    _enemyClassWeights,
                    _enemyUnitTypes
                );


            CancellationToken cancellationToken =
                this.GetCancellationTokenOnDestroy();


            _spawnManager.SpawnEnemyWaveAsync(
                _enemyCost,
                context,
                cancellationToken
            ).Forget();


            Debug.Log(
                "[UnitSpawnTest] " +
                $"적군 Wave Spawn 요청 완료. " +
                $"Cost={_enemyCost}"
            );
        }


        // ============================================================
        // Preparation Completed
        // ============================================================

        private void OnPreparationCompleted()
        {
            Debug.Log(
                "[UnitSpawnTest] " +
                "모든 Unit Spawn 및 Rally 준비 완료."
            );


            StartBattle();
        }


        // ============================================================
        // Start Battle
        // ============================================================

        [ContextMenu("Start Battle")]
        private void StartBattle()
        {
            if (_runtimeUnitManager == null)
            {
                Debug.LogError(
                    "[UnitSpawnTest] RuntimeUnitManager가 없습니다."
                );

                return;
            }


            _runtimeUnitManager.StartBattlePhase();


            Debug.Log(
                "[UnitSpawnTest] " +
                "전투 시작."
            );
        }


        // ============================================================
        // Reset Battle
        // ============================================================

        [ContextMenu("Reset Battle")]
        private void ResetBattle()
        {
            if (_runtimeUnitManager == null)
            {
                Debug.LogError(
                    "[UnitSpawnTest] RuntimeUnitManager가 없습니다."
                );

                return;
            }


            _runtimeUnitManager.ClearRuntime();


            Debug.Log(
                "[UnitSpawnTest] " +
                "전투 상태 초기화."
            );
        }


        // ============================================================
        // Validation
        // ============================================================

        private bool Validate()
        {
            if (_spawnManager == null)
            {
                Debug.LogError(
                    "[UnitSpawnTest] SpawnManager가 없습니다."
                );

                return false;
            }


            if (_runtimeUnitManager == null)
            {
                Debug.LogError(
                    "[UnitSpawnTest] RuntimeUnitManager가 없습니다."
                );

                return false;
            }


            if (_allySpawnPosition1 == null)
            {
                Debug.LogError(
                    "[UnitSpawnTest] Ally Group 1 Spawn Position이 없습니다."
                );

                return false;
            }


            if (_allySpawnPosition2 == null)
            {
                Debug.LogError(
                    "[UnitSpawnTest] Ally Group 2 Spawn Position이 없습니다."
                );

                return false;
            }


            if (_allySpawnPosition3 == null)
            {
                Debug.LogError(
                    "[UnitSpawnTest] Ally Group 3 Spawn Position이 없습니다."
                );

                return false;
            }


            if (_allySpawnPosition4 == null)
            {
                Debug.LogError(
                    "[UnitSpawnTest] Ally Group 4 Spawn Position이 없습니다."
                );

                return false;
            }


            if (_allyCount1 <= 0)
            {
                Debug.LogError(
                    "[UnitSpawnTest] Ally Group 1 Count가 올바르지 않습니다."
                );

                return false;
            }


            if (_allyCount2 <= 0)
            {
                Debug.LogError(
                    "[UnitSpawnTest] Ally Group 2 Count가 올바르지 않습니다."
                );

                return false;
            }


            if (_allyCount3 <= 0)
            {
                Debug.LogError(
                    "[UnitSpawnTest] Ally Group 3 Count가 올바르지 않습니다."
                );

                return false;
            }


            if (_allyCount4 <= 0)
            {
                Debug.LogError(
                    "[UnitSpawnTest] Ally Group 4 Count가 올바르지 않습니다."
                );

                return false;
            }


            if (_enemyCost <= 0)
            {
                Debug.LogError(
                    "[UnitSpawnTest] Enemy Cost가 올바르지 않습니다."
                );

                return false;
            }


            if (_enemyUnitTypes == null ||
                _enemyUnitTypes.Count == 0)
            {
                Debug.LogError(
                    "[UnitSpawnTest] Enemy Unit Type이 설정되지 않았습니다."
                );

                return false;
            }


            return true;
        }
    }
}