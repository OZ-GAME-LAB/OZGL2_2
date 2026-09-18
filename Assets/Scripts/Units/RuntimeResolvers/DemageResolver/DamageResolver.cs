using UnityEngine;



namespace Units
{
    public class DamageResolver : MonoBehaviour
    {
        // ============================================================
        // Singleton
        // ============================================================

        public static DamageResolver Instance { get; private set; }


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Awake()
        {
            if (Instance != null
                && Instance != this)
            {
                Debug.LogError(
                    "[DamageResolver] DamageResolver가 중복 생성되어 자동 삭제됩니다."
                );

                Destroy(
                    gameObject
                );

                return;
            }


            Instance =
                this;
        }


        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance =
                    null;
            }
        }


        // ============================================================
        // Resolve
        // ============================================================

        public void Resolve(
            DamageRequest request)
        {
            if (!IsValidRequest(
                request))
            {
                return;
            }


            float damage =
                CalculateDamage(
                    request
                );


            if (damage <= 0f)
                return;


            ApplyDamage(
                request,
                damage
            );
        }


        public void ResolveDotDamage(
            DotDamageRequest request)
        {
            if (!IsValidDotDamageRequest(
                    request))
            {
                return;
            }

            DamageResult result =
                new DamageResult(
                    request.Attacker,
                    request.Target,
                    request.Damage,
                    request.SourceType
                );

            request.Target.TakeDamage(
                result
            );
        }


        // ============================================================
        // Validation
        // ============================================================

        private bool IsValidRequest(
            DamageRequest request)
        {
            if (request.Attacker == null)
                return false;


            if (request.Attacker.RuntimeStatus == null)
                return false;


            if (request.Targets == null)
                return false;


            if (request.Targets.Count <= 0)
                return false;


            if (request.DamageMultiplier <= 0f)
                return false;


            return true;
        }


        private bool IsValidTarget(
            DamageRequest request,
            ICombatTarget target)
        {
            if (target == null)
                return false;


            if (!target.IsTargetable)
                return false;


            if (target.Team ==
                request.Attacker.Team)
            {
                return false;
            }


            return true;
        }


        private bool IsValidDotDamageRequest(
           DotDamageRequest request)
        {
            if (request.Attacker == null)
                return false;

            if (request.Target == null)
                return false;

            if (!request.Target.IsTargetable)
                return false;

            if (request.Target.Team ==
                request.Attacker.Team)
            {
                return false;
            }

            if (request.Damage <= 0f)
                return false;

            return true;
        }


        // ============================================================
        // Damage Calculation
        // ============================================================

        private float CalculateDamage(
            DamageRequest request)
        {
            float attackPower =
                request.Attacker
                    .RuntimeStatus
                    .AttackPower;


            float damage =
                attackPower
                * request.DamageMultiplier;


            return Mathf.Max(
                0f,
                damage
            );
        }


        // ============================================================
        // Damage Apply
        // ============================================================

        private void ApplyDamage(
            DamageRequest request,
            float damage)
        {
            for (int i = 0;
                i < request.Targets.Count;
                i++)
            {
                ICombatTarget target =
                    request.Targets[i];


                if (!IsValidTarget(
                    request,
                    target))
                {
                    continue;
                }


                DamageResult result =
                    new DamageResult(
                        request.Attacker,
                        target,
                        damage,
                        request.SourceType
                    );


                target.TakeDamage(
                    result
                );
            }
        }
    }
}