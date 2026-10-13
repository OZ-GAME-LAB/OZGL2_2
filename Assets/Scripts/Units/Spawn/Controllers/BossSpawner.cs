using System;
using System.Collections.Generic;

namespace Units
{
    // Plans explicit single-member boss groups. Execution uses the shared enemy
    // group spawner so pooling, initialization and cancellation have one owner.
    internal sealed class BossSpawner
    {
        internal List<EnemyUnitType> BuildPlan(IReadOnlyList<EnemyUnitType> ids,
            IReadOnlyDictionary<EnemyUnitType, EnemySpawnEntry> entries)
        {
            var result = new List<EnemyUnitType>();
            if (ids == null) return result;
            foreach (var id in ids)
            {
                if (!entries.TryGetValue(id, out var entry) || entry.Prefab == null ||
                    entry.Rank != EnemyRank.Boss)
                    throw new ArgumentException($"[BossSpawner] 등록된 Boss 프리팹이 아닙니다: {id}");
                result.Add(id);
            }
            return result;
        }
    }
}
