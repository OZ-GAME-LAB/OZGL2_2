using UnityEngine;
using System.Collections.Generic;


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
        private float _stuckDistanceThreshold = 0.2f;

        [SerializeField]
        private float _stuckTimeLimit = 1f;


        // ============================================================
        // Rally Settings
        // ============================================================

        [SerializeField, Min(0.1f)]
        private float _rallyStuckDelay = 3f;

        // 아군 경유 이동과 적 집결 이동은 전투 스탯과 별도의 속도를 사용한다.
        [SerializeField, Min(0f)]
        private float _rallyMoveSpeed = 15f;

        [SerializeField, Min(0.1f)]
        private float _rallyFailureDelay = 8f;

        // 경유점은 통과 판정에 별도의 반경을 사용한다.
        [SerializeField, Min(0.01f)]
        private float _rallyViaArrivalDistance = 1f;

        // 최종 반경에 진입하면 준비 완료를 알린다. 자리 정렬은 전투 전 Hold가 담당한다.
        [SerializeField, Min(0.01f)]
        private float _rallyArrivalDistance = 1f;


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

        // 장애물 외곽을 따라갈 때 몸체와 벽 사이에 확보할 여유 거리다.
        [SerializeField, Min(0.02f)]
        private float _wallClearance = 0.15f;

        private Collider2D _followObstacle;
        private float _wallSide;
        private readonly List<Vector2> _wallVisited = new List<Vector2>();


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

        // 재시도 중에도 최단 거리와 무진전 시간을 유지하여 제자리 흔들림을 제외한다.
        private bool _isRallyMovement;

        private bool _isRallyViaMovement;

        private bool _isMovementPaused;

        private float _rallyNoProgressTime;

        private float _rallyAttemptTime;

        private float _rallyRetryRemaining;


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
        // Restriction Runtime State
        // ============================================================

        private bool _isMovementBlocked;


        // ============================================================
        // Properties
        // ============================================================

        public bool IsMoving
            => _isMoving;

        public bool IsHoldingPosition
            => _isHoldingPosition;

        public bool CanMove
            => !_isMovementBlocked;


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


            _isMovementBlocked =
                false;


            _isMovementPaused = false;

            Stop();
        }


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void OnDisable()
        {
            Stop();

            _isMovementPaused = false;
        }

        private void FixedUpdate()
        {
            if (_rigidbody == null)
                return;


            if (_isMovementBlocked || _isMovementPaused)
            {
                if (_rigidbody.linearVelocity !=
                    Vector2.zero)
                {
                    _rigidbody.linearVelocity =
                        Vector2.zero;
                }

                return;
            }


            if (_isMoving)
            {
                UpdateMovement();

                // 목적지 이동의 실제 속도를 사용한다. 자리 유지 보정은 시선을 바꾸지 않는다.
                if (_isMoving)
                    _core?.SetMovementFacingDirection(_rigidbody.linearVelocity);
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
            _isRallyMovement = false;

            _isRallyViaMovement = false;

            BeginMovement(targetPosition);
        }

        public void MoveToRally(Vector2 targetPosition, bool isViaPoint = false)
        {
            _isRallyMovement = true;

            _isRallyViaMovement = isViaPoint;

            _rallyNoProgressTime = 0f;

            _rallyAttemptTime = 0f;

            _rallyRetryRemaining = 0f;

            BeginMovement(targetPosition);
        }

        private void BeginMovement(Vector2 targetPosition)
        {
            if (_rigidbody == null)
                return;


            if (!CanMove && !_isRallyMovement)
                return;


            ReleasePosition();

            ResetWallFollowing();


            _targetPosition =
                targetPosition;

            _isMoving =
                true;


            ResetMovementState();
        }


        /// <summary>추적 목적지만 갱신하고 속도, 회피 방향과 정체 감시는 유지한다.</summary>
        public void UpdateDestination(Vector2 targetPosition)
        {
            if (_rigidbody == null || !CanMove) return;
            if (!_isMoving)
            {
                MoveTo(targetPosition);
                return;
            }

            // 목적지가 움직인 거리는 유닛 자신의 전진/후퇴로 계산하지 않는다.
            // 진행 시간을 초기화하면 움직이는 대상을 쫓다 막힌 상태를 영원히 감지하지 못한다.
            float previousDistance = Vector2.Distance(_rigidbody.position, _targetPosition);
            float nextDistance = Vector2.Distance(_rigidbody.position, targetPosition);
            _moveBestDistance += nextDistance - previousDistance;
            _targetPosition = targetPosition;
        }

        public void PauseMovement()
        {
            _isMovementPaused = true;

            // Rally 경로와 정체 누적값은 일시 정지 동안 보존한다.
            if (!_isRallyMovement)
                Stop();

            if (_rigidbody != null)
                _rigidbody.linearVelocity = Vector2.zero;
        }

        public void ResumeMovement()
        {
            _isMovementPaused = false;
        }

        public void Stop()
        {
            ResetWallFollowing();
            _isRallyMovement = false;

            _isRallyViaMovement = false;

            _rallyNoProgressTime = 0f;

            _rallyAttemptTime = 0f;

            _rallyRetryRemaining = 0f;

            _core?.PlayAnimation_Idle();
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
        // Movement Restriction
        // ============================================================

        public void SetMovementBlocked(
            bool isBlocked)
        {
            if (_isMovementBlocked ==
                isBlocked)
            {
                return;
            }


            _isMovementBlocked =
                isBlocked;


            if (!isBlocked)
                return;

            // 이동 제한은 통행 실패가 아니다. 해제 후 같은 Rally 구간을 재개한다.
            if (_isRallyMovement)
            {
                if (_rigidbody != null)
                    _rigidbody.linearVelocity = Vector2.zero;

                return;
            }


            Stop();
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
                    _isRallyViaMovement
                        ? Mathf.Max(_arrivalDistance, _rallyViaArrivalDistance)
                        : _isRallyMovement ? _rallyArrivalDistance : _arrivalDistance
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
                        _isRallyMovement ? _rallyMoveSpeed : _core.RuntimeStatus.MoveSpeed
                    ),
                    distance
                    / Time.fixedDeltaTime
                );


            CheckProgress(
                distance
            );


            if (!_isMoving)
                return;

            if (_isRallyMovement && _rallyRetryRemaining > 0f)
            {
                _rigidbody.linearVelocity = Vector2.zero;

                _core.PlayAnimation_Idle();

                return;
            }


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


            if (!CanMove)
                return;

            ResetWallFollowing();

            _isRallyMovement = false;

            _isRallyViaMovement = false;

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
            ResetWallFollowing();
            if (!_isMoving)
                _core?.PlayAnimation_Idle();

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
            if (HasWallProgress() || distance
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
            if (targetVelocity.sqrMagnitude > 0.0001f)
                _core?.PlayAnimation_Move();
            else
                _core?.PlayAnimation_Idle();

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

            desired = FollowEnvironment(desired, speed);


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
            // 외곽 추종 중에는 유닛 회피 후보도 진행 방향을 뒤집지 않도록 제한한다.
            if (_followObstacle != null && Mathf.Abs(angle) > 60f) return;
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
        // Environment Following
        // ============================================================

        private bool CastEnvironment(Vector2 direction, float distance, out RaycastHit2D nearest)
        {
            nearest = default;
            if (_collider == null || _environmentMask.value == 0) return false;
            var filter = new ContactFilter2D();
            filter.SetLayerMask(_environmentMask);
            filter.useTriggers = false;
            int count = _collider.Cast(direction, filter, _environmentHits, distance);
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = _environmentHits[i];
                if (hit.collider == null || hit.collider == _collider || hit.distance >= best) continue;
                best = hit.distance;
                nearest = hit;
            }
            return nearest.collider != null;
        }

        private Vector2 FollowEnvironment(Vector2 desired, float speed)
        {
            Vector2 position = _rigidbody.position;
            Vector2 target = _isMoving ? _targetPosition : _holdPosition;
            float distance = Vector2.Distance(position, target);
            float lookAhead = Mathf.Max(_environmentCheckDistance, speed * Time.fixedDeltaTime + _wallClearance);
            bool blocked = CastEnvironment(desired, lookAhead, out var hit);
            if (_followObstacle == null || !_followObstacle.enabled || !_followObstacle.gameObject.activeInHierarchy)
            {
                _followObstacle = null;
                if (!blocked) return desired;
                _followObstacle = hit.collider;
                Vector2 tangent = new Vector2(-hit.normal.y, hit.normal.x);
                // 목표 방향과 잘 맞는 쪽을 우선하며 실제로 막힌 쪽은 선택하지 않는다.
                _wallSide = Vector2.Dot(tangent, desired) >= 0f ? 1f : -1f;
                if (CastEnvironment(tangent * _wallSide, lookAhead, out _) &&
                    !CastEnvironment(-tangent * _wallSide, lookAhead, out _)) _wallSide = -_wallSide;
                if (_wallVisited.Count == 0) _wallVisited.Add(position);
            }

            // 목적지까지의 전체 직선이 아니라 당장 전진할 구간만 검사한다.
            // 진입 검사보다 조금 긴 여유 구간을 확보해 모서리에서 상태가 반복 전환되지 않게 한다.
            float forwardDistance = Mathf.Min(distance, lookAhead + _wallClearance);
            if (!CastEnvironment(desired, forwardDistance, out _))
            {
                _followObstacle = null;
                return desired;
            }

            Vector2 normal = position - _followObstacle.ClosestPoint(position);
            if (normal.sqrMagnitude < 0.0001f) normal = blocked ? hit.normal : -desired;
            normal.Normalize();
            Vector2 along = new Vector2(-normal.y, normal.x) * _wallSide;
            // 연결된 건물로 접촉 대상이 바뀌어도 우회 방향과 방문 기록은 보존한다.
            if (CastEnvironment(along, lookAhead, out var next) && next.collider != _followObstacle)
            {
                _followObstacle = next.collider;
                normal = next.normal;
                along = new Vector2(-normal.y, normal.x) * _wallSide;
            }
            float radius = Mathf.Abs(normal.x) * _collider.bounds.extents.x +
                Mathf.Abs(normal.y) * _collider.bounds.extents.y;
            float gap = Vector2.Distance(position, _followObstacle.ClosestPoint(position)) - radius;
            float correction = Mathf.Clamp((_wallClearance - gap) * 2f, -0.75f, 0.75f);
            return (along + normal * correction).normalized;
        }

        private bool HasWallProgress()
        {
            if (_followObstacle == null || _rigidbody == null || _wallVisited.Count >= 2048) return false;
            Vector2 position = _rigidbody.position;
            float step = Mathf.Max(0.25f, _stuckDistanceThreshold);
            // 같은 공간을 왕복하거나 한 바퀴 도는 움직임은 새 진전으로 인정하지 않는다.
            foreach (Vector2 visited in _wallVisited)
                if ((position - visited).sqrMagnitude < step * step) return false;
            _wallVisited.Add(position);
            _moveProgressTime = Time.time;
            return true;
        }

        private void ResetWallFollowing()
        {
            _followObstacle = null;
            _wallSide = 0f;
            _wallVisited.Clear();
        }


        // ============================================================
        // Stuck Detection
        // ============================================================

        private void CheckProgress(
            float distance)
        {
            if (_isRallyMovement)
            {
                CheckRallyProgress(distance, Time.fixedDeltaTime);

                return;
            }

            float threshold =
                Mathf.Max(
                    0.01f,
                    _stuckDistanceThreshold
                );


            // 마지막으로 인정한 거리보다 충분히 가까워졌을 때만
            // 실제 이동 진전이 있었다고 판단한다.
            if (HasWallProgress() || distance
                <= _moveBestDistance
                - threshold)
            {
                _moveBestDistance =
                    Mathf.Min(_moveBestDistance, distance);

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
        // Rally Stuck Detection
        // ============================================================

        private void CheckRallyProgress(float distance, float deltaTime)
        {
            float threshold = Mathf.Max(0.01f, _stuckDistanceThreshold);

            if (HasWallProgress() || distance <= _moveBestDistance - threshold)
            {
                _moveBestDistance = Mathf.Min(_moveBestDistance, distance);

                _moveProgressTime = Time.time;

                _rallyNoProgressTime = 0f;

                _rallyAttemptTime = 0f;

                _rallyRetryRemaining = 0f;

                return;
            }

            _rallyNoProgressTime += deltaTime;

            float stuckDelay = Mathf.Max(0.1f, _rallyStuckDelay);

            if (_rallyNoProgressTime >= Mathf.Max(stuckDelay, _rallyFailureDelay))
            {
                Stop();

                _core?.NotifyMovementFailed();

                return;
            }

            if (_rallyRetryRemaining > 0f)
            {
                _rallyRetryRemaining = Mathf.Max(0f, _rallyRetryRemaining - deltaTime);

                return;
            }

            _rallyAttemptTime += deltaTime;

            if (_rallyAttemptTime < stuckDelay)
                return;

            _rallyAttemptTime = 0f;

            _rallyRetryRemaining = Random.Range(0.5f, 1f);

            // 같은 목적지를 유지하고 회피 방향만 다시 선택한다.
            // MoveTo를 재호출하면 무진전 누적값이 초기화되므로 호출하지 않는다.
            // 정체 재시도는 유닛 회피만 초기화한다. 장애물 우회 방향은 계속 유지한다.
            _steeringSide = 0f;

            _steeringSideUntil = 0f;
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
