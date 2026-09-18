using UnityEngine;

namespace Units
{
    internal static class CombatTargetUtility
    {
        // ============================================================
        // Validation
        // ============================================================

        internal static bool IsValid(
            ICombatTarget target)
        {
            // 인터페이스 참조는 Unity의 파괴된 Object 여부를 별도로 확인한다.
            return target != null && !(target is Object unityObject && unityObject == null) && target.Transform != null && target.IsTargetable;
        }
    }
}
