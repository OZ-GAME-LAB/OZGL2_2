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


        // ============================================================
        // Data
        // ============================================================

        private readonly ActiveSkillData _data;


        // ============================================================
        // Constructor
        // ============================================================

        public ActiveSkillExecutor(
            Unit_Core core,
            ActiveSkillData data)
        {
            _core =
                core;

            _data =
                data;
        }


        // ============================================================
        // Execute
        // ============================================================

        public void Execute(
            GameObject target)
        {
            if (_core == null)
                return;

            if (_core.RuntimeStatus == null)
                return;

            if (_data == null)
                return;

            if (target == null)
                return;


            switch (_data.ActionType)
            {
                case ActiveSkillActionType.Instant:

                    ExecuteAttack(
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
            }
        }


        // ============================================================
        // Action
        // ============================================================

        private void ExecuteCast(
            GameObject target)
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
            GameObject target)
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
            GameObject target)
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


        private void ExecuteDirect(
            GameObject target)
        {
            float damage =
                CalculateDamage();


            // TODO:
            // Damage Manager 작성 후 연결


            // TODO:
            // Skill FX


            // TODO:
            // Hit FX
        }


        private void ExecuteProjectile(
            GameObject target)
        {
            float damage =
                CalculateDamage();

            float projectileSpeed =
                _data.ProjectileSpeed;


            // TODO:
            // Projectile Manager 작성 후 연결
        }


        private void ExecuteTargetArea(
            GameObject target)
        {
            float damage =
                CalculateDamage();

            Vector3 center =
                target.transform.position;

            float radius =
                _data.AreaRadius;

            int maxTargetCount =
                _data.MaxTargetCount;


            // TODO:
            // 범위 탐색 Manager 작성 후 연결


            // TODO:
            // Damage 처리


            // TODO:
            // FX 처리
        }


        private void ExecuteSelfArea()
        {
            float damage =
                CalculateDamage();

            Vector3 center =
                _core.transform.position;

            float radius =
                _data.AreaRadius;

            int maxTargetCount =
                _data.MaxTargetCount;


            // TODO:
            // 범위 탐색 Manager 작성 후 연결


            // TODO:
            // Damage 처리


            // TODO:
            // FX 처리
        }


        // ============================================================
        // Damage
        // ============================================================

        private float CalculateDamage()
        {
            return
                _core.RuntimeStatus.AttackPower
                * _core.RuntimeStatus.SkillDamageMultiplier;
        }
    }
}