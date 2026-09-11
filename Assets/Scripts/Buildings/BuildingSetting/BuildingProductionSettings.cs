// Current date KDH 2026-09-11
// BuildingData에 붙여 넣는 생산 설정. 런타임 누적값은 여기에 두지 않습니다.
using System;
using UnityEngine;

namespace OZGL.KDH
{
    [Serializable]
    public class BuildingProductionSettings
    {
        public bool enabled;
        public BuildingResourceType resourceType = BuildingResourceType.Gold;
        [Min(1)] public int amount = 10;
        public ProductionTrigger trigger = ProductionTrigger.PerWave;
    }
}
