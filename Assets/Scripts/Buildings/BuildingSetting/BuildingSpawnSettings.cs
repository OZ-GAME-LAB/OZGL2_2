// Current date KDH 2026-09-15
// BuildingData에 붙여 넣는 소환 설정. 월드 좌표는 여기에 두지 않습니다.
// 실제 소환은 unitType만 SpawnManager에 넘깁니다. 프리팹은 유닛 쪽이 고릅니다.
using System;
using UnityEngine;
using Units;

namespace OZGL.KDH
{
    [Serializable]
    public class BuildingSpawnSettings
    {
        public bool enabled;
        [Tooltip("SpawnManager가 프리팹을 고를 때 사용합니다.")]
        public AllyUnitType unitType = AllyUnitType.Warrior;
        [Min(1)] public int countPerWave = 1;
        [Min(1)] public int maxAlive = 3;
    }
}
