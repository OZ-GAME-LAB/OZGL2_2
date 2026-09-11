using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;


namespace Units
{
    public interface ISpawnManager
    {
        // ============================================================
        // Events
        // ============================================================

        event Action AllySpawnCompleted;

        event Action EnemySpawnCompleted;


        // ============================================================
        // Ally Spawn
        // ============================================================

        void SpawnAllyGroup(
            AllyUnitType unitType,
            Vector2 spawnPosition,
            int count,
            Vector2 rallyPoint
        );


        // ============================================================
        // Enemy Spawn
        // ============================================================

        UniTask SpawnEnemyWaveAsync(
            IReadOnlyList<EnemySpawnRequest> requests
        );


        // ============================================================
        // Prefab
        // ============================================================

        bool TryGetAllyPrefab(
            AllyUnitType unitType,
            out GameObject prefab
        );

        bool TryGetEnemyPrefab(
            EnemyUnitType unitType,
            out GameObject prefab
        );
    }
}