using UnityEngine;

namespace Units
{
    internal static class CombatTargetUtility
    {
        // ============================================================
        // Validation
        // ============================================================

        internal static bool Exists(
            ICombatTarget target)
        {
            if (target == null)
                return false;

            // 인터페이스 참조는 Unity의 파괴된 Object 여부를 별도로 확인한다.
            if (target is Object unityObject &&
                unityObject == null)
            {
                return false;
            }

            return true;
        }


        internal static bool IsValid(
            ICombatTarget target)
        {
            return Exists(
                       target)
                   && target.Transform != null
                   && target.IsTargetable;
        }
    }
}