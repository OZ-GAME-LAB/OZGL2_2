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
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[HealResolver] HealResolver가 중복 생성되어 자동 삭제됩니다.");

                Destroy(gameObject);

                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }


        // ============================================================
        // Resolve
        // ============================================================

        public void Resolve(HealRequest request)
        {
            ResolveWithResult(request);
        }

        public CombatApplicationResult ResolveWithResult(HealRequest request)
        {
            if (!IsValidRequest(request) || !request.TargetSnapshot.IsTargetable || (request.SourceSnapshot == null && !request.Metadata.Owner.MatchesLifetime))
                return CombatApplicationResult.Invalid(
                    CombatApplicationKind.Heal,
                    request.Target,
                    request.Metadata,
                    "Invalid heal request"
                );

            return request.Target.HealWithResult(CalculateHeal(request), request.Metadata);
        }

        public void ResolveDotHeal(DotHealRequest request)
        {
            ResolveDotHealWithResult(request);
        }

        public CombatApplicationResult ResolveDotHealWithResult(DotHealRequest request)
        {
            if (!IsValidDotHealRequest(request) || !request.TargetSnapshot.IsTargetable)
                return CombatApplicationResult.Invalid(
                    CombatApplicationKind.Heal,
                    request.Target,
                    request.Metadata,
                    "Invalid periodic heal request"
                );

            return request.Target.HealWithResult(CalculateDotHeal(request), request.Metadata);
        }


        // ============================================================
        // Validation
        // ============================================================

        private bool IsValidRequest(HealRequest request)
        {
            if (request.Healer == null)
                return false;

            if (request.SourceSnapshot == null && request.Healer.RuntimeStatus == null)
                return false;

            if (request.Target == null)
                return false;

            if (request.Target.RuntimeStatus == null)
                return false;

            if (!request.Target.IsTargetable)
                return false;

            if (request.Target.Team != request.Metadata.Owner.Team)
            {
                return false;
            }

            if (request.HealRatio <= 0f)
                return false;

            return true;
        }

        private bool IsValidDotHealRequest(DotHealRequest request)
        {
            if (request.Healer == null)
                return false;

            if (request.Target == null)
                return false;

            if (request.Target.RuntimeStatus == null)
                return false;

            if (!request.Target.IsTargetable)
                return false;

            if (request.Target.Team != request.Metadata.Owner.Team)
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

        private float CalculateHeal(HealRequest request)
        {
            float scalingValue = GetScalingValue(request);

            if (scalingValue <= 0f)
                return 0f;

            float healingMultiplier = (request.SourceSnapshot?.HealingMultiplier ?? request.Healer.RuntimeStatus.HealingMultiplier);

            float healingTakenMultiplier = request.Target.RuntimeStatus.HealingTakenMultiplier;

            float finalHeal = scalingValue * request.HealRatio * healingMultiplier * healingTakenMultiplier;

            return Mathf.Max(0f, finalHeal);
        }

        private float CalculateDotHeal(DotHealRequest request)
        {
            float healingTakenMultiplier = request.Target.RuntimeStatus.HealingTakenMultiplier;

            float finalHeal = request.HealAmount * healingTakenMultiplier;

            return Mathf.Max(0f, finalHeal);
        }

        private float GetScalingValue(HealRequest request)
        {
            switch (request.ScalingStatType)
            {
                case HealScalingStatType.MaxHp:
                    return (request.SourceSnapshot?.MaxHp ?? request.Healer.RuntimeStatus.MaxHp);

                case HealScalingStatType.AttackPower:
                    return (request.SourceSnapshot?.AttackPower ?? request.Healer.RuntimeStatus.AttackPower);

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
            target.Heal(healAmount);
        }
    }
}