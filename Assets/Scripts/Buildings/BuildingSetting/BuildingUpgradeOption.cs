// Current date KDH 2026-09-16
// 한 건물이 바뀔 수 있는 다음 건물과 비용입니다. 배열 길이가 곧 선택지 개수입니다.
// cost에 Gold와 Gem을 같이 넣으면 둘 다 깎입니다. 마지막 단계만 Gem을 넣으면 됩니다.
using System;
using UnityEngine;

namespace OZGL.KDH
{
    [Serializable]
    public class BuildingUpgradeOption
    {
        [Tooltip("업그레이드 후 이 건물로 바뀝니다.")]
        public BuildingData nextBuilding;
        [Tooltip("Gold만, 또는 Gold와 Gem을 같이 넣습니다. 둘 다 있으면 동시에 소비합니다.")]
        public BuildingResourceCost[] cost;
        [Tooltip("현재 코어 레벨이 이 값 이상이어야 이 업그레이드가 열립니다. 0이면 처음부터 가능합니다.")]
        [Min(0)] public int requiredCoreLevel;
    }
}
