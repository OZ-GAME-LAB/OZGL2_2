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

        [SerializeField]
        private Collider2D _collider;


        // ============================================================
        // Movement Settings
        // ============================================================

        // Defaults match the current four unit prefabs. Existing serialized values remain valid.
        [SerializeField]
        private float _arrivalDistance = 0.15f;

        [SerializeField]
        private float _stuckDistanceThreshold = 1f;

        [SerializeField]
        private float _stuckTimeLimit = 1f;


        // ============================================================
        // Hold Settings
        // ============================================================

        [SerializeField]
        private float _holdStartReturnDistance = 0.35f;

        [SerializeField]
        private float _holdStopReturnDistance = 0.15f;

        [SerializeField]
        private float _holdReturnSpeed = 5f;

        [SerializeField]
        private float _holdAcceleration = 10f;


        // ============================================================
        // Environment Avoidance Settings
        // ============================================================

        [SerializeField]
        private LayerMask _environmentMask = 0;

        [SerializeField]
        private float _environmentCheckDistance = 0.5f;


        // ============================================================
        // Unit Avoidance Settings
        // ============================================================

        [SerializeField]
        private LayerMask _unitMask = 1 << 13;

        // Soft clearance around the actual body, not a replacement for its radius.
        [SerializeField]
        private float _unitAvoidanceRadius = 0.2f;

        [SerializeField]
        private float _unitCheckDistance = 0.2f;

        [SerializeField]
        private float _avoidanceDirectionHoldTime = 0.2f;


        // ============================================================
        // Avoidance Constants
        // ============================================================

        private const float PredictionTime = 0.35f;

        private const float VelocityResponseTime = 0.2f;


        private static readonly float[] SteeringAngles =
        {
            0f,
            -30f,
            30f,
            -60f,
            60f,
            -90f,
            90f
        };


        private static readonly float[] RecoveryAngles =
        {
            -120f,
            120f
        };


        // ============================================================
        // Physics Buffers
        // ============================================================

        private Collider2D[] _neighbours =
            new Collider2D[32];

        private readonly RaycastHit2D[] _environmentHits =
            new RaycastHit2D[8];


        // ============================================================
        // Movement Runtime State
        // ============================================================

        private Vector2 _targetPosition;

        private float _moveBestDistance;

        private float _moveProgressTime;

        private bool _isMoving;


        // ============================================================
        // Hold Runtime State
        // ============================================================

        private Vector2 _holdPosition;

        private float _holdBestDistance;

        private float _holdProgressTime;

        private float _holdYieldUntil;

        private int _holdRecoveryAttempt;

        private bool _isHoldingPosition;

        private bool _isReturningToHold;


        // ============================================================
        // Avoidance Runtime State
        // ============================================================

        private int _neighbourCount;

        private bool _neighbourBufferFull;

        private float _steeringSide;

        private float _steeringSideUntil;


        // ============================================================
        // Properties
        // ============================================================

        public bool IsMoving
            => _isMoving;

        public bool IsHoldingPosition
            => _isHoldingPosition;


        // ============================================================
        // Initialize
        // ============================================================

        public void Initialize(
            Unit_Core core)
        {
            _core =
                core;


            if (_rigidbody == null)
            {
                _rigidbody =
                    GetComponent<Rigidbody2D>();
            }


            if (_collider == null)
            {
                _collider =
                    GetComponent<Collider2D>();
            }


            Stop();
        }


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void FixedUpdate()
        {
            if (_rigidbody == null)
                return;


            if (_isMoving)
            {
                UpdateMovement();
            }
            else if (_isHoldingPosition)
            {
                UpdatePositionHold();
            }
        }


        // ============================================================
        // Movement Command
        // ============================================================

        public void MoveTo(
            Vector2 targetPosition)
        {
            if (_rigidbody == null)
                return;


            ReleasePosition();


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


            ReleasePosition();

            ResetMovementState();


            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity =
                    Vector2.zero;
            }
        }


        // ============================================================
        // Movement Update
        // ============================================================

        private void UpdateMovement()
        {
            if (_core == null
                || _core.RuntimeStatus == null)
            {
                return;
            }


            Vector2 offset =
                _targetPosition
                - _rigidbody.position;


            float distance =
                offset.magnitude;


            if (distance
                <= Mathf.Max(
                    0.01f,
                    _arrivalDistance
                ))
            {
                Stop();

                _core.NotifyMovementCompleted();

                return;
            }


            float speed =
                Mathf.Min(
                    Mathf.Max(
                        0f,
                        _core.RuntimeStatus.MoveSpeed
                    ),
                    distance
                    / Time.fixedDeltaTime
                );


            CheckProgress(
                distance
            );


            if (!_isMoving)
                return;


            // 속도를 덮어쓰지 않고 제한된 힘으로 접근하여
            // 가벼운 접촉 중에도 상대를 조금 밀며 진입한다.
            ApplyHoldVelocity(
                SelectVelocity(
                    offset.normalized,
                    speed,
                    false
                )
            );
        }


        // ============================================================
        // Position Hold Command
        // ============================================================

        public void HoldPosition(
            Vector2 position)
        {
            if (_rigidbody == null)
                return;


            _isMoving =
                false;


            _holdPosition =
                position;

            _isHoldingPosition =
                true;

            _isReturningToHold =
                false;


            ResetRecovery();
        }


        public void ReleasePosition()
        {
            _isHoldingPosition =
                false;

            _isReturningToHold =
                false;


            ResetRecovery();
        }


        // ============================================================
        // Position Hold Update
        // ============================================================

        private void UpdatePositionHold()
        {
            Vector2 offset =
                _holdPosition
                - _rigidbody.position;


            float distance =
                offset.magnitude;


            float stop =
                Mathf.Max(
                    0.01f,
                    _holdStopReturnDistance
                );


            float start =
                Mathf.Max(
                    stop + 0.05f,
                    _holdStartReturnDistance
                );


            // 일정 범위 이상 밀려난 경우에만 원래 위치로 복귀를 시작한다.
            if (!_isReturningToHold
                && distance >= start)
            {
                _isReturningToHold =
                    true;

                ResetRecovery();
            }


            // 충분히 원래 위치에 접근하면 복귀 상태를 종료한다.
            if (distance <= stop)
            {
                _isReturningToHold =
                    false;

                ResetRecovery();
            }


            if (!_isReturningToHold)
            {
                ApplyHoldVelocity(
                    Vector2.zero
                );

                return;
            }


            // 복구 과정에서 다른 유닛과의 교착을 피하기 위해
            // 지정된 시간 동안 현재 위치에서 양보한다.
            if (Time.time
                < _holdYieldUntil)
            {
                ApplyHoldVelocity(
                    Vector2.zero
                );

                return;
            }


            // 이전보다 Hold 위치에 가까워졌다면
            // 정상적으로 복귀가 진행되고 있는 것으로 판단한다.
            if (distance
                < _holdBestDistance - 0.05f)
            {
                ResetHoldProgress();

                _holdRecoveryAttempt =
                    0;
            }
            else if (Time.time
                - _holdProgressTime
                >= Mathf.Max(
                    0.5f,
                    _stuckTimeLimit
                ))
            {
                // Repeated stalls trigger new pauses and a new side search, not an endless fixed turn.
                _holdRecoveryAttempt++;


                _steeringSide =
                    -_steeringSide;


                _steeringSideUntil =
                    Time.time
                    + Mathf.Max(
                        0.1f,
                        _avoidanceDirectionHoldTime
                    )
                    + 0.5f;


                _holdYieldUntil =
                    Time.time
                    + 0.2f
                    + (
                        (GetInstanceID()
                            + _holdRecoveryAttempt)
                        & 3
                    )
                    * 0.1f;


                ResetHoldProgress();


                ApplyHoldVelocity(
                    Vector2.zero
                );

                return;
            }


            float remaining =
                Mathf.Max(
                    0f,
                    distance - stop
                );


            float speed =
                Mathf.Min(
                    Mathf.Max(
                        0f,
                        _holdReturnSpeed
                    ),
                    Mathf.Min(
                        remaining * 3f,
                        Mathf.Sqrt(
                            2f
                            * Mathf.Max(
                                0.01f,
                                _holdAcceleration
                            )
                            * remaining
                        )
                    )
                );


            ApplyHoldVelocity(
                SelectVelocity(
                    offset.normalized,
                    speed,
                    _holdRecoveryAttempt > 0
                )
            );
        }


        // ============================================================
        // Hold Velocity
        // ============================================================

        private void ApplyHoldVelocity(
            Vector2 targetVelocity)
        {
            Vector2 acceleration =
                Vector2.ClampMagnitude(
                    (
                        targetVelocity
                        - _rigidbody.linearVelocity
                    )
                    / VelocityResponseTime,
                    Mathf.Max(
                        0.01f,
                        _holdAcceleration
                    )
                );


            _rigidbody.AddForce(
                acceleration
                * _rigidbody.mass,
                ForceMode2D.Force
            );
        }


        // ============================================================
        // Velocity Selection
        // ============================================================

        private Vector2 SelectVelocity(
            Vector2 desired,
            float speed,
            bool recovering)
        {
            if (speed <= 0f
                || desired == Vector2.zero)
            {
                return Vector2.zero;
            }


            GatherNeighbours(
                speed
            );


            float forwardRisk =
                CollisionRisk(
                    desired * speed
                );


            bool hasProgress =
                !_isMoving
                || Time.time - _moveProgressTime
                    < Mathf.Max(
                        0.5f,
                        _stuckTimeLimit
                    );


            // 어깨 접촉 정도의 경로는 우회 후보와 경쟁시키지 않는다.
            // 진전이 없는 경우 또는 깊은 충돌이 예상되면 우회 탐색으로 전환한다.
            if (!recovering
                && hasProgress
                && !_neighbourBufferFull
                && forwardRisk <= 0.08f
                && !IsEnvironmentBlocked(
                    desired,
                    speed))
            {
                _steeringSideUntil =
                    0f;

                return desired
                    * speed
                    * Mathf.Lerp(
                        1f,
                        0.75f,
                        forwardRisk / 0.08f
                    );
            }


            Vector2 best =
                Vector2.zero;

            float bestScore =
                float.NegativeInfinity;

            float bestAngle =
                0f;

            float bestRisk =
                0f;


            // Score both sides against all neighbours. A right-hand tie break is only a preference.
            foreach (float angle
                in SteeringAngles)
            {
                ConsiderDirection(
                    desired,
                    speed,
                    angle,
                    ref best,
                    ref bestScore,
                    ref bestAngle,
                    ref bestRisk
                );
            }


            if (recovering)
            {
                foreach (float angle
                    in RecoveryAngles)
                {
                    ConsiderDirection(
                        desired,
                        speed,
                        angle,
                        ref best,
                        ref bestScore,
                        ref bestAngle,
                        ref bestRisk
                    );
                }
            }


            if (best == Vector2.zero)
                return Vector2.zero;


            // 한 번 선택한 회피 방향을 일정 시간 유지하여
            // 좌우 방향이 매 프레임 뒤집히는 현상을 줄인다.
            if (Mathf.Abs(
                    bestAngle)
                > 1f)
            {
                if (Time.time
                        >= _steeringSideUntil
                    || _steeringSide
                        != Mathf.Sign(
                            bestAngle
                        ))
                {
                    _steeringSideUntil =
                        Time.time
                        + Mathf.Max(
                            0.05f,
                            _avoidanceDirectionHoldTime
                        );
                }


                _steeringSide =
                    Mathf.Sign(
                        bestAngle
                    );
            }


            // 충돌 위험도가 높을수록 이동 속도를 낮춘다.
            float scale =
                Mathf.Lerp(
                    1f,
                    0.15f,
                    Mathf.Clamp01(
                        bestRisk
                    )
                );


            // 주변 유닛 버퍼가 포화된 경우에는
            // 판정 누락 가능성을 고려하여 보수적으로 이동한다.
            if (_neighbourBufferFull)
            {
                scale =
                    Mathf.Min(
                        scale,
                        0.25f
                    );
            }


            return best
                * speed
                * scale;
        }


        private void ConsiderDirection(
            Vector2 desired,
            float speed,
            float angle,
            ref Vector2 best,
            ref float bestScore,
            ref float bestAngle,
            ref float bestRisk)
        {
            float radians =
                angle
                * Mathf.Deg2Rad;


            Vector2 direction =
                new Vector2(
                    desired.x
                        * Mathf.Cos(
                            radians
                        )
                        - desired.y
                        * Mathf.Sin(
                            radians
                        ),

                    desired.x
                        * Mathf.Sin(
                            radians
                        )
                        + desired.y
                        * Mathf.Cos(
                            radians
                        )
                );


            if (IsEnvironmentBlocked(
                direction,
                speed))
            {
                return;
            }


            float risk =
                CollisionRisk(
                    direction
                    * speed
                );


            float score =
                Vector2.Dot(
                    desired,
                    direction
                )
                - risk * 4f;


            if (angle < 0f)
            {
                score +=
                    0.005f;
            }


            if (angle != 0f
                && Time.time
                    < _steeringSideUntil
                && Mathf.Sign(
                    angle
                ) == _steeringSide)
            {
                score +=
                    0.15f;
            }


            if (score <= bestScore)
                return;


            bestScore =
                score;

            best =
                direction;

            bestAngle =
                angle;

            bestRisk =
                risk;
        }


        // ============================================================
        // Neighbour Detection
        // ============================================================

        private void GatherNeighbours(
            float speed)
        {
            _neighbourCount =
                0;

            _neighbourBufferFull =
                false;


            if (_collider == null
                || _unitMask.value == 0)
            {
                return;
            }


            ContactFilter2D filter =
                new ContactFilter2D();


            filter.SetLayerMask(
                _unitMask
            );

            filter.useTriggers =
                false;


            // Include oncoming units over the prediction horizon at typical unit speeds.
            float range =
                BodyRadius(
                    _collider
                )
                + Mathf.Max(
                    0f,
                    _unitAvoidanceRadius
                )
                + Mathf.Max(
                    Mathf.Max(
                        0f,
                        _unitCheckDistance
                    ),
                    Mathf.Max(
                        speed,
                        _rigidbody.linearVelocity.magnitude
                    )
                    * PredictionTime
                    * 2f
                );


            do
            {
                _neighbourCount =
                    Physics2D.OverlapCircle(
                        _collider.bounds.center,
                        range,
                        filter,
                        _neighbours
                    );


                if (_neighbourCount
                    < _neighbours.Length)
                {
                    return;
                }


                if (_neighbours.Length
                    >= 256)
                {
                    _neighbourBufferFull =
                        true;

                    return;
                }


                System.Array.Resize(
                    ref _neighbours,
                    _neighbours.Length * 2
                );
            }
            while (true);
        }


        // ============================================================
        // Collision Prediction
        // ============================================================

        private float CollisionRisk(
            Vector2 velocity)
        {
            if (_collider == null)
                return 0f;


            float risk =
                0f;


            Vector2 center =
                _collider.bounds.center;


            float radius =
                BodyRadius(
                    _collider
                );


            float clearance =
                Mathf.Max(
                    0.01f,
                    _unitAvoidanceRadius
                );


            for (int i = 0;
                i < _neighbourCount;
                i++)
            {
                Collider2D other =
                    _neighbours[i];


                if (other == null
                    || other == _collider
                    || other.attachedRigidbody
                        == _rigidbody
                    || other.transform.IsChildOf(
                        transform))
                {
                    continue;
                }


                Vector2 offset =
                    (Vector2)other.bounds.center
                    - center;


                Vector2 otherVelocity =
                    other.attachedRigidbody
                        != null
                        ? other.attachedRigidbody.linearVelocity
                        : Vector2.zero;


                Vector2 relative =
                    otherVelocity
                    - velocity;


                float radii =
                    radius
                    + BodyRadius(
                        other
                    );


                float currentGap =
                    offset.magnitude
                    - radii;


                float gap =
                    PredictedGap(
                        offset,
                        relative,
                        radii,
                        PredictionTime
                    );


                // Side contact and motion separating the bodies must not block escape.
                if (Vector2.Dot(
                        offset,
                        relative)
                    >= 0f
                    && relative.sqrMagnitude
                        > 0.000001f)
                {
                    continue;
                }


                float overlap =
                    Mathf.Clamp01(
                        -gap
                        / Mathf.Max(
                            0.01f,
                            radii
                        )
                    );


                float soft =
                    Mathf.Clamp01(
                        (
                            clearance
                            - gap
                        )
                        / clearance
                    );


                // 반지름 합의 10% 이내인 얕은 예상 접촉은
                // 물리 충돌로 조금 밀고 들어갈 수 있도록 낮은 비용만 부여한다.
                // Collider를 축소하거나 실제 겹침을 강제로 허용하지는 않는다.
                float deepOverlap =
                    Mathf.Max(
                        0f,
                        overlap - 0.1f
                    ) / 0.9f;


                float candidateRisk =
                    soft * 0.02f
                    + deepOverlap;


                if (currentGap < -radii * 0.1f
                    && gap
                        < currentGap - 0.01f)
                {
                    candidateRisk +=
                        0.5f;
                }


                risk =
                    Mathf.Max(
                        risk,
                        candidateRisk
                    );
            }


            return risk;
        }


        // Pure geometry: closest separation of two moving circles over a short horizon.
        private static float PredictedGap(
            Vector2 offset,
            Vector2 relativeVelocity,
            float radii,
            float horizon)
        {
            float speedSqr =
                relativeVelocity.sqrMagnitude;


            float time =
                speedSqr > 0.000001f
                    ? Mathf.Clamp(
                        -Vector2.Dot(
                            offset,
                            relativeVelocity
                        )
                        / speedSqr,
                        0f,
                        horizon
                    )
                    : 0f;


            return (
                    offset
                    + relativeVelocity
                    * time
                ).magnitude
                - radii;
        }


        private static float BodyRadius(
            Collider2D body)
        {
            // bounds includes world scale and offset. Circles are the intended body shape;
            // other shapes retain a conservative enclosing radius.
            Vector3 extents =
                body.bounds.extents;


            return body
                is CircleCollider2D
                    ? Mathf.Max(
                        extents.x,
                        extents.y
                    )
                    : new Vector2(
                        extents.x,
                        extents.y
                    ).magnitude;
        }


        // ============================================================
        // Environment Avoidance
        // ============================================================

        private bool IsEnvironmentBlocked(
            Vector2 direction,
            float speed)
        {
            if (_collider == null
                || _environmentMask.value == 0)
            {
                return false;
            }


            ContactFilter2D filter =
                new ContactFilter2D();


            filter.SetLayerMask(
                _environmentMask
            );

            filter.useTriggers =
                false;


            return _collider.Cast(
                    direction,
                    filter,
                    _environmentHits,
                    Mathf.Max(
                        Mathf.Max(
                            0.01f,
                            _environmentCheckDistance
                        ),
                        speed
                        * Time.fixedDeltaTime
                    )
                )
                > 0;
        }


        // ============================================================
        // Stuck Detection
        // ============================================================

        private void CheckProgress(
            float distance)
        {
            float threshold =
                Mathf.Max(
                    0.01f,
                    _stuckDistanceThreshold
                );


            // 마지막으로 인정한 거리보다 충분히 가까워졌을 때만
            // 실제 이동 진전이 있었다고 판단한다.
            if (distance
                <= _moveBestDistance
                - threshold)
            {
                _moveBestDistance =
                    distance;

                _moveProgressTime =
                    Time.time;

                return;
            }


            float timeLimit =
                Mathf.Max(
                    0.1f,
                    _stuckTimeLimit
                );


            // 일정 시간 동안 목표 위치까지 의미 있게 접근하지 못했다면
            // 현재 이동 경로로는 목표에 도달하기 어렵다고 판단한다.
            if (Time.time
                - _moveProgressTime
                < timeLimit)
            {
                return;
            }


            Stop();

            _core?.NotifyMovementFailed();
        }


        // ============================================================
        // Reset
        // ============================================================

        private void ResetMovementState()
        {
            _moveBestDistance =
                _rigidbody == null
                    ? 0f
                    : Vector2.Distance(
                        _rigidbody.position,
                        _targetPosition
                    );


            _moveProgressTime =
                Time.time;
        }


        private void ResetRecovery()
        {
            _holdYieldUntil =
                0f;

            _holdRecoveryAttempt =
                0;

            _steeringSide =
                0f;

            _steeringSideUntil =
                0f;


            ResetHoldProgress();
        }


        private void ResetHoldProgress()
        {
            _holdBestDistance =
                _rigidbody == null
                    ? 0f
                    : Vector2.Distance(
                        _rigidbody.position,
                        _holdPosition
                    );


            _holdProgressTime =
                Time.time;
        }
    }
}