using System.Collections.Generic;
using Units.Skills;
using UnityEngine;
using System;


namespace Units
{
    public class BasicAttackExecutor
    {
        // ============================================================
        // References
        // ============================================================

        private readonly Unit_Core _core;

        private readonly HitTargetResolver _hitTargetResolver;


        // ============================================================
        // Data
        // ============================================================

        private readonly BasicAttackData _data;


        // ============================================================
        // Runtime Buffer
        // ============================================================

        private readonly List<ICombatTarget> _singleTargetBuffer;


        // ============================================================
        // Constructor
        // ============================================================

        public BasicAttackExecutor(
            Unit_Core core,
            BasicAttackData data,
            HitTargetResolver hitTargetResolver)
        {
            _core =
                core;

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
            if (_core == null)
                return;

            if (_data == null)
                return;

            if (target == null)
                return;

            if (!target.IsTargetable)
                return;


            switch (_data.ExecutionType)
            {
                case BasicAttackExecutionType.Direct:

                    ExecuteDirect(
                        target
                    );

                    break;


                case BasicAttackExecutionType.Projectile:

                    ExecuteProjectile(
                        target
                    );

                    break;
            }


            onCompleted?.Invoke();
        }


        // ============================================================
        // Direct
        // ============================================================

        private void ExecuteDirect(
            ICombatTarget target)
        {
            switch (_data.AreaType)
            {
                case BasicAttackAreaType.Single:

                    ExecuteSingle(
                        target
                    );

                    break;


                case BasicAttackAreaType.TargetCircle:

                    ExecuteTargetCircle(
                        target
                    );

                    break;


                case BasicAttackAreaType.SelfCircle:

                    ExecuteSelfCircle();

                    break;


                case BasicAttackAreaType.SelfCone:

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


            RequestDamage(
                _singleTargetBuffer
            );


            // TODO:
            // Attack FX 실행

            // TODO:
            // Hit FX 실행
        }


        // ============================================================
        // Target Circle
        // ============================================================

        private void ExecuteTargetCircle(
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
                    _data.MaxTargetCount,
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
            // Attack FX 실행

            // TODO:
            // Hit FX 실행
        }


        // ============================================================
        // Self Circle
        // ============================================================

        private void ExecuteSelfCircle()
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
                    _data.MaxTargetCount,
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
            // Attack FX 실행

            // TODO:
            // Hit FX 실행
        }


        // ============================================================
        // Self Cone
        // ============================================================

        private void ExecuteSelfCone(
            ICombatTarget target)
        {
            if (_hitTargetResolver == null)
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
                    _data.MaxTargetCount,
                    HitAreaType.Cone
                );


            IReadOnlyList<ICombatTarget> targets =
                _hitTargetResolver.Resolve(
                    hitRequest
                );


            RequestDamage(
                targets
            );


            // TODO:
            // Attack FX 실행

            // TODO:
            // Hit FX 실행
        }


        // ============================================================
        // Projectile
        // ============================================================

        private void ExecuteProjectile(
            ICombatTarget target)
        {
            float projectileSpeed =
                _data.ProjectileSpeed;


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
            // 전달할 데이터:
            // Attacker
            // Target
            // ProjectileSpeed
            // AreaType
            // AreaRadius
            // AreaAngle
            // MaxTargetCount
            // HitFXType


            // TODO:
            // Attack FX 실행
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
                    "[BasicAttackExecutor] DamageResolver가 존재하지 않습니다."
                );

                return;
            }


            DamageRequest request =
                new DamageRequest(
                    _core,
                    targets,
                    DamageSourceType.BasicAttack,
                    _core.RuntimeStatus.BasicAttackMultiplier
                );


            DamageResolver.Instance.Resolve(
                request
            );
        }
    }
}