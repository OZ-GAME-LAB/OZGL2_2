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
            if (Instance != null
                && Instance != this)
            {
                Debug.LogError(
                    "[SkillEffectResolver] SkillEffectResolver가 중복 생성되어 자동 삭제됩니다."
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
            SkillEffectRequest request)
        {
            if (!IsValidRequest(
                    request))
            {
                return;
            }


            IReadOnlyList<SkillEffectData> effects =
                request.Effects;


            for (int i = 0;
                 i < effects.Count;
                 i++)
            {
                SkillEffectData effect =
                    effects[i];


                if (effect == null)
                    continue;


                ResolveEffect(
                    request,
                    effect
                );
            }
        }


        private void ResolveEffect(
            SkillEffectRequest request,
            SkillEffectData effect)
        {
            switch (effect)
            {
                case SkillDamageEffectData damageEffect:

                    ResolveDamage(
                        request,
                        damageEffect
                    );

                    break;


                case SkillHealEffectData healEffect:

                    ResolveHeal(
                        request,
                        healEffect
                    );

                    break;


                case SkillRuntimeEffectData runtimeEffect:

                    ResolveRuntimeEffect(
                        request,
                        runtimeEffect
                    );

                    break;
            }
        }


        // ============================================================
        // Damage
        // ============================================================

        private void ResolveDamage(
            SkillEffectRequest request,
            SkillDamageEffectData effect)
        {
            if (DamageResolver.Instance == null)
                return;


            ICombatTarget[] targets =
            {
                request.Target
            };


            DamageRequest damageRequest =
                new DamageRequest(
                    request.Caster,
                    targets,
                    DamageSourceType.Skill,
                    effect.DamageType,
                    effect.DamageMultiplier
                );


            DamageResolver.Instance.Resolve(
                damageRequest
            );
        }


        // ============================================================
        // Heal
        // ============================================================

        private void ResolveHeal(
            SkillEffectRequest request,
            SkillHealEffectData effect)
        {
            if (HealResolver.Instance == null)
                return;


            HealRequest healRequest =
                new HealRequest(
                    request.Caster,
                    request.Target,
                    effect.ScalingStatType,
                    effect.HealRatio
                );


            HealResolver.Instance.Resolve(
                healRequest
            );
        }


        // ============================================================
        // Runtime Effect
        // ============================================================

        private void ResolveRuntimeEffect(
            SkillEffectRequest request,
            SkillRuntimeEffectData effect)
        {
            if (RuntimeEffectManager.Instance == null)
                return;

            if (effect.EffectData == null)
                return;


            Unit_Gateway source =
                request.Caster.GetComponent<Unit_Gateway>();


            if (source == null)
                return;


            if (request.Target
                is not Unit_Gateway target)
            {
                return;
            }


            EffectRequest effectRequest =
                new EffectRequest(
                    effect.EffectData,
                    source,
                    target
                );


            RuntimeEffectManager.Instance.ApplyEffect(
                effectRequest
            );
        }


        // ============================================================
        // Validation
        // ============================================================

        private bool IsValidRequest(
            SkillEffectRequest request)
        {
            if (request.Caster == null)
                return false;

            if (request.Target == null)
                return false;

            if (!request.Caster.IsAlive)
                return false;

            if (!request.Target.IsTargetable)
                return false;

            if (request.Effects == null ||
                request.Effects.Count == 0)
            {
                return false;
            }


            return true;
        }
    }
}