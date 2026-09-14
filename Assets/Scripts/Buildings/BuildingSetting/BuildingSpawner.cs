// Current date KDH 2026-09-14
// 병영 앞에서 소환하고, 집결 좌표만 SpawnManager에 넘깁니다. 이동은 유닛이 합니다.
// 건설은 Preparation에서만 하고, 유닛은 BattlePreparing 진입 때 소환합니다.
using Game.Core;
using UnityEngine;
using Units;

namespace OZGL.KDH
{
    public class BuildingSpawner : MonoBehaviour, IBuildingModule
    {
        private Building _owner;
        private GameFlowController _gameFlow;
        private SpawnManager _spawnManager;
        private GamePhase _previousPhase = GamePhase.None;
        private bool _setup;

        public void Setup(Building owner)
        {
            Teardown();

            _owner = owner;
            if (_owner == null || _owner.Data == null)
            {
                Debug.LogWarning("[BuildingSpawner] Setup에 BuildingData가 없습니다.", this);
                return;
            }

            if (!_owner.Data.HasSpawn)
                return;

            if (_owner.Data.Spawn.unitType == AllyUnitType.Default)
            {
                Debug.LogWarning("[BuildingSpawner] unitType이 Default라 소환할 수 없습니다.", this);
                return;
            }

            _gameFlow = FindFirstObjectByType<GameFlowController>();
            if (_gameFlow == null)
            {
                Debug.LogWarning("[BuildingSpawner] GameFlowController를 찾지 못해 소환 시점을 알 수 없습니다.", this);
                return;
            }

            _spawnManager = FindFirstObjectByType<SpawnManager>();
            if (_spawnManager == null)
            {
                Debug.LogWarning("[BuildingSpawner] SpawnManager를 찾지 못해 소환할 수 없습니다.", this);
                return;
            }

            _previousPhase = _gameFlow.CurPhase;
            _gameFlow.PhaseChanged += OnPhaseChanged;
            _setup = true;
        }

        public void Teardown()
        {
            if (_setup && _gameFlow != null)
                _gameFlow.PhaseChanged -= OnPhaseChanged;

            _setup = false;
            _owner = null;
            _gameFlow = null;
            _spawnManager = null;
            _previousPhase = GamePhase.None;
        }

        private void OnDestroy()
        {
            Teardown();
        }

        // Current date KDH 2026-09-14
        // 준비 단계에서는 짓기만 하고, 전투준비로 넘어갈 때 한 번 소환합니다.
        private void OnPhaseChanged(GamePhase phase)
        {
            bool enterBattlePrep = phase == GamePhase.BattlePreparing
                && _previousPhase != GamePhase.BattlePreparing;

            _previousPhase = phase;
            if (!enterBattlePrep)
                return;

            SpawnUnits();
        }

        private void SpawnUnits()
        {
            if (_owner == null || _owner.Data == null || !_owner.Data.HasSpawn)
                return;

            BuildingSpawnSettings settings = _owner.Data.Spawn;
            if (settings.unitType == AllyUnitType.Default)
            {
                Debug.LogWarning("[BuildingSpawner] unitType이 Default라 소환을 건너뜁니다.", this);
                return;
            }

            if (_spawnManager == null)
            {
                Debug.LogWarning("[BuildingSpawner] SpawnManager가 없어 소환을 건너뜁니다.", this);
                return;
            }

            int count = settings.countPerWave;
            if (count > settings.maxAlive)
                count = settings.maxAlive;

            if (count <= 0)
                return;

            Vector2 spawnPosition = _owner.SpawnWorldPosition;
            Vector2 rallyPosition = _owner.RallyWorldPosition;

            _spawnManager.SpawnAllyGroup(
                settings.unitType,
                spawnPosition,
                count,
                rallyPosition);
        }
    }
}
