using System;
using System.Collections.Generic;
using Units.Skills;
using UnityEngine;


namespace Units
{
    public class ActiveSkillExecutor
    {
        // ============================================================
        // Reference
        // ============================================================

        private readonly Unit_Core _core;

        private readonly TargetResolver _targetResolver;


        // ============================================================
        // Data
        // ============================================================

        private readonly ActiveSkillData _data;


        // ============================================================
        // Action Controllers
        // ============================================================

        private readonly CastController _castController =
            new CastController();

        private readonly DashController _dashController;


        // ============================================================
        // Runtime State
        // ============================================================

        private Action _onCompleted;

        private ICombatTarget _target;

        private int _executionId;

        private bool _isExecuting;

        private Predicate<ICombatTarget> _targetFilter;


        // ============================================================
        // Properties
        // ============================================================

        public bool IsCasting =>
            _castController.IsCasting;

        public bool IsDashing =>
            _dashController.IsDashing;


        // ============================================================
        // Runtime Buffer
        // ============================================================

        private readonly List<ICombatTarget> _singleTargetBuffer;

        private readonly List<ICombatTarget> _projectileTargetBuffer;


        // ============================================================
        // Constructor
        // ============================================================

        public ActiveSkillExecutor(
            Unit_Core core,
            ActiveSkillData data,
            TargetResolver targetResolver)
        {
            _core =
                core;

            _dashController =
                new DashController(
                    core
                );

            _data =
                data;

            _targetResolver =
                targetResolver;


            _singleTargetBuffer =
                new List<ICombatTarget>(
                    1
                );


            _projectileTargetBuffer =
                new List<ICombatTarget>();
        }


        // ============================================================
        // Execute
        // ============================================================

        public bool CanExecute(
            ICombatTarget target)
        {
            if (_core == null
                || !_core.IsAlive
                || !_core.isActiveAndEnabled
                || _core.RuntimeStatus == null
                || _data == null
                || !CombatTargetUtility.IsValid(target)
                || !target.IsTargetable)
            {
                return false;
            }


            if (_data.ActionType != ActiveSkillActionType.Instant
                && _data.ActionType != ActiveSkillActionType.Cast
                && _data.ActionType != ActiveSkillActionType.Dash)
            {
                return false;
            }


            if (_data.DeliveryType != ActiveSkillDeliveryType.Direct
                && _data.DeliveryType != ActiveSkillDeliveryType.Projectile)
            {
                return false;
            }


            if (_data.ActionType == ActiveSkillActionType.Dash
                && (_data.DashDistance <= 0f
                    || _data.DashSpeed <= 0f))
            {
                return false;
            }


            if (_data.DeliveryType == ActiveSkillDeliveryType.Projectile
                && _data.ProjectileSpeed <= 0f)
            {
                return false;
            }


            return SkillEffectResolver.Instance != null;
        }


        public void Execute(
            ICombatTarget target,
            Action onCompleted,
            Predicate<ICombatTarget> targetFilter = null)
        {
            Cancel();


            if (!CanExecute(target)
                || (targetFilter != null
                    && !targetFilter(target)))
            {
                onCompleted?.Invoke();

                return;
            }


            _targetFilter =
                targetFilter;

            _target =
                target;

            _onCompleted =
                onCompleted;

            _isExecuting =
                true;


            switch (_data.ActionType)
            {
                case ActiveSkillActionType.Instant:

                    FinishAttack(
                        target
                    );

                    break;


                case ActiveSkillActionType.Cast:

                    ExecuteCast(
                        target
                    );

                    break;


                case ActiveSkillActionType.Dash:

                    ExecuteDash(
                        target
                    );

                    break;


                default:

                    CompleteExecution();

                    break;
            }
        }


        // ============================================================
        // Action
        // ============================================================

        public void Tick(
            float deltaTime)
        {
            if (ValidateExecution())
            {
                _castController.Tick(
                    deltaTime
                );
            }
        }


        public void FixedTick(
            float deltaTime)
        {
            if (ValidateExecution())
            {
                _dashController.FixedTick(
                    deltaTime
                );
            }
        }


        public void Cancel()
        {
            _executionId++;

            _targetFilter =
                null;

            _isExecuting =
                false;

            _onCompleted =
                null;

            _target =
                null;


            _castController.Cancel();

            _dashController.Cancel();
        }


        private bool ValidateExecution()
        {
            if (!_isExecuting)
                return false;


            if (_core == null
                || !_core.isActiveAndEnabled
                || !_core.IsAlive)
            {
                CompleteExecution();

                return false;
            }


            if (!CombatTargetUtility.IsValid(
                    _target)
                || (_targetFilter != null
                    && !_targetFilter(_target)))
            {
                // Target이 무효화되면 지연 공격 없이 현재 행동을 완료한다.
                CompleteExecution();

                return false;
            }


            return true;
        }


        private void FinishAttack(
            ICombatTarget target)
        {
            if (!_isExecuting)
                return;


            int executionId =
                _executionId;


            try
            {
                if (_core != null
                    && _core.IsAlive
                    && CombatTargetUtility.IsValid(target)
                    && (_targetFilter == null
                        || _targetFilter(target)))
                {
                    ExecuteAttack(
                        target
                    );
                }
            }
            finally
            {
                if (executionId == _executionId)
                {
                    CompleteExecution();
                }
            }
        }


