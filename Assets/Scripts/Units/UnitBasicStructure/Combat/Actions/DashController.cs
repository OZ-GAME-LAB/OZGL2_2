using System;
using UnityEngine;

namespace Units
{
    public sealed class DashController
    {
        // ============================================================
        // Constants
        // ============================================================

        private const float TargetStopDistance = 1.15f;

        // Rigidbody2D 위치와 TargetStopDistance의 부동소수점 경계에서
        // Dash가 극소 이동을 반복하며 종료되지 않는 것을 방지한다.
        private const float ArrivalTolerance = 0.01f;

        private const int UnitLayer = 13;


        // ============================================================
        // Reference
        // ============================================================

        private readonly Unit_Core _core;

        private readonly Rigidbody2D _body;

        private readonly Unit_Movement _movement;


        // ============================================================
        // Runtime State
        // ============================================================

        private ICombatTarget _target;

        private Vector2 _direction;

        private float _remainingDistance;

        private float _speed;

        private bool _restoreMovement;

        private Action _onCompleted;


        // ============================================================
        // Collision Runtime State
        // ============================================================

        private LayerMask _previousExcludeLayers;

        private bool _unitCollisionIgnored;


        // ============================================================
        // Properties
        // ============================================================

        public bool IsDashing { get; private set; }

        public bool HasMoved { get; private set; }


        // 재선택은 정지 대상만 바꾼다. 시작 시 고정한 진행 방향은 유지한다.
        public void Retarget(ICombatTarget target)
        {
            _target = target;
        }


        // ============================================================
        // Constructor
        // ============================================================

        public DashController(Unit_Core core)
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

            HasMoved = false;

            if (_core == null || !CombatTargetUtility.IsValid(target))
            {
                onCompleted?.Invoke();

                return;
            }

            _target = target;

            Vector2 currentPosition = GetCurrentPosition();

            Vector2 targetPosition = target.Transform.position;

            // Dash 시작 시 진행 방향을 고정한다.
            // Target이 이동하더라도 진행 방향 자체는 변경하지 않는다.
            _direction = (targetPosition - currentPosition).normalized;

            _core.SetFacingDirection(_direction);

            // DashDistance는 반드시 이동할 거리가 아니라
            // 한 번의 Dash에서 이동할 수 있는 최대 거리이다.
            _remainingDistance = Mathf.Max(0f, distance);

            _speed = Mathf.Max(0f, speed);

            _onCompleted = onCompleted;

            _core.StopMovement();

            _restoreMovement = _movement != null && _movement.enabled;

            if (_movement != null)
                _movement.enabled = false;

            IgnoreUnitCollision();

            IsDashing = true;

            if (_remainingDistance <= ArrivalTolerance
                || _speed <= 0f
                || _direction.sqrMagnitude <= 0f
                || HasReachedTarget())
            {
                Complete();
            }
        }


        // ============================================================
        // Update
        // ============================================================

        public void FixedTick(float deltaTime)
        {
            if (!IsDashing)
                return;

            if (_core == null)
            {
                Cancel();

                return;
            }

            if (!CombatTargetUtility.IsValid(_target))
            {
                Complete();

                return;
            }

            if (_remainingDistance <= ArrivalTolerance || HasReachedTarget())
            {
                Complete();

                return;
            }

            Vector2 currentPosition = GetCurrentPosition();

            Vector2 targetPosition = _target.Transform.position;

            float step = Mathf.Min(
                _remainingDistance,
                _speed * Mathf.Max(0f, deltaTime)
            );

            // 이번 FixedTick에서 Target 앞의 정지 지점을 넘어가지 않도록
            // 실제 이동량을 제한한다.
            float distanceToTarget = Vector2.Distance(
                currentPosition,
                targetPosition
            );

            float availableDistance = Mathf.Max(
                0f,
                distanceToTarget - TargetStopDistance
            );

            step = Mathf.Min(step, availableDistance);

            // TargetStopDistance 경계에서 부동소수점 오차로 극소 이동을
            // 반복하지 않도록 충분히 작은 이동량은 도착으로 처리한다.
            if (step <= ArrivalTolerance)
            {
                Complete();

                return;
            }

            HasMoved = true;

            Vector2 nextPosition = currentPosition + _direction * step;

            if (_body != null)
            {
                _body.MovePosition(nextPosition);
            }
            else
            {
                _core.transform.position = nextPosition;
            }

            _remainingDistance = Mathf.Max(
                0f,
                _remainingDistance - step
            );
        }


        // ============================================================
        // Target
        // ============================================================

        private bool HasReachedTarget()
        {
            if (!CombatTargetUtility.IsValid(_target))
            {
                return true;
            }

            Vector2 currentPosition = GetCurrentPosition();

            Vector2 targetPosition = _target.Transform.position;

            Vector2 toTarget = targetPosition - currentPosition;

            // TargetStopDistance의 정확한 경계값에 의존하지 않고
            // 작은 허용 오차 안에 들어오면 Dash 도착으로 처리한다.
            float arrivalDistance =
                TargetStopDistance + ArrivalTolerance;

            if (toTarget.sqrMagnitude
                <= arrivalDistance * arrivalDistance)
            {
                return true;
            }

            // 시작 시 고정한 진행 방향을 기준으로 Target이 뒤쪽에 있다면
            // 이미 Target을 지나친 것으로 판단한다.
            if (Vector2.Dot(toTarget, _direction) <= 0f)
            {
                return true;
            }

            return false;
        }

        private Vector2 GetCurrentPosition()
        {
            if (_body != null)
                return _body.position;

            return _core.transform.position;
        }


        // ============================================================
        // Collision
        // ============================================================

        private void IgnoreUnitCollision()
        {
            if (_body == null || _unitCollisionIgnored)
            {
                return;
            }

            // Dash 이전의 충돌 제외 설정을 그대로 보관한다.
            // Dash 종료 시 Unit Layer만 제거하는 것이 아니라
            // 원래 설정 전체를 복구하기 위해 필요하다.
            _previousExcludeLayers = _body.excludeLayers;

            int unitMask = 1 << UnitLayer;

            // GameObject의 Layer나 Collider 활성 상태는 변경하지 않는다.
            // 따라서 피격/탐색 판정은 그대로 유지하면서
            // Rigidbody의 Unit Layer 물리 충돌만 Dash 동안 제외한다.
            _body.excludeLayers = _body.excludeLayers | unitMask;

            _unitCollisionIgnored = true;
        }

        private void RestoreUnitCollision()
        {
            if (_body == null || !_unitCollisionIgnored)
            {
                return;
            }

            // Dash 시작 직전의 충돌 제외 설정으로 완전히 복구한다.
            _body.excludeLayers = _previousExcludeLayers;

            _unitCollisionIgnored = false;
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

            // Dash가 정상 종료되거나 중간에 취소되는 모든 경우에
            // Unit Layer 물리 충돌을 원래 상태로 복구한다.
            RestoreUnitCollision();

            IsDashing = false;

            _target = null;

            _direction = Vector2.zero;

            _remainingDistance = 0f;

            _speed = 0f;

            _onCompleted = null;
        }


        // ============================================================
        // Complete
        // ============================================================

        private void Complete()
        {
            var callback = _onCompleted;

            Cancel();

            // Dash로 인한 전투 위치 변경을 알린다.
            _core?.NotifyCombatPositionChanged();

            callback?.Invoke();
        }


    }
}