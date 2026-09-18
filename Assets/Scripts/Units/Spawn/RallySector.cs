using UnityEngine;




namespace Units
{
    public class RallySector : MonoBehaviour
    {
        // ============================================================
        // Constants
        // ============================================================

        private const float GizmoDisplayDuration =
            7f;


        // ============================================================
        // Runtime
        // ============================================================

        [SerializeField]
        private Unit_GroupAI _assignedGroup;

        private float
            _gizmoDisplayEndTime;


        // ============================================================
        // Gizmo Settings
        // ============================================================

        [Header("Gizmo")]

        [SerializeField]
        private Vector2 _gizmoSize =
            new Vector2(
                5f,
                10f
            );


        // ============================================================
        // Properties
        // ============================================================

        public Vector2 Center =>
            transform.position;

        public bool IsOccupied =>
            _assignedGroup != null;

        public Unit_GroupAI AssignedGroup =>
            _assignedGroup;


        // ============================================================
        // Assignment
        // ============================================================

        internal void Assign(
            Unit_GroupAI group)
        {
            _assignedGroup =
                group;


            _gizmoDisplayEndTime =
                Time.time +
                GizmoDisplayDuration;
        }


        internal void Clear()
        {
            _assignedGroup =
                null;

            _gizmoDisplayEndTime =
                0f;
        }


        // ============================================================
        // Gizmo
        // ============================================================

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
                return;


            if (Time.time >
                _gizmoDisplayEndTime)
            {
                return;
            }


            Gizmos.DrawWireCube(
                transform.position,
                new Vector3(
                    _gizmoSize.x,
                    _gizmoSize.y,
                    0f
                )
            );
        }
    }
}