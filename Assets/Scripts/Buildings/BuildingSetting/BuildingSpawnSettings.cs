// Current date KDH 2026-09-11
// BuildingData에 붙여 넣는 소환 설정. 살아있는 유닛 목록은 실물 모듈이 가집니다.
using System;
using UnityEngine;

namespace OZGL.KDH
{
    [Serializable]
    public class BuildingSpawnSettings
    {
        public bool enabled;
        [Tooltip("유닛 담당 프리팹. 나중에 UnitData SO로 바꿔도 이 슬롯만 교체하면 됩니다.")]
        public GameObject unitPrefab;
        [Min(1)] public int countPerWave = 1;
        [Min(1)] public int maxAlive = 3;
    }
}
