using UnityEngine;

namespace Units
{
    // UnitData에서 읽어 구성하는 스폰 정보입니다.
    public readonly struct AllySpawnEntry
    {
        // ============================================================
        // Properties
        // ============================================================

        public AllyUnitTier Tier { get; }


        public AllyUnitClass UnitClass { get; }


        public GameObject Prefab { get; }

        public RallyZone RallyZone { get; }


        // ============================================================
        // Constructor
        // ============================================================

        public AllySpawnEntry(
            AllyUnitTier tier,
            AllyUnitClass unitClass,
            GameObject prefab,
            RallyZone rallyZone = RallyZone.Front)
        {
            Tier = tier;

            UnitClass = unitClass;

            Prefab = prefab;

            RallyZone = rallyZone;
        }
    }
}
