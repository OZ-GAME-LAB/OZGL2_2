using UnityEngine;

namespace Units
{
    // UnitData에서 읽어 구성하는 스폰 정보입니다.
    public readonly struct EnemySpawnEntry
    {
        // ============================================================
        // Properties
        // ============================================================

        public EnemyUnitFaction Faction { get; }


        public EnemyUnitClass UnitClass { get; }


        public GameObject Prefab { get; }
        public RallyZone RallyZone { get; }
        public EnemyRank Rank { get; }


        // ============================================================
        // Constructor
        // ============================================================

        public EnemySpawnEntry(
            EnemyUnitFaction faction,
            EnemyUnitClass unitClass,
            GameObject prefab,
            RallyZone rallyZone = RallyZone.Front,
            EnemyRank rank = EnemyRank.Normal)
        {
            Faction = faction;

            UnitClass = unitClass;

            Prefab = prefab;
            RallyZone = rallyZone;
            Rank = rank;
        }
    }
}
