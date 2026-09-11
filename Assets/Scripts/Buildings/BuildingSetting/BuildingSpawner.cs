// Current date KDH 2026-09-11
// 웨이브가 끝날 때 아군 유닛을 소환합니다. 죽은 유닛은 Unity 가짜 null로만 정리합니다.
using System.Collections.Generic;
using UnityEngine;

namespace OZGL.KDH
{
    public class BuildingSpawner : MonoBehaviour, IBuildingModule
    {
        private Building _owner;
        private Transform _spawnPoint;
        private readonly List<Transform> _alive = new List<Transform>(8);
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

            if (_owner.Data.Spawn.unitPrefab == null)
            {
                Debug.LogWarning("[BuildingSpawner] unitPrefab이 없어 소환할 수 없습니다.", this);
                return;
            }

            _spawnPoint = _owner.SpawnPoint != null ? _owner.SpawnPoint : _owner.transform;
            WaveEvents.WaveCleared += OnWaveCleared;
            _setup = true;
        }

        public void Teardown()
        {
            if (_setup)
                WaveEvents.WaveCleared -= OnWaveCleared;

            _setup = false;
            _owner = null;
            _spawnPoint = null;
            _alive.Clear();
        }

        private void OnDestroy()
        {
            Teardown();
        }

        private void OnWaveCleared(int waveIndex)
        {
            if (_owner == null || _owner.Data == null || !_owner.Data.HasSpawn)
                return;

            BuildingSpawnSettings settings = _owner.Data.Spawn;
            if (settings.unitPrefab == null)
            {
                Debug.LogWarning("[BuildingSpawner] unitPrefab이 없어 소환을 건너뜁니다.", this);
                return;
            }

            RemoveDead();

            int toSpawn = settings.countPerWave;
            int room = settings.maxAlive - _alive.Count;
            if (room <= 0)
                return;

            if (toSpawn > room)
                toSpawn = room;

            Vector3 pos = _spawnPoint != null ? _spawnPoint.position : transform.position;
            for (int i = 0; i < toSpawn; i++)
            {
                GameObject unit = Instantiate(settings.unitPrefab, pos, Quaternion.identity);
                if (unit == null)
                {
                    Debug.LogWarning("[BuildingSpawner] 유닛 Instantiate에 실패했습니다.", this);
                    continue;
                }

                _alive.Add(unit.transform);
            }
        }

        private void RemoveDead()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                if (_alive[i] == null)
                    _alive.RemoveAt(i);
            }
        }
    }
}
