using UnityEngine;


namespace Units
{
    public class Unit_Movement : MonoBehaviour
    {
        // ============================================================
        // Reference
        // ============================================================

        private Unit_Core _core;

        [SerializeField]
        private Rigidbody2D _rigidbody;


        // ============================================================
        // Settings
        // ============================================================

        [SerializeField]
        private float _arrivalDistance = 0.1f;

        [SerializeField]
        private float _stuckCheckInterval = 0.5f;

        [SerializeField]
        private float _stuckDistanceThreshold = 0.05f;

        [SerializeField]
        private float _stuckTimeLimit = 1.5f;


        // ============================================================
        // Runtime State
        // ============================================================

        private Vector2 _targetPosition;

        private Vector2 _lastCheckPosition;

        private float _nextStuckCheckTime;

        private float _stuckTime;

        private bool _isMoving;


        // ============================================================
        // Properties
        // ============================================================

        public bool IsMoving
            => _isMoving;


        // ============================================================
        // Initialize
        // ============================================================

        public void Initialize(
            Unit_Core core)
        {
            _core = core;

            _isMoving = false;

            ResetMovementState();
        }


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void FixedUpdate()
        {
            UpdateMovement();
        }


        // ============================================================
        // Movement
        // ============================================================

        public void MoveTo(
            Vector2 targetPosition)
        {
            if (_rigidbody == null)
                return;


            _targetPosition =
                targetPosition;

            _isMoving =
                true;


            ResetMovementState();
        }


        public void Stop()
        {
            _isMoving =
                false;

            ResetMovementState();


            if (_rigidbody == null)
                return;


            _rigidbody.linearVelocity =
                Vector2.zero;
        }


        private void UpdateMovement()
        {
            if (!_isMoving)
                return;

            if (_core == null)
                return;

            if (_rigidbody == null)
                return;


            Vector2 currentPosition =
                _rigidbody.position;


            float distance =
                Vector2.Distance(
                    currentPosition,
                    _targetPosition
                );


            if (distance <= _arrivalDistance)
            {
                CompleteMovement();

                return;
            }


            UpdateVelocity(
                currentPosition,
                distance
            );


            CheckStuck(
                currentPosition
            );
        }


        private void UpdateVelocity(
            Vector2 currentPosition,
            float distance)
        {
            Vector2 direction =
                (
                    _targetPosition
                    - currentPosition
                ).normalized;


            float moveSpeed =
                Mathf.Max(
                    0f,
                    _core.RuntimeStatus.MoveSpeed
                );


            float maxSpeedToTarget =
                distance
                / Time.fixedDeltaTime;


            float currentSpeed =
                Mathf.Min(
                    moveSpeed,
                    maxSpeedToTarget
                );


            _rigidbody.linearVelocity =
                direction
                * currentSpeed;
        }


        // ============================================================
        // Stuck Detection
        // ============================================================

        private void CheckStuck(
            Vector2 currentPosition)
        {
            if (Time.time
                < _nextStuckCheckTime)
            {
                return;
            }


            _nextStuckCheckTime =
                Time.time
                + _stuckCheckInterval;


            float movedDistance =
                Vector2.Distance(
                    currentPosition,
                    _lastCheckPosition
                );


            if (movedDistance
                <= _stuckDistanceThreshold)
            {
                _stuckTime +=
                    _stuckCheckInterval;
            }
            else
            {
                _stuckTime =
                    0f;
            }


            _lastCheckPosition =
                currentPosition;


            if (_stuckTime
                < _stuckTimeLimit)
            {
                return;
            }


            FailMovement();
        }


        private void ResetMovementState()
        {
            _stuckTime =
                0f;

            _nextStuckCheckTime =
                Time.time
                + _stuckCheckInterval;


            if (_rigidbody != null)
            {
                _lastCheckPosition =
                    _rigidbody.position;
            }
        }


        // ============================================================
        // Complete / Fail
        // ============================================================

        private void CompleteMovement()
        {
            Stop();

            _core?.NotifyMovementCompleted();
        }


        private void FailMovement()
        {
            Stop();

            _core?.NotifyMovementFailed();
        }
    }
}