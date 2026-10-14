using System.Collections.Generic;
using Units.Effects;
using UnityEngine;


namespace Units.Skills
{
    public class SkillEffectResolver : MonoBehaviour
    {
        // ============================================================
        // Singleton
        // ============================================================

        public static SkillEffectResolver Instance { get; private set; }


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[SkillEffectResolver] SkillEffectResolver가 중복 생성되어 자동 삭제됩니다.");

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

        public void Resolve(SkillEffectRequest request)
        {
            ResolveWithResults(request);
        }

        public IReadOnlyList<CombatApplicationResult> ResolveWithResults(SkillEffectRequest request)
        {
            var results = new List<CombatApplicationResult>();

            if (!IsValidRequest(request) || !request.TargetSnapshot.IsTargetable || (request.SourceSnapshot == null && !request.Metadata.Owner.MatchesLifetime))
            {
                results.Add(CombatApplicationResult.Invalid(CombatApplicationKind.RuntimeEffect, request.Target, request.Metadata, "Invalid skill effect request"));

                return results.AsReadOnly();
            }

            var effects = new List<SkillEffectData>(request.Effects);

            using (CombatEventContext.Enter(request.Metadata))
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    if (request.CanContinue != null && !request.CanContinue())
                        break;

                    if (effects[i] == null)
                        continue;

                    if (!request.TargetSnapshot.IsTargetable || (request.SourceSnapshot == null && !request.Metadata.Owner.IsTargetable))
                    {
                        results.Add(CombatApplicationResult.Invalid(CombatApplicationKind.RuntimeEffect, request.Target, request.Metadata, "Target or caster lifetime changed"));

                        continue;
                    }

                    var result = ResolveEffect(request, effects[i]);
                    results.Add(result);
                    if (result.WasApplied && effects[i].ApplicationFX != null)
                        foreach (var fx in effects[i].ApplicationFX)
                            if (fx != null)
                            {
                                var playback = new SkillFXRequest(fx, request.Metadata, result.Target.Position,
                                    target: result.Target);
                                Units.FX.UnitFXBridge.Dispatch(playback);
                                // 효과 요청은 별도 실행 수명이 없으므로 종료를 함께 전달한다.
                                // 독립 재생은 대상에서 분리되어 자체 수명까지 유지한다.
                                Units.FX.UnitFXBridge.Dispatch(playback.AsCleanup());
                            }
                }
            }

            return results.AsReadOnly();
        }

        private CombatApplicationResult ResolveEffect(
            SkillEffectRequest request,
            SkillEffectData effect)
        {
            switch (effect)
            {
                case SkillDamageEffectData damageEffect:
                    return ResolveDamage(request, damageEffect);

                case SkillHealEffectData healEffect:
                    return ResolveHeal(request, healEffect);

                case SkillShieldEffectData shieldEffect:
                    return ResolveShield(request, shieldEffect);

                case SkillRuntimeEffectData runtimeEffect:
                    return ResolveRuntimeEffect(request, runtimeEffect);

                default:
                    return CombatApplicationResult.Invalid(
                        CombatApplicationKind.RuntimeEffect,
                        request.Target,
                        request.Metadata,
                        "Unsupported effect type"
                    );
            }
        }


        // ============================================================
        // Damage
        // ============================================================

        private CombatApplicationResult ResolveDamage(
            SkillEffectRequest request,
            SkillDamageEffectData effect)
        {
            if (DamageResolver.Instance == null)
                return CombatApplicationResult.Invalid(
                    CombatApplicationKind.Damage,
                    request.Target,
                    request.Metadata,
                    "DamageResolver unavailable"
                );

            var results = DamageResolver.Instance.ResolveWithResults(new DamageRequest(request.Caster, new[] { request.Target }, DamageSourceType.Skill, effect.DamageType, effect.DamageMultiplier, request.Metadata, request.SourceSnapshot));

            return results[0];
        }


        // ============================================================
        // Heal
        // ============================================================

        private CombatApplicationResult ResolveHeal(
            SkillEffectRequest request,
            SkillHealEffectData effect)
        {
            if (HealResolver.Instance == null)
                return CombatApplicationResult.Invalid(
                    CombatApplicationKind.Heal,
                    request.Target,
                    request.Metadata,
                    "HealResolver unavailable"
                );

            return HealResolver.Instance.ResolveWithResult(new HealRequest(request.Caster, request.Target, effect.ScalingStatType, effect.HealRatio, request.Metadata, request.SourceSnapshot));
        }


        // ============================================================
        // Shield
        // ============================================================

        private CombatApplicationResult ResolveShield(
            SkillEffectRequest request,
            SkillShieldEffectData effect)
        {
            if (ShieldResolver.Instance == null)
                return CombatApplicationResult.Invalid(
                    CombatApplicationKind.Shield,
                    request.Target,
                    request.Metadata,
                    "ShieldResolver unavailable"
                );

            return ShieldResolver.Instance.ResolveWithResult(new ShieldRequest(request.Caster, request.Target, effect.ScalingStatType, effect.ShieldRatio, request.Metadata, request.SourceSnapshot));
        }


        // ============================================================
        // Runtime Effect
        // ============================================================

        private CombatApplicationResult ResolveRuntimeEffect(
            SkillEffectRequest request,
            SkillRuntimeEffectData effect)
        {
            if (RuntimeEffectManager.Instance == null || effect.EffectData == null)
                return CombatApplicationResult.Invalid(
                    CombatApplicationKind.RuntimeEffect,
                    request.Target,
                    request.Metadata,
                    "Runtime effect unavailable"
                );

            return RuntimeEffectManager.Instance.ApplyEffectWithResult(new EffectRequest(effect.EffectData, request.Caster, request.Target, request.Metadata, effect.FrozenDefinition));
        }


        // ============================================================
        // Validation
        // ============================================================

        private bool IsValidRequest(SkillEffectRequest request)
        {
            if (request.Caster == null)
                return false;

            if (request.Target == null)
                return false;

            if (request.SourceSnapshot == null && !request.Caster.IsAlive)
                return false;

            if (!request.Target.IsTargetable)
                return false;

            if (request.Effects == null || request.Effects.Count == 0)
            {
                return false;
            }

            return true;
        }
    }
}
