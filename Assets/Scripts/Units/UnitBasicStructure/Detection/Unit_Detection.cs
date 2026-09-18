using UnityEngine;



namespace Units
{
    public class Unit_Detection : MonoBehaviour
    {
        // ============================================================
        // Detection Settings
        // ============================================================

        [SerializeField]
        private LayerMask _obstacleLayer;


        // ============================================================
        // References
        // ============================================================

        private Unit_Core _core;


        // ============================================================
        // Initialize
        // ============================================================

        public void Initialize(
            Unit_Core core)
        {
            _core = core;

            if (_core == null)
            {
                Debug.LogError(
                    $"[Unit_Detection] {name} : Unit_Core가 없습니다."
                );
            }
        }


        // ============================================================
        // Distance
        // ============================================================

        public float GetDistanceToTarget(
            GameObject target)
        {
            if (target == null)
                return float.MaxValue;

            return Vector2.Distance(
                transform.position,
                target.transform.position
            );
        }


        // ============================================================
        // Path Detection
        // ============================================================

        public bool CanMoveStraightToTarget(
            GameObject target)
        {
            if (target == null)
                return false;

            Vector2 startPosition
                = transform.position;

            Vector2 targetPosition
                = target.transform.position;

            RaycastHit2D hit = Physics2D.Linecast(
                startPosition,
                targetPosition,
                _obstacleLayer
            );

            return hit.collider == null;
        }
    }
}