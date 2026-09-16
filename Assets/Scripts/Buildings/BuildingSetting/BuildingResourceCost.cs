// Current date KDH 2026-09-08
// struct로 두어 건설비 배열을 순회해도 클래스처럼 힙 할당이 나지 않습니다.
using System;
using UnityEngine;

namespace OZGL.KDH
{
    [Serializable]
    public struct BuildingResourceCost
    {
        public BuildingResourceType type;
        [Min(0)] public int amount;
    }
}
