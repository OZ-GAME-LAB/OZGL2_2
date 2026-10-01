// Current date KDH 2026-10-01
// 건설 상태 저장용 데이터입니다. SO/GameObject 참조 없이 복원에 필요한 최소 값만 담습니다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZGL.KDH
{
    [Serializable]
    public class BuildingSaveData
    {
        public List<BuildingSaveEntry> buildings = new();
    }

    [Serializable]
    public class BuildingSaveEntry
    {
        // BuildingData.BuildingId. 복원 때 BuildingDatabase.GetById로 다시 찾습니다.
        public string buildingId;

        // 어느 칸인지 구분하는 값입니다. 칸의 BuildPosition을 그대로 저장합니다.
        public Vector3 position;
    }
}
