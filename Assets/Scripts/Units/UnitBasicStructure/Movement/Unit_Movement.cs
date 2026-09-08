using UnityEngine;



namespace Units
{
    public class Unit_Movement : MonoBehaviour
    {
        // ============================================================
        // References
        // ============================================================

        [SerializeField] private Unit_RuntimeStatus _runtimeStatus;
        [SerializeField] private Rigidbody2D _rigidbody;


        // ============================================================
        // Settings
        // ============================================================

        [SerializeField] private float _arrivalDistance = 0.1f;


        // ============================================================
        // Data
        // ============================================================

        private Vector2 _destination;

        private bool _isMoving;
        private bool _canMove;


        // ============================================================
        // Properties
        // ============================================================

        public Vector2 Destination => _destination;

        public bool IsMoving => _isMoving;
        public bool CanMove => _canMove;

        public float MoveSpeed =>
            _runtimeStatus != null
                ? _runtimeStatus.MoveSpeed
                : 0f;


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Awake()
        {
            if (_runtimeStatus == null)
            {
                _runtimeStatus =
                    GetComponent<Unit_RuntimeStatus>();
            }

            if (_rigidbody == null)
            {
                _rigidbody =
                    GetComponent<Rigidbody2D>();
            }
        }


        private void FixedUpdate()
        {
            UpdateMovement();
        }


        // ============================================================
        // Initialize
        // ============================================================

        public void Initialize()
        {
            _destination = _rigidbody.position;

            _isMoving = false;
            _canMove = true;

            _rigidbody.linearVelocity =
                Vector2.zero;
        }


        // ============================================================
        // Public Methods
        // ============================================================

        public void MoveTo(Vector2 destination)
        {
            if (!_canMove)
                return;

            _destination = destination;
            _isMoving = true;
        }


        public void Stop()
        {
            _isMoving = false;

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity =
                    Vector2.zero;
            }
        }


        public void SetCanMove(bool canMove)
        {
            _canMove = canMove;

            if (!_canMove)
            {
                Stop();
            }
        }


        // ============================================================
        // Movement
        // ============================================================

        private void UpdateMovement()
        {
            if (!_canMove || !_isMoving)
                return;

            Vector2 currentPosition =
                _rigidbody.position;

            Vector2 toDestination =
                _destination - currentPosition;

            if (toDestination.sqrMagnitude
                <= _arrivalDistance * _arrivalDistance)
            {
                Arrive();
                return;
            }

            Vector2 direction =
                toDestination.normalized;

            _rigidbody.linearVelocity =
                direction * MoveSpeed;
        }


        private void Arrive()
        {
            _rigidbody.linearVelocity =
                Vector2.zero;

            _isMoving = false;
        }
    }
}