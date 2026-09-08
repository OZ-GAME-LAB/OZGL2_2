using Units.Skills;
using UnityEngine;



namespace Units
{
    public class BasicAttackExecutor
    {
        // ============================================================
        // References
        // ============================================================

        private readonly Unit_Core _core;


        // ============================================================
        // Data
        // ============================================================

        private readonly BasicAttackData _data;


        // ============================================================
        // Constructor
        // ============================================================

        public BasicAttackExecutor(
            Unit_Core core,
            BasicAttackData data)
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

            if (_data == null)
                return;

            if (target == null)
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
        }


        // ============================================================
        // Direct
        // ============================================================

        private void ExecuteDirect(
            GameObject target)
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
            GameObject target)
        {
            float damage =
                CalculateDamage();

            // TODO:
            // Damage 처리 Manager 구현 후
            // target에게 damage 적용

            // TODO:
            // Attack FX 실행

            // TODO:
            // Hit FX 실행
        }


        // ============================================================
        // Target Circle
        // ============================================================

        private void ExecuteTargetCircle(
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
            // TargetSearch / Damage 관련 Manager 구현 후
            //
            // center 기준 radius 범위 탐색
            // maxTargetCount만큼 대상 선정
            // 각 대상에게 damage 적용

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
            float damage =
                CalculateDamage();

            Vector3 center =
                _core.transform.position;

            float radius =
                _data.AreaRadius;

            int maxTargetCount =
                _data.MaxTargetCount;

            // TODO:
            // TargetSearch / Damage 관련 Manager 구현 후
            //
            // Unit 위치 기준 radius 범위 탐색
            // maxTargetCount만큼 대상 선정
            // 각 대상에게 damage 적용

            // TODO:
            // Attack FX 실행

            // TODO:
            // Hit FX 실행
        }


        // ============================================================
        // Self Cone
        // ============================================================

        private void ExecuteSelfCone(
            GameObject target)
        {
            float damage =
                CalculateDamage();

            Vector3 origin =
                _core.transform.position;

            Vector3 direction =
                (
                    target.transform.position
                    - origin
                ).normalized;

            float radius =
                _data.AreaRadius;

            float angle =
                _data.AreaAngle;

            int maxTargetCount =
                _data.MaxTargetCount;

            // TODO:
            // TargetSearch / Damage 관련 Manager 구현 후
            //
            // origin 기준
            // direction 방향으로
            // radius / angle 범위의 대상 탐색
            // maxTargetCount만큼 대상 선정
            // 각 대상에게 damage 적용

            // TODO:
            // Attack FX 실행

            // TODO:
            // Hit FX 실행
        }


        // ============================================================
        // Projectile
        // ============================================================

        private void ExecuteProjectile(
            GameObject target)
        {
            float damage =
                CalculateDamage();

            float projectileSpeed =
                _data.ProjectileSpeed;

            // TODO:
            // Projectile Manager 구현 후 실행 요청
            //
            // 전달해야 할 정보 예시:
            //
            // 공격자
            // target
            // damage
            // projectileSpeed
            // AreaType
            // AreaRadius
            // MaxTargetCount
            // HitFXType

            // TODO:
            // Attack FX 실행
        }


        // ============================================================
        // Damage
        // ============================================================

        private float CalculateDamage()
        {
            return
                _core.RuntimeStatus.AttackPower
                * _core.RuntimeStatus.BasicAttackMultiplier;
        }
    }
}