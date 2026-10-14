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


        // ============================================================
        // Constructor
        // ============================================================

        public EnemySpawnEntry(
            EnemyUnitFaction faction,
            EnemyUnitClass unitClass,
            GameObject prefab)
        {
            Faction = faction;

            UnitClass = unitClass;

            Prefab = prefab;
        }
    }
}
