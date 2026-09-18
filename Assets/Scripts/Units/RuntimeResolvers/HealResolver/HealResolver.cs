using Units.Skills;
using UnityEngine;

namespace Units
{
    public class HealResolver : MonoBehaviour
    {
        // ============================================================
        // Singleton
        // ============================================================

        public static HealResolver Instance { get; private set; }


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Awake()
        {
            if (Instance != null
                && Instance != this)
            {
                Debug.LogError(
                    "[HealResolver] HealResolver가 중복 생성되어 자동 삭제됩니다."
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
            HealRequest request)
        {
            if (!IsValidRequest(
                request))
            {
                return;
            }


            float healAmount =
                CalculateHeal(
                    request
                );


            if (healAmount <= 0f)
                return;


            ApplyHeal(
                request.Target,
                healAmount
            );
        }


        public void ResolveDotHeal(
            DotHealRequest request)
        {
            if (!IsValidDotHealRequest(
                request))
            {
                return;
            }


            ApplyHeal(
                request.Target,
                request.HealAmount
            );
        }


        // ============================================================
        // Validation
        // ============================================================

        private bool IsValidRequest(
            HealRequest request)
        {
            if (request.Healer == null)
                return false;

            if (request.Healer.RuntimeStatus == null)
                return false;

            if (request.Target == null)
                return false;

            if (!request.Target.IsTargetable)
                return false;

            if (request.Target.Team
                != request.Healer.Team)
            {
                return false;
            }

            if (request.HealRatio <= 0f)
                return false;


            return true;
        }


        private bool IsValidDotHealRequest(
            DotHealRequest request)
        {
            if (request.Healer == null)
                return false;

            if (request.Target == null)
                return false;

            if (!request.Target.IsTargetable)
                return false;

            if (request.Target.Team
                != request.Healer.Team)
            {
                return false;
            }

            if (request.HealAmount <= 0f)
                return false;


            return true;
        }


        // ============================================================
        // Calculation
        // ============================================================

        private float CalculateHeal(
            HealRequest request)
        {
            float scalingValue =
                GetScalingValue(
                    request
                );


            if (scalingValue <= 0f)
                return 0f;


            float healRatio =
                request.HealRatio
                / 100f;


            float skillDamageMultiplier =
                request.Healer
                    .RuntimeStatus
                    .SkillDamageMultiplier;


            float finalHeal =
                scalingValue
                * healRatio
                * skillDamageMultiplier;


            return Mathf.Max(
                0f,
                finalHeal
            );
        }


        private float GetScalingValue(
            HealRequest request)
        {
            switch (request.ScalingStatType)
            {
                case HealScalingStatType.MaxHp:

                    return request.Healer
                        .RuntimeStatus
                        .MaxHp;


                case HealScalingStatType.AttackPower:

                    return request.Healer
                        .RuntimeStatus
                        .AttackPower;


                default:

                    return 0f;
            }
        }


        // ============================================================
        // Apply
        // ============================================================

        private void ApplyHeal(
            ICombatTarget target,
            float healAmount)
        {
            target.Heal(
                healAmount
            );
        }
    }
}