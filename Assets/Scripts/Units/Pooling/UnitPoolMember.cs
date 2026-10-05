using UnityEngine;

namespace Units
{
    // 인스턴스의 보관 위치와 대여 수명을 기록한다.
    [DisallowMultipleComponent]
    public sealed class UnitPoolMember : MonoBehaviour
    {
        // ============================================================
        // Properties
        // ============================================================

        public UnitPoolManager Owner { get; internal set; }
        public long LastSpawnGeneration { get; internal set; }
        public long LeaseVersion { get; internal set; }
        public bool IsLeased { get; internal set; }
        internal UnitPoolBucket Bucket { get; set; }

        // Runtime 등록 해제는 유닛 반환 경로에서 처리한다.
        internal RuntimeUnitManager RuntimeManager { get; set; }
    }
}