        private void CompleteExecution()
        {
            var callback =
                _onCompleted;


            Cancel();


            callback?.Invoke();
        }


        private void ExecuteCast(
            ICombatTarget target)
        {
            float attackSpeed =
                Mathf.Max(
                    0.01f,
                    _core.RuntimeStatus.AttackSpeed
                );


            float finalCastTime =
                _data.CastTime
                / attackSpeed;


            _core.StopMovement();


            int executionId =
                _executionId;


            _castController.StartCast(
                finalCastTime,
                () =>
                {
                    if (executionId == _executionId)
                    {
                        FinishAttack(
                            target
                        );
                    }
                }
            );


            // Cast 완료 후 현재 실행이 유효한 경우
            // FinishAttack을 통해 실제 공격을 실행하고 Skill Action을 완료한다.
        }


        private void ExecuteDash(
            ICombatTarget target)
        {
            float dashDistance =
                _data.DashDistance;

            float dashSpeed =
                _data.DashSpeed;


            int executionId =
                _executionId;


            _dashController.StartDash(
                target,
                dashDistance,
                dashSpeed,
                () =>
                {
                    if (executionId == _executionId)
                    {
                        FinishAttack(
                            target
                        );
                    }
                }
            );


            // Dash는 일반 Movement와 분리해서 처리하며
            // MovementCompleted는 발생시키지 않는다.
            //
            // Dash 완료 후 현재 실행이 유효한 경우
            // FinishAttack을 통해 실제 공격을 실행하고 Skill Action을 완료한다.
        }


        // ============================================================
        // Attack
        // ============================================================

        private void ExecuteAttack(
            ICombatTarget target)
        {
            switch (_data.DeliveryType)
            {
                case ActiveSkillDeliveryType.Direct:

                    ExecuteDirect(
                        target
                    );

                    break;


                case ActiveSkillDeliveryType.Projectile:

                    ExecuteProjectile(
                        target
                    );

                    break;
            }
        }


        // ============================================================
        // Direct
        // ============================================================

        private void ExecuteDirect(
            ICombatTarget target)
        {
            switch (_data.AreaType)
            {
                case ActiveSkillAreaType.Single:

                    ExecuteSingle(
                        target
                    );

                    break;


                case ActiveSkillAreaType.TargetCircle:

                    ExecuteTargetCircle(
                        target
                    );

                    break;


                case ActiveSkillAreaType.SelfCircle:

                    ExecuteSelfCircle();

                    break;


                case ActiveSkillAreaType.SelfCone:

                    ExecuteSelfCone(
                        target
                    );

                    break;
            }
        }


        // ============================================================
        // Single
        // ============================================================

        private void ExecuteSingle(
            ICombatTarget target)
        {
            _singleTargetBuffer.Clear();


            _singleTargetBuffer.Add(
                target
            );


            RequestSkillEffects(
                _singleTargetBuffer
            );


            // TODO:
            // Skill FX 실행

            // TODO:
            // Hit FX 실행
        }


        // ============================================================
        // Target Circle
        // ============================================================

        private void ExecuteTargetCircle(
            ICombatTarget target)
        {
            if (_targetResolver == null)
                return;


            Vector2 center =
                target.Transform.position;


            TargetHitRequest hitRequest =
                new TargetHitRequest(
                    center,
                    Vector2.zero,
                    _data.AreaRadius,
                    0f,
                    _data.MaxEffectTargetCount,
                    HitAreaType.Circle,
                    target.Team,
                    _targetFilter
                );


            IReadOnlyList<ICombatTarget> targets =
                _targetResolver.ResolveHitTargets(
                    hitRequest
                );


            RequestSkillEffects(
                targets
            );


            // TODO:
            // Skill FX 실행

            // TODO:
            // Hit FX 실행
        }


        // ============================================================
        // Self Circle
        // ============================================================

        private void ExecuteSelfCircle()
        {
            if (_targetResolver == null)
                return;


            Vector2 center =
                _core.transform.position;


            TargetHitRequest hitRequest =
                new TargetHitRequest(
                    center,
                    Vector2.zero,
                    _data.AreaRadius,
                    0f,
                    _data.MaxEffectTargetCount,
                    HitAreaType.Circle,
                    GetTargetTeam(),
                    _targetFilter
                );


            IReadOnlyList<ICombatTarget> targets =
                _targetResolver.ResolveHitTargets(
                    hitRequest
                );


            RequestSkillEffects(
                targets
            );


            // TODO:
            // Skill FX 실행

            // TODO:
            // Hit FX 실행
        }


        // ============================================================
        // Self Cone
        // ============================================================

        private void ExecuteSelfCone(
            ICombatTarget target)
        {
            if (_targetResolver == null)
                return;


            Vector2 origin =
                _core.transform.position;


            Vector2 direction =
                (
                    (Vector2)target.Transform.position
                    - origin
                ).normalized;


            TargetHitRequest hitRequest =
                new TargetHitRequest(
                    origin,
                    direction,
                    _data.AreaRadius,
                    _data.AreaAngle,
                    _data.MaxEffectTargetCount,
                    HitAreaType.Cone,
                    GetTargetTeam(),
                    _targetFilter
                );


            IReadOnlyList<ICombatTarget> targets =
                _targetResolver.ResolveHitTargets(
                    hitRequest
                );


            RequestSkillEffects(
                targets
            );


            // TODO:
            // Skill FX 실행

            // TODO:
            // Hit FX 실행
        }


        // ============================================================
        // Projectile
        // ============================================================

        private void ExecuteProjectile(
            ICombatTarget target)
        {
            if (_targetResolver == null)
                return;


            ProjectileImpactType impactType =
                GetProjectileImpactType();

            // Projectile의 SelfCircle / SelfCone도 충돌 위치에서 범위를 판정한다.

            Vector2 origin =
                _core.transform.position;


            _projectileTargetBuffer.Clear();


            _projectileTargetBuffer.Add(
                target
            );


            if (_data.MaxTargetCount > 1)
            {
                TargetCandidateRequest candidateRequest =
                    new TargetCandidateRequest(
                        origin,
                        _data.SkillRange,
                        target.Team,
                        _targetFilter
                    );


                IReadOnlyList<ICombatTarget> candidates =
                    _targetResolver.ResolveCandidates(
                        candidateRequest
                    );


                for (int i = 0;
                     i < candidates.Count
                     && _projectileTargetBuffer.Count < _data.MaxTargetCount;
                     i++)
                {
                    ICombatTarget candidate =
                        candidates[i];


                    if (candidate == target)
                        continue;


                    _projectileTargetBuffer.Add(
                        candidate
                    );
                }
            }


            // 실제 명중 대상은 Projectile 충돌 시점에 확정한다.
            SkillEffectRequest skillEffectRequest =
                new SkillEffectRequest(
                    _core,
                    null,
                    _data.Effects
                );


            // 목표마다 1발씩 발사하고, 각 투사체의 효과 적용 인원은 별도로 제한한다.
            for (int i = 0;
                 i < _projectileTargetBuffer.Count;
                 i++)
            {
                ProjectileRequest request =
                    new ProjectileRequest(
                        _core,
                        _projectileTargetBuffer[i],
                        origin,
                        _data.ProjectileSpeed,
                        impactType,
                        _data.AreaRadius,
                        _data.AreaAngle,
                        impactType == ProjectileImpactType.Single
                            ? 1
                            : _data.MaxEffectTargetCount,
                        skillEffectRequest,
                        _targetFilter
                    );


                ProjectileManager.GetOrCreate().Fire(
                    request
                );
            }


            // 목표마다 생성된 ProjectileRequest를 ProjectileManager에 전달한다.
            // SkillEffectRequest는 발사 시점의 Skill Effect 정보를 보관하고,
            // 실제 효과 대상은 Projectile 충돌 시 ImpactType에 따라 확정된다.
            //
            // Single
            // → 충돌 대상을 SkillEffectRequest의 대상으로 사용
            //
            // Circle / Cone
            // → 충돌 위치 기준 범위 판정 후 SkillEffectRequest의 대상으로 사용


            // TODO:
            // Skill FX 실행
        }


        private ProjectileImpactType GetProjectileImpactType()
        {
            switch (_data.AreaType)
            {
                case ActiveSkillAreaType.Single:

                    return ProjectileImpactType.Single;


                case ActiveSkillAreaType.SelfCone:

                    return ProjectileImpactType.Cone;


                case ActiveSkillAreaType.TargetCircle:
                case ActiveSkillAreaType.SelfCircle:

                    return ProjectileImpactType.Circle;


                default:

                    return ProjectileImpactType.Single;
            }
        }


        // ============================================================
        // Target Team
        // ============================================================

        private UnitTeam GetTargetTeam()
        {
            switch (_data.TargetSide)
            {
                case SkillTargetRelation.Friendly:
                case SkillTargetRelation.Self:

                    return _core.Team;


                case SkillTargetRelation.Hostile:

                    return _core.Team == UnitTeam.Ally
                        ? UnitTeam.Enemy
                        : UnitTeam.Ally;


                default:

                    return _core.Team;
            }
        }


        // ============================================================
        // Skill Effect
        // ============================================================

        private void RequestSkillEffects(
            IReadOnlyList<ICombatTarget> targets)
        {
            if (targets == null)
                return;

            if (targets.Count <= 0)
                return;


            if (SkillEffectResolver.Instance == null)
            {
                Debug.LogError(
                    "[ActiveSkillExecutor] SkillEffectResolver가 존재하지 않습니다."
                );

                return;
            }


            for (int i = 0;
                 i < targets.Count;
                 i++)
            {
                ICombatTarget target =
                    targets[i];


                if (!CombatTargetUtility.IsValid(
                        target))
                {
                    continue;
                }


                SkillEffectRequest request =
                    new SkillEffectRequest(
                        _core,
                        target,
                        _data.Effects
                    );


                SkillEffectResolver.Instance.Resolve(
                    request
                );
            }
        }
    }
}