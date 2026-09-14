using System;
using UnityEngine;

namespace Units
{
    public sealed class DashController
    {
        // ============================================================
        // Reference
        // ============================================================

        private readonly Unit_Core _core;


        private readonly Rigidbody2D _body;


        private readonly Unit_Movement _movement;


        // ============================================================
        // Runtime State
        // ============================================================

        private Vector2 _direction;


        private float _remainingDistance;


        private float _speed;


        private bool _restoreMovement;


        private Action _onCompleted;


        // ============================================================
        // Properties
        // ============================================================

        public bool IsDashing { get; private set; }


        // ============================================================
        // Constructor
        // ============================================================

        public DashController(
            Unit_Core core)
        {
            _core = core;

            if (core == null)
                return;

            _body = core.GetComponent<Rigidbody2D>();

            _movement = core.GetComponent<Unit_Movement>();
        }


        // ============================================================
        // Dash
        // ============================================================

        public void StartDash(
            ICombatTarget target,
            float distance,
            float speed,
            Action onCompleted)
        {
            Cancel();

            if (_core == null || !CombatTargetUtility.IsValid(target))
            {
                onCompleted?.Invoke();

                return;
            }

            // 시작 시 Target 방향을 고정하고, 목표까지의 거리가 아닌 DashDistance만큼 이동한다.
            _direction = ((Vector2)target.Transform.position - (Vector2)_core.transform.position).normalized;

            _remainingDistance = Mathf.Max(
                0f,
                distance
            );

            _speed = Mathf.Max(
                0f,
                speed
            );

            _onCompleted = onCompleted;

            _core.StopMovement();

            _restoreMovement = _movement != null && _movement.enabled;

            if (_movement != null)
                _movement.enabled = false;

            IsDashing = true;

            if (_remainingDistance <= 0f || _speed <= 0f || _direction.sqrMagnitude <= 0f)
                Complete();
        }


        // ============================================================
        // Update
        // ============================================================

        public void FixedTick(
            float deltaTime)
        {
            if (!IsDashing)
                return;

            if (_core == null)
            {
                Cancel();

                return;
            }

            // 마지막 MovePosition이 물리에 반영된 다음 틱에서 돌진 완료를 처리한다.
            if (_remainingDistance <= 0f)
            {
                Complete();

                return;
            }

            float step = Mathf.Min(
                _remainingDistance,
                _speed * Mathf.Max(0f, deltaTime)
            );

            if (_body != null)
                _body.MovePosition(_body.position + _direction * step);

            else
                _core.transform.position += (Vector3)(_direction * step);

            _remainingDistance -= step;
        }


        // ============================================================
        // Cancel
        // ============================================================

        public void Cancel()
        {
            if (IsDashing)
            {
                // 대기 중인 일반 이동을 정리하되 MovementCompleted는 발생시키지 않는다.
                _core?.StopMovement();

                if (_body != null)
                    _body.linearVelocity = Vector2.zero;

                if (_movement != null)
                    _movement.enabled = _restoreMovement;
            }

            IsDashing = false;

            _remainingDistance = 0f;

            _onCompleted = null;
        }


        // ============================================================
        // Complete
        // ============================================================

        private void Complete()
        {
            var callback = _onCompleted;

            Cancel();

            callback?.Invoke();
        }
    }
}
