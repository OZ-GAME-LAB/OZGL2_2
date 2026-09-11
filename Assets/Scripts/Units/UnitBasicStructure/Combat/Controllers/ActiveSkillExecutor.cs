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


            switch (_data.ActionType)
            {
                case ActiveSkillActionType.Instant:

                    ExecuteAttack(
                        target
                    );

                    onCompleted?.Invoke();

                    break;


                case ActiveSkillActionType.Cast:

                    ExecuteCast(
                        target
                    );

                    // TODO:
                    // 실제 Casting 완료 시
                    // ExecuteAttack(target)
                    // onCompleted.Invoke()

                    onCompleted?.Invoke();

                    break;


                case ActiveSkillActionType.Dash:

                    ExecuteDash(
                        target
                    );

                    // TODO:
                    // 실제 Dash 완료 시
                    // ExecuteAttack(target)
                    // onCompleted.Invoke()

                    onCompleted?.Invoke();

                    break;
            }
        }


        // ============================================================
        // Action
        // ============================================================

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


                case ActiveSkillAttackType.Projectile:

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