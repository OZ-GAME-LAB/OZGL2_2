using System.Collections.Generic;
using UnityEngine;


namespace Units.Effects
{
    public class RuntimeEffectManager : MonoBehaviour
    {
        // ============================================================
        // Singleton
        // ============================================================

        public static RuntimeEffectManager Instance { get; private set; }


        // ============================================================
        // Settings
        // ============================================================

        private const float UpdateInterval =
            0.1f;

        private const int MaxTickProcessCount =
            20;


        // ============================================================
        // Data
        // ============================================================

        private readonly List<RuntimeEffectInstance> _activeEffects =
            new();


        // ============================================================
        // Runtime
        // ============================================================

        private float _updateTimer;


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Awake()
        {
            if (Instance != null &&
                Instance != this)
            {
                Destroy(gameObject);

                return;
            }

            Instance =
                this;
        }


        private void Update()
        {
            if (_activeEffects.Count == 0)
            {
                _updateTimer =
                    0f;

                return;
            }

            _updateTimer +=
                Time.deltaTime;

            if (_updateTimer <
                UpdateInterval)
            {
                return;
            }

            _updateTimer =
                0f;


            float currentTime =
                Time.time;

            UpdateActiveEffects(
                currentTime
            );
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
        // Apply
        // ============================================================

        public bool ApplyEffect(
            EffectRequest request)
        {
            if (!ValidateRequest(
                    request))
            {
                return false;
            }

            float currentTime =
                Time.time;

            RuntimeEffectInstance existingEffect =
                FindEffect(
                    request.Target,
                    request.EffectData
                );

            if (existingEffect != null)
            {
                ApplyExistingEffect(
                    existingEffect,
                    currentTime
                );

                return true;
            }

            RuntimeEffectInstance newEffect =
                new RuntimeEffectInstance(
                    request.EffectData,
                    request.Source,
                    request.Target,
                    currentTime
                );


            if (!RegisterEffect(
                    newEffect))
            {
                return false;
            }

            return true;
        }


        // ============================================================
        // Remove
        // ============================================================

        public bool RemoveEffect(
            RuntimeEffectInstance instance)
        {
            if (instance == null)
                return false;

            int index =
                _activeEffects.IndexOf(
                    instance
                );

            if (index < 0)
                return false;

            RemoveEffectAt(
                index
            );

            return true;
        }


        public void RemoveEffects(
            Unit_Gateway target)
        {
            if (target == null)
                return;

            for (int i = _activeEffects.Count - 1;
                 i >= 0;
                 i--)
            {
                RuntimeEffectInstance instance =
                    _activeEffects[i];

                if (instance.Target != target)
                    continue;

                RemoveEffectAt(
                    i
                );
            }
        }


        public void ClearAllEffects()
        {
            for (int i = _activeEffects.Count - 1;
                 i >= 0;
                 i--)
            {
                RemoveEffectAt(
                    i
                );
            }
        }


        // ============================================================
        // Update
        // ============================================================

        private void UpdateActiveEffects(
            float currentTime)
        {
            for (int i = _activeEffects.Count - 1;
                 i >= 0;
                 i--)
            {
                RuntimeEffectInstance instance =
                    _activeEffects[i];

                if (!IsValidInstance(
                        instance))
                {
                    RemoveEffectAt(
                        i
                    );

                    continue;
                }

                ProcessPeriodicTicks(
                    instance,
                    currentTime
                );

                if (instance.IsExpired(
                        currentTime))
                {
                    RemoveEffectAt(
                        i
                    );
                }
            }
        }


        // ============================================================
        // Existing Effect
        // ============================================================

        private void ApplyExistingEffect(
            RuntimeEffectInstance instance,
            float currentTime)
        {
            EffectStackType stackType =
                instance.Data.StackType;

            switch (stackType)
            {
                case EffectStackType.None:
                    break;

                case EffectStackType.Refresh:
                    instance.RefreshDuration(
                        currentTime
                    );

                    break;

                case EffectStackType.Extend:
                    instance.ExtendDuration();

                    break;

                case EffectStackType.Stack:
                    ApplyStack(
                        instance,
                        currentTime
                    );

                    break;
            }
        }


        private void ApplyStack(
            RuntimeEffectInstance instance,
            float currentTime)
        {
            bool stackAdded =
                instance.TryAddStack();

            instance.RefreshDuration(
                currentTime
            );

            if (!stackAdded)
                return;

            Unit_RuntimeStatus runtimeStatus =
                instance.Target.RuntimeStatus;

            if (runtimeStatus == null)
                return;

            runtimeStatus.RefreshRuntimeEffect(
                instance
            );
        }


        // ============================================================
        // Registration
        // ============================================================

        private bool RegisterEffect(
            RuntimeEffectInstance instance)
        {
            if (!IsValidInstance(
                    instance))
            {
                return false;
            }

            Unit_RuntimeStatus runtimeStatus =
                instance.Target.RuntimeStatus;

            if (runtimeStatus == null)
                return false;

            if (!runtimeStatus.AddRuntimeEffect(
                    instance))
            {
                return false;
            }

            _activeEffects.Add(
                instance
            );

            return true;
        }


        private void RemoveEffectAt(
            int index)
        {
            if (index < 0 ||
                index >= _activeEffects.Count)
            {
                return;
            }

            RuntimeEffectInstance instance =
                _activeEffects[index];

            _activeEffects.RemoveAt(
                index
            );

            if (instance == null ||
                instance.Target == null)
            {
                return;
            }

            Unit_RuntimeStatus runtimeStatus =
                instance.Target.RuntimeStatus;

            if (runtimeStatus == null)
                return;

            runtimeStatus.RemoveRuntimeEffect(
                instance
            );
        }


        // ============================================================
        // Periodic
        // ============================================================

        private void ProcessPeriodicTicks(
            RuntimeEffectInstance instance,
            float currentTime)
        {
            if (!instance.HasPeriodicTick)
                return;

            float processUntilTime =
                instance.GetTickProcessUntilTime(
                    currentTime
                );

            int processCount =
                0;

            while (instance.CanTick(
                       processUntilTime))
            {
                ExecutePeriodicAction(
                    instance
                );

                processCount++;

                if (processCount >=
                    MaxTickProcessCount)
                {
                    break;
                }

                if (!IsValidInstance(
                        instance))
                {
                    break;
                }
            }
        }


        private void ExecutePeriodicAction(
            RuntimeEffectInstance instance)
        {
            EffectActionData periodicAction =
                GetPeriodicAction(
                    instance.Data
                );

            if (periodicAction == null)
                return;


            switch (periodicAction)
            {
                case PeriodicDamageEffectActionData periodicDamage:

                    ExecutePeriodicDamage(
                        instance,
                        periodicDamage
                    );

                    instance.AdvanceTick(
                        periodicDamage.Interval
                    );

                    break;


                case PeriodicHealEffectActionData periodicHeal:

                    ExecutePeriodicHeal(
                        instance,
                        periodicHeal
                    );

                    instance.AdvanceTick(
                        periodicHeal.Interval
                    );

                    break;
            }
        }


        private void ExecutePeriodicDamage(
            RuntimeEffectInstance instance,
            PeriodicDamageEffectActionData periodicAction)
        {
            float damage =
                CalculatePeriodicDamage(
                    instance,
                    periodicAction
                );

            if (damage <= 0f)
                return;


            ApplyPeriodicDamage(
                instance,
                damage
            );
        }


        private void ExecutePeriodicHeal(
            RuntimeEffectInstance instance,
            PeriodicHealEffectActionData periodicAction)
        {
            float healAmount =
                CalculatePeriodicHeal(
                    instance,
                    periodicAction
                );

            if (healAmount <= 0f)
                return;


            ApplyPeriodicHeal(
                instance,
                healAmount
            );
        }


        private float CalculatePeriodicDamage(
            RuntimeEffectInstance instance,
            PeriodicDamageEffectActionData periodicAction)
        {
            float stackMultiplier =
                1f;

            if (instance.Data.StackType ==
                EffectStackType.Stack)
            {
                stackMultiplier =
                    instance.StackCount;
            }

            return periodicAction.Damage *
                stackMultiplier;
        }


        private float CalculatePeriodicHeal(
            RuntimeEffectInstance instance,
            PeriodicHealEffectActionData periodicAction)
        {
            if (instance.Target == null)
                return 0f;

            Unit_RuntimeStatus runtimeStatus =
                instance.Target.RuntimeStatus;

            if (runtimeStatus == null)
                return 0f;


            float stackMultiplier =
                1f;

            if (instance.Data.StackType ==
                EffectStackType.Stack)
            {
                stackMultiplier =
                    instance.StackCount;
            }


            return runtimeStatus.MaxHp
                * (periodicAction.HealRatio / 100f)
                * stackMultiplier;
        }


        private void ApplyPeriodicDamage(
            RuntimeEffectInstance instance,
            float damage)
        {
            if (DamageResolver.Instance == null)
                return;

            if (instance.Source == null ||
                instance.Target == null)
            {
                return;
            }

            Unit_Core attacker =
                instance.Source.Core;

            if (attacker == null)
                return;

            DotDamageRequest request =
                new DotDamageRequest(
                    attacker,
                    instance.Target,
                    damage,
                    DamageSourceType.Dot
                );

            DamageResolver.Instance.ResolveDotDamage(
                request
            );
        }


        private void ApplyPeriodicHeal(
            RuntimeEffectInstance instance,
            float healAmount)
        {
            if (HealResolver.Instance == null)
                return;

            if (instance.Source == null ||
                instance.Target == null)
            {
                return;
            }

            Unit_Core healer =
                instance.Source.Core;

            if (healer == null)
                return;

            DotHealRequest request =
                new DotHealRequest(
                    healer,
                    instance.Target,
                    healAmount
                );

            HealResolver.Instance.ResolveDotHeal(
                request
            );
        }


        private EffectActionData GetPeriodicAction(
            EffectData effectData)
        {
            IReadOnlyList<EffectActionData> actions =
                effectData.Actions;

            for (int i = 0;
                 i < actions.Count;
                 i++)
            {
                if (actions[i]
                    is PeriodicDamageEffectActionData)
                {
                    return actions[i];
                }

                if (actions[i]
                    is PeriodicHealEffectActionData)
                {
                    return actions[i];
                }
            }

            return null;
        }


        // ============================================================
        // Search
        // ============================================================

        private RuntimeEffectInstance FindEffect(
            Unit_Gateway target,
            EffectData effectData)
        {
            for (int i = 0;
                 i < _activeEffects.Count;
                 i++)
            {
                RuntimeEffectInstance instance =
                    _activeEffects[i];

                if (instance == null)
                    continue;

                if (instance.Target != target)
                    continue;

                if (instance.Data != effectData)
                    continue;

                return instance;
            }

            return null;
        }


        // ============================================================
        // Validation
        // ============================================================

        private bool ValidateRequest(
            EffectRequest request)
        {
            if (request.EffectData == null)
            {
                Debug.LogWarning(
                    "[RuntimeEffectManager] EffectData가 없는 EffectRequest입니다."
                );

                return false;
            }

            if (request.Target == null)
            {
                Debug.LogWarning(
                    "[RuntimeEffectManager] Target이 없는 EffectRequest입니다."
                );

                return false;
            }

            if (!request.Target.IsAlive)
                return false;

            if (request.Target.RuntimeStatus == null)
            {
                Debug.LogWarning(
                    $"[RuntimeEffectManager] {request.Target.name}에 RuntimeStatus가 없습니다."
                );

                return false;
            }

            return true;
        }


        private bool IsValidInstance(
            RuntimeEffectInstance instance)
        {
            if (instance == null)
                return false;

            if (instance.Data == null)
                return false;

            if (instance.Target == null)
                return false;

            if (!instance.Target.IsAlive)
                return false;

            return true;
        }
    }
}