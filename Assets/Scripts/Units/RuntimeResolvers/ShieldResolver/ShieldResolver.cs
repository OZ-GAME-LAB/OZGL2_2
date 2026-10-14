using Units.Skills;
using UnityEngine;

namespace Units
{
    public class ShieldResolver : MonoBehaviour
    {
        // ============================================================
        // Singleton
        // ============================================================

        public static ShieldResolver Instance { get; private set; }


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[ShieldResolver] ShieldResolver가 중복 생성되어 자동 삭제됩니다.");

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

        public void Resolve(ShieldRequest request)
        {
            ResolveWithResult(request);
        }

        public CombatApplicationResult ResolveWithResult(ShieldRequest request)
        {
            if (!IsValidRequest(request) || !request.TargetSnapshot.IsTargetable || (request.SourceSnapshot == null && !request.Metadata.Owner.MatchesLifetime))
                return CombatApplicationResult.Invalid(
                    CombatApplicationKind.Shield,
                    request.Target,
                    request.Metadata,
                    "Invalid shield request"
                );

            return request.Target.AddShieldWithResult(CalculateShield(request), request.Metadata);
        }


        // ============================================================
        // Validation
        // ============================================================

        private bool IsValidRequest(ShieldRequest request)
        {
            if (request.Caster == null)
                return false;

            if (request.SourceSnapshot == null && request.Caster.RuntimeStatus == null)
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

            if (request.ShieldRatio <= 0f)
                return false;

            return true;
        }


        // ============================================================
        // Calculation
        // ============================================================

        private float CalculateShield(ShieldRequest request)
        {
            float scalingValue = GetScalingValue(request);

            if (scalingValue <= 0f)
                return 0f;

            float healingMultiplier = (request.SourceSnapshot?.HealingMultiplier ?? request.Caster.RuntimeStatus.HealingMultiplier);

            float healingTakenMultiplier = request.Target.RuntimeStatus.HealingTakenMultiplier;

            float finalShield = scalingValue * request.ShieldRatio * healingMultiplier * healingTakenMultiplier;

            return Mathf.Max(0f, finalShield);
        }

        private float GetScalingValue(ShieldRequest request)
        {
            switch (request.ScalingStatType)
            {
                case ShieldScalingStatType.MaxHp:
                    return (request.SourceSnapshot?.MaxHp ?? request.Caster.RuntimeStatus.MaxHp);

                case ShieldScalingStatType.AttackPower:
                    return (request.SourceSnapshot?.AttackPower ?? request.Caster.RuntimeStatus.AttackPower);

                default:
                    return 0f;
            }
        }


        // ============================================================
        // Apply
        // ============================================================

        private void ApplyShield(
            ICombatTarget target,
            float shieldAmount)
        {
            target.AddShield(shieldAmount);
        }
    }
}