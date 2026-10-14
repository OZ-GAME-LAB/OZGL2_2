using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using OZGL.KDH;
using Units;
using UnityEngine;

namespace Game.UI.Samples
{
    /// <summary>
    /// UI 독립 씬에서 최신 WaveController 계약만 검증하는 전투 테스트 더블.
    /// 실제 RuntimeUnitManager의 전투/보스 로직은 구현하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MvpRuntimeCombatFixture : MonoBehaviour, ISpawnManager, IRuntimeUnitManager
    {
        // =========================
        // Events
        // =========================

        public event Action AllySpawnCompleted;

        public event Action EnemySpawnCompleted;

        public event Action PreparationCompleted;

        public event Action<Unit_Gateway> UnitDied;

        public event Action<UnitTeam> TeamWiped;

        public event Action<Unit_Gateway> BossSpawned;

        public event Action<Unit_Gateway> BossDied;

        public event Action<Unit_Gateway> BossRemoved;

        public event Action AllBossesDefeated;


        // =========================
        // Runtime Unit Info
        // =========================

        public int AllyUnitCount
            => 1;

        public int EnemyUnitCount
            => 1;

        public int BossUnitCount
            => 0;

        public IReadOnlyList<Unit_Gateway> BossUnits
            => Array.Empty<Unit_Gateway>();


        // =========================
        // Core Fixture
        // =========================

        [SerializeField]
        private BuildingCoreProgress _coreProgress;

        [SerializeField]
        private Building _coreFixture;


        // =========================
        // Unity Lifecycle
        // =========================

        private void OnEnable()
        {
            if (_coreProgress != null &&
                _coreFixture != null)
            {
                _coreProgress.Register(
                    _coreFixture
                );
            }
        }


        private void OnDisable()
        {
            if (_coreProgress != null &&
                _coreFixture != null)
            {
                _coreProgress.Unregister(
                    _coreFixture
                );
            }
        }


        // =========================
        // Spawn
        // =========================

        public void SpawnAllyGroup(
            AllyUnitType unitType,
            Vector2 spawnPosition,
            int count,
            Vector2 rallyPoint)
        {
            AllySpawnCompleted?.Invoke();
        }


        public UniTask SpawnEnemyWaveAsync(
            int cost,
            SpawnContext context,
            CancellationToken cancellationToken,
            bool isInfiniteWave = false)
        {
            cancellationToken.ThrowIfCancellationRequested();


            EnemySpawnCompleted?.Invoke();

            PreparationCompleted?.Invoke();


            return UniTask.CompletedTask;
        }


        public bool TryGetAllyPrefab(
            AllyUnitType unitType,
            out GameObject prefab)
        {
            prefab =
                null;


            return false;
        }


        public bool TryGetEnemyPrefab(
            EnemyUnitType unitType,
            out GameObject prefab)
        {
            prefab =
                null;


            return false;
        }


        // =========================
        // Runtime Unit Manager
        // =========================

        public bool GetRemain(
            out int enemy,
            out int allies)
        {
            enemy =
                EnemyUnitCount;

            allies =
                AllyUnitCount;


            return true;
        }


        public void StartBattlePhase()
        {
        }


        public void PauseBattle()
        {
        }


        public void ResumeBattle()
        {
        }


        public void ClearRuntime()
        {
        }


        // =========================
        // Interface Contract
        // =========================

        // UI Fixture에서는 실제 유닛 사망, 전멸,
        // Boss 관련 Runtime 동작을 발생시키지 않는다.
        private void KeepInterfaceEventsReferenced()
        {
            UnitDied?.Invoke(
                null
            );

            TeamWiped?.Invoke(
                default
            );

            BossSpawned?.Invoke(
                null
            );

            BossDied?.Invoke(
                null
            );

            BossRemoved?.Invoke(
                null
            );

            AllBossesDefeated?.Invoke();
        }
    }
}