using System.Collections.Generic;
using Units.Skills;
using UnityEngine;
using System;



namespace Units
{
    public class ActiveSkillExecutor
    {
        // ============================================================
        // Reference
        // ============================================================

        private readonly Unit_Core _core;

        private readonly HitTargetResolver _hitTargetResolver;


        // ============================================================
        // Data
        // ============================================================

        private readonly ActiveSkillData _data;


        // ============================================================
        // Action Controllers
        // ============================================================

        private readonly CastController _castController = new CastController();

        private readonly DashController _dashController;


        // ============================================================
        // Runtime State
        // ============================================================

        private Action _onCompleted;

        private ICombatTarget _target;

        private int _executionId;

        private bool _isExecuting;


        // ============================================================
        // Properties
        // ============================================================

        public bool IsCasting => _castController.IsCasting;

        public bool IsDashing => _dashController.IsDashing;


        // ============================================================
        // Runtime Buffer
        // ============================================================

        private readonly List<ICombatTarget> _singleTargetBuffer;


        // ============================================================
        // Constructor
        // ============================================================

        public ActiveSkillExecutor(
            Unit_Core core,
            ActiveSkillData data,
            HitTargetResolver hitTargetResolver)
        {
            _core =
                core;
            _dashController = new DashController(core);

            _data =
                data;

            _hitTargetResolver =
                hitTargetResolver;


            _singleTargetBuffer =
                new List<ICombatTarget>(
                    1
                );
        }


        // ============================================================
        // Execute
        // ============================================================

        public void Execute(
            ICombatTarget target,
            Action onCompleted)
        {
            Cancel();
            if (_core == null || _core.RuntimeStatus == null || _data == null
                || !CombatTargetUtility.IsValid(target))
            {
                onCompleted?.Invoke();
                return;
            }
            _target = target;
            _onCompleted = onCompleted;
            _isExecuting = true;
            switch (_data.ActionType)
            {
                case ActiveSkillActionType.Instant:

                    FinishAttack(target);

                    break;


                case ActiveSkillActionType.Cast:

                    ExecuteCast(
                        target
                    );

                    // TODO:
                    // 실제 Casting 완료 시
                    // ExecuteAttack(target)
                    // onCompleted.Invoke()

                    // 실제 Action 완료 Callback에서 공격 실행 및 완료를 처리한다.

                    break;


                case ActiveSkillActionType.Dash:

                    ExecuteDash(
                        target
                    );

                    // TODO:
                    // 실제 Dash 완료 시
                    // ExecuteAttack(target)
                    // onCompleted.Invoke()

                    // 실제 Action 완료 Callback에서 공격 실행 및 완료를 처리한다.

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
                _castController.Tick(deltaTime);
        }

        public void FixedTick(
            float deltaTime)
        {
            if (ValidateExecution())
                _dashController.FixedTick(deltaTime);
        }

        public void Cancel()
        {
            _executionId++;

            _isExecuting = false;

            _onCompleted = null;

            _target = null;

            _castController.Cancel();

            _dashController.Cancel();
        }

        private bool ValidateExecution()
        {
            if (!_isExecuting)
                return false;

            if (_core == null || !_core.isActiveAndEnabled || !_core.IsAlive)
            {
                CompleteExecution();

                return false;
            }

            if (!CombatTargetUtility.IsValid(_target))
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

            int executionId = _executionId;

            try
            {
                if (_core != null && _core.IsAlive && CombatTargetUtility.IsValid(target))
                    ExecuteAttack(target);
            }
            finally
            {
                if (executionId == _executionId)
                    CompleteExecution();
            }
        }

        private void CompleteExecution()
        {
            var callback = _onCompleted;

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
            int executionId = _executionId;
            _castController.StartCast(finalCastTime, () =>
            {
                if (executionId == _executionId) FinishAttack(target);
            });
            // 구현 완료: 아래 기존 TODO의 Cast 완료 후 공격 순서를 연결했다.


            // TODO:
            // Cast 처리 시스템 작성 후 연결
            //
            // finalCastTime 동안 Cast 후
            // ExecuteAttack(target) 실행
        }


        private void ExecuteDash(
            ICombatTarget target)
        {
            float dashDistance =
                _data.DashDistance;

            float dashSpeed =
                _data.DashSpeed;
            int executionId = _executionId;
            _dashController.StartDash(target, dashDistance, dashSpeed, () =>
            {
                if (executionId == _executionId) FinishAttack(target);
            });
            // 구현 완료: 일반 이동과 분리된 Dash를 사용하며 MovementCompleted는 발생시키지 않는다.


            // TODO:
            // Dash 실행 시스템 작성 후 연결
            //
            // Dash 완료 또는 적절한 타이밍에
            // ExecuteAttack(target) 실행
        }


        // ============================================================
        // Attack
        // ============================================================

        private void ExecuteAttack(
            ICombatTarget target)
        {
            switch (_data.AttackType)
            {
                case ActiveSkillAttackType.Direct:

                    ExecuteDirect(
                        target
                    );

                    break;


                case ActiveSkillAttackType.ProjectileSingle:
                case ActiveSkillAttackType.ProjectileArea:

                    ExecuteProjectile(
                        target
                    );

                    break;


                case ActiveSkillAttackType.TargetArea:

                    ExecuteTargetArea(
                        target
                    );

                    break;


                case ActiveSkillAttackType.SelfArea:

                    ExecuteSelfArea();

                    break;
            }
        }


        // ============================================================
        // Direct
        // ============================================================

        private void ExecuteDirect(
            ICombatTarget target)
        {
            _singleTargetBuffer.Clear();


            _singleTargetBuffer.Add(
                target
            );


            RequestDamage(
                _singleTargetBuffer
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
            if (_hitTargetResolver == null)
                return;


            ProjectileImpactType impactType =
                _data.AttackType == ActiveSkillAttackType.ProjectileArea
                    ? ProjectileImpactType.Circle
                    : ProjectileImpactType.Single;

            // 범위 여부는 AttackType으로 구분하며, 반지름으로 추론하지 않는다.

            Vector2 origin =
                _core.transform.position;

            IReadOnlyList<ICombatTarget> targets =
                _hitTargetResolver.ResolveAttackTargets(
                    origin,
                    _data.SkillRange,
                    _data.MaxTargetCount,
                    target
                );


            // 목표마다 1발씩 발사하고, 각 투사체의 광역 피해 인원은 별도로 제한한다.
            for (int i = 0; i < targets.Count; i++)
            {
                ProjectileRequest request =
                    new ProjectileRequest(
                        _core,
                        targets[i],
                        origin,
                        _data.ProjectileSpeed,
                        impactType,
                        _data.AreaRadius,
                        _data.AreaAngle,
                        impactType == ProjectileImpactType.Single ? 1 : _data.MaxDamageableCount,
                        DamageSourceType.Skill,
                        _core.RuntimeStatus.SkillDamageMultiplier
                    );

                ProjectileManager.GetOrCreate().Fire(
                    request
                );
            }

            // 아래 기존 TODO의 대상 수는 이제 MaxDamageableCount로 전달한다.

            // TODO:
            // Projectile Manager 구현 후 실행 요청
            //
            // Projectile 충돌 시:
            //
            // Single
            // → 충돌 대상 DamageRequest
            //
            // Area
            // → 충돌 위치 기준 HitTargetResolver
            // → DamageRequest
            //
            // 전달 데이터:
            // Attacker
            // Target
            // ProjectileSpeed
            // AreaRadius
            // MaxTargetCount
            // HitFXType
        }


        // ============================================================
        // Target Area
        // ============================================================

        private void ExecuteTargetArea(
            ICombatTarget target)
        {
            if (_hitTargetResolver == null)
                return;


            Vector2 center =
                target.Transform.position;


            TargetHitRequest hitRequest =
                new TargetHitRequest(
                    center,
                    Vector2.zero,
                    _data.AreaRadius,
                    0f,
                    _data.MaxDamageableCount,
                    HitAreaType.Circle
                );


            IReadOnlyList<ICombatTarget> targets =
                _hitTargetResolver.Resolve(
                    hitRequest
                );


            RequestDamage(
                targets
            );


            // TODO:
            // Skill FX 실행

            // TODO:
            // Hit FX 실행
        }


        // ============================================================
        // Self Area
        // ============================================================

        private void ExecuteSelfArea()
        {
            if (_hitTargetResolver == null)
                return;


            Vector2 center =
                _core.transform.position;


            TargetHitRequest hitRequest =
                new TargetHitRequest(
                    center,
                    Vector2.zero,
                    _data.AreaRadius,
                    0f,
                    _data.MaxDamageableCount,
                    HitAreaType.Circle
                );


            IReadOnlyList<ICombatTarget> targets =
                _hitTargetResolver.Resolve(
                    hitRequest
                );


            RequestDamage(
                targets
            );


            // TODO:
            // Skill FX 실행

            // TODO:
            // Hit FX 실행
        }


        // ============================================================
        // Damage
        // ============================================================

        private void RequestDamage(
            IReadOnlyList<ICombatTarget> targets)
        {
            if (targets == null)
                return;

            if (targets.Count <= 0)
                return;


            if (DamageResolver.Instance == null)
            {
                Debug.LogError(
                    "[ActiveSkillExecutor] DamageResolver가 존재하지 않습니다."
                );

                return;
            }


            DamageRequest request =
                new DamageRequest(
                    _core,
                    targets,
                    DamageSourceType.Skill,
                    _core.RuntimeStatus.SkillDamageMultiplier
                );


            DamageResolver.Instance.Resolve(
                request
            );
        }
    }
}
