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

        private const float UpdateInterval = 0.1f;

        private const int MaxTickProcessCount = 20;


        // ============================================================
        // Data
        // ============================================================

        private readonly List<RuntimeEffectInstance> _activeEffects = new();


        // ============================================================
        // Runtime
        // ============================================================

        private float _updateTimer;

        private readonly HashSet<ICombatTarget> _mutatingTargets = new();

        private readonly Dictionary<string, EffectData> _knownIds = new();


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);

                return;
            }

            Instance = this;
        }

        private void Update()
        {
            if (_activeEffects.Count == 0)
            {
                _updateTimer = 0f;

                return;
            }

            _updateTimer += Time.deltaTime;

            if (_updateTimer < UpdateInterval)
            {
                return;
            }

            _updateTimer = 0f;

            float currentTime = Time.time;

            UpdateActiveEffects(currentTime);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }


        // ============================================================
        // Apply
        // ============================================================

        public bool ApplyEffect(EffectRequest request)
        {
            return ApplyEffectWithResult(request).Status != CombatApplicationStatus.Invalid;
        }

        public CombatApplicationResult ApplyEffectWithResult(EffectRequest request)
        {
            if (!ValidateRequest(request) || string.IsNullOrWhiteSpace(request.Definition.EffectId) || _mutatingTargets.Contains(request.Target))
                return CombatApplicationResult.Invalid(
                    CombatApplicationKind.RuntimeEffect,
                    request.Target,
                    request.Metadata,
                    "Invalid effect request or transaction reentry"
                );

            string id = request.Definition.EffectId;

            if (_knownIds.TryGetValue(id, out var known) && known != null && known != request.EffectData)
                return CombatApplicationResult.Invalid(
                    CombatApplicationKind.RuntimeEffect,
                    request.Target,
                    request.Metadata,
                    "Duplicate EffectId: " + id
                );

            if (request.Target.RuntimeStatus.IsImmuneToEffect(request.Definition))
                return new CombatApplicationResult(
                    CombatApplicationKind.RuntimeEffect,
                    request.Target,
                    1f,
                    request.Metadata,
                    CombatApplicationStatus.NoChange,
                    "Immune to effect status"
                );

            _knownIds[id] = request.EffectData;

            var existing = FindEffect(request.Target, request.EffectData, request.OwnershipKey);

            int count = existing != null ? existing.StackCount : 0;

            float expiry = existing != null ? existing.ExpireTime : 0f;

            var result = new CombatApplicationResult(
                CombatApplicationKind.RuntimeEffect,
                request.Target,
                1f,
                request.Metadata
            );

            var status = request.Target.RuntimeStatus;

            _mutatingTargets.Add(request.Target);

            status.BeginEffectTransaction();

            using (CombatEventContext.Enter(request.Metadata))
            {
                try
                {
                    if (!ApplyEffectInternal(request))
                        result.Status = CombatApplicationStatus.Invalid;

                    else
                    {
                        var current = FindEffect(request.Target, request.EffectData, request.OwnershipKey);

                        bool changed = current != null && (existing == null || current.StackCount != count || current.ExpireTime != expiry);

                        if (changed)
                            current.MergeLineage(request.Metadata);

                        result.Status = changed ? CombatApplicationStatus.Applied : CombatApplicationStatus.NoChange;
                    }
                }
                finally
                {
                    try
                    {
                        status.EndEffectTransaction(result.WasApplied);
                    }
                    finally
                    {
                        _mutatingTargets.Remove(request.Target);
                    }
                }
            }

            return result;
        }

        public bool TryConsumeStacks(EffectStackConsumeRequest request)
        {
            if (!request.Target.IsTargetable || request.Query == null || !request.Query.IsValid || request.Count <= 0 || _mutatingTargets.Contains(request.Target.Target))
                return false;

            var target = request.Target.Target;

            var candidates = new List<RuntimeEffectInstance>();

            long available = 0;

            foreach (var instance in _activeEffects)
                if (ReferenceEquals(instance.Target, target) && IsValidInstance(instance) && request.Query.Matches(instance.Definition))
                {
                    candidates.Add(instance);

                    available += instance.StackCount;
                }

            if (available < request.Count)
                return false; // 부족하면 변경·통지 없이 실패한다.
            candidates.Sort((
                a,
                b) => a.AppliedOrder.CompareTo(b.AppliedOrder));

            _mutatingTargets.Add(target);

            var status = target.RuntimeStatus;

            status.BeginEffectTransaction();

            using (CombatEventContext.Enter(request.Metadata))
            {
                try
                {
                    int remaining = request.Count;

                    foreach (var instance in candidates)
                    {
                        int consume = Mathf.Min(remaining, instance.StackCount);

                        instance.ConsumeStacks(consume);

                        remaining -= consume;

                        if (instance.StackCount == 0)
                            RemoveEffect(instance);

                        else
                            status.RefreshRuntimeEffect(instance);

                        if (remaining == 0)
                            break;
                    }
                }
                finally
                {
                    try
                    {
                        status.EndEffectTransaction(true);
                    }
                    finally
                    {
                        _mutatingTargets.Remove(target);
                    }
                }
            }

            return true;
        }

        private bool ApplyEffectInternal(EffectRequest request)
        {
            if (!ValidateRequest(request))
            {
                return false;
            }

            float currentTime = Time.time;

            RuntimeEffectInstance existingEffect = FindEffect(request.Target, request.EffectData, request.OwnershipKey);

            if (existingEffect != null)
            {
                ApplyExistingEffect(existingEffect, currentTime);

                return true;
            }

            RuntimeEffectInstance newEffect = new RuntimeEffectInstance(
                request.EffectData,
                request.Source,
                request.Target,
                currentTime,
                request.Metadata,
                request.Definition,
                request.OwnershipKey
            );

            if (!RegisterEffect(newEffect))
            {
                return false;
            }

            return true;
        }


        // ============================================================
        // Remove
        // ============================================================

        public bool RemoveEffect(RuntimeEffectInstance instance)
        {
            if (instance == null)
                return false;

            int index = _activeEffects.IndexOf(instance);

            if (index < 0)
                return false;

            RemoveEffectAt(index);

            return true;
        }

        public void RemoveOwnedEffects(ICombatTarget target, object ownershipKey)
        {
            if (ownershipKey == null) return;
            foreach (var instance in _activeEffects.ToArray())
                if (ReferenceEquals(instance.Target, target) && ReferenceEquals(instance.OwnershipKey, ownershipKey))
                    RemoveEffect(instance);
        }

        public void RemoveEffects(ICombatTarget target)
        {
            if (target == null)
                return;

            foreach (var instance in _activeEffects.ToArray())
                if (ReferenceEquals(instance.Target, target))
                    RemoveEffect(instance);
        }

        public void ClearAllEffects()
        {
            foreach (var instance in _activeEffects.ToArray())
                RemoveEffect(instance);
        }


        // ============================================================
        // Update
        // ============================================================

        private void UpdateActiveEffects(float currentTime)
        {
            foreach (var instance in _activeEffects.ToArray())
            {
                if (!_activeEffects.Contains(instance))
                    continue;

                if (!IsValidInstance(instance))
                {
                    RemoveEffect(instance);

                    continue;
                }

                ProcessPeriodicTicks(instance, currentTime);

                if (instance.IsExpired(currentTime))
                    RemoveEffect(instance);
            }
        }


        // ============================================================
        // Existing Effect
        // ============================================================

        private void ApplyExistingEffect(
            RuntimeEffectInstance instance,
            float currentTime)
        {
            EffectStackType stackType = instance.Definition.StackType;

            switch (stackType)
            {
                case EffectStackType.None:
                    break;

                case EffectStackType.Refresh:
                    instance.RefreshDuration(currentTime);

                    break;

                case EffectStackType.Extend:
                    instance.ExtendDuration();

                    break;

                case EffectStackType.Stack:
                    ApplyStack(instance, currentTime);

                    break;
            }
        }

        private void ApplyStack(
            RuntimeEffectInstance instance,
            float currentTime)
        {
            bool stackAdded = instance.TryAddStack();

            instance.RefreshDuration(currentTime);

            if (!stackAdded)
                return;

            Unit_RuntimeStatus runtimeStatus = instance.Target.RuntimeStatus;

            if (runtimeStatus == null)
                return;

            runtimeStatus.RefreshRuntimeEffect(instance);
        }


        // ============================================================
        // Registration
        // ============================================================

        private bool RegisterEffect(RuntimeEffectInstance instance)
        {
            if (!IsValidInstance(instance))
            {
                return false;
            }

            Unit_RuntimeStatus runtimeStatus = instance.Target.RuntimeStatus;

            if (runtimeStatus == null)
                return false;

            if (!runtimeStatus.AddRuntimeEffect(instance))
            {
                return false;
            }

            _activeEffects.Add(instance);

            return true;
        }

        private void RemoveEffectAt(int index)
        {
            if (index < 0 || index >= _activeEffects.Count)
                return;

            var instance = _activeEffects[index];

            _activeEffects.RemoveAt(index);

            if (instance == null || !CombatTargetUtility.Exists(instance.Target) || instance.Target.LifetimeVersion != instance.TargetLifetimeVersion)
                return;

            var status = instance.Target.RuntimeStatus;

            if (status == null)
                return;

            status.BeginEffectTransaction();

            using (CombatEventContext.Enter(instance.Metadata))
            {
                try
                {
                    status.RemoveRuntimeEffect(instance);
                }
                finally
                {
                    status.EndEffectTransaction(true);
                }
            }
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

            float processUntilTime = instance.GetTickProcessUntilTime(currentTime);

            int processCount = 0;

            while (instance.CanTick(processUntilTime))
            {
                ExecutePeriodicAction(instance);

                processCount++;

                if (processCount >= MaxTickProcessCount)
                {
                    break;
                }

                if (!_activeEffects.Contains(instance) || !IsValidInstance(instance))
                {
                    break;
                }
            }
        }

        private void ExecutePeriodicAction(RuntimeEffectInstance instance)
        {
            EffectActionData periodicAction = GetPeriodicAction(instance.Definition);

            if (periodicAction == null)
                return;

            switch (periodicAction)
            {
                case PeriodicDamageEffectActionData periodicDamage:
                    ExecutePeriodicDamage(instance, periodicDamage);

                    instance.AdvanceTick(periodicDamage.Interval);

                    break;

                case PeriodicHealEffectActionData periodicHeal:
                    ExecutePeriodicHeal(instance, periodicHeal);

                    instance.AdvanceTick(periodicHeal.Interval);

                    break;
            }
        }

        private void ExecutePeriodicDamage(
            RuntimeEffectInstance instance,
            PeriodicDamageEffectActionData periodicAction)
        {
            float damage = CalculatePeriodicDamage(instance, periodicAction);

            if (damage <= 0f)
                return;

            ApplyPeriodicDamage(instance, damage);
        }

        private void ExecutePeriodicHeal(
            RuntimeEffectInstance instance,
            PeriodicHealEffectActionData periodicAction)
        {
            float healAmount = CalculatePeriodicHeal(instance, periodicAction);

            if (healAmount <= 0f)
                return;

            ApplyPeriodicHeal(instance, healAmount);
        }

        private float CalculatePeriodicDamage(
            RuntimeEffectInstance instance,
            PeriodicDamageEffectActionData periodicAction)
        {
            float stackMultiplier = 1f;

            if (instance.Definition.StackType == EffectStackType.Stack)
            {
                stackMultiplier = instance.StackCount;
            }

            return periodicAction.Damage * stackMultiplier;
        }

        private float CalculatePeriodicHeal(
            RuntimeEffectInstance instance,
            PeriodicHealEffectActionData periodicAction)
        {
            if (instance.Target == null)
                return 0f;

            Unit_RuntimeStatus runtimeStatus = instance.Target.RuntimeStatus;

            if (runtimeStatus == null)
                return 0f;

            float stackMultiplier = 1f;

            if (instance.Definition.StackType == EffectStackType.Stack)
            {
                stackMultiplier = instance.StackCount;
            }

            return runtimeStatus.MaxHp * periodicAction.HealRatio * stackMultiplier;
        }

        private void ApplyPeriodicDamage(
            RuntimeEffectInstance instance,
            float damage)
        {
            if (DamageResolver.Instance == null)
                return;

            if (!CombatTargetUtility.Exists(instance.Target))
            {
                return;
            }

            // 고정된 주기량/원인 스냅샷은 시전자 사망·재사용 뒤에도 유지한다.
            DotDamageRequest request = new DotDamageRequest(
                instance.Source,
                instance.Target,
                damage,
                DamageSourceType.Dot,
                CombatEventMetadata.Create(instance.Source, instance.Metadata)
            );

            DamageResolver.Instance.ResolveDotDamage(request);
        }

        private void ApplyPeriodicHeal(
            RuntimeEffectInstance instance,
            float healAmount)
        {
            if (HealResolver.Instance == null)
                return;

            if (!CombatTargetUtility.Exists(instance.Target))
            {
                return;
            }

            // 고정된 주기량/원인 스냅샷은 시전자 사망·재사용 뒤에도 유지한다.
            DotHealRequest request = new DotHealRequest(
                instance.Source,
                instance.Target,
                healAmount,
                CombatEventMetadata.Create(instance.Source, instance.Metadata)
            );

            HealResolver.Instance.ResolveDotHeal(request);
        }

        private EffectActionData GetPeriodicAction(EffectDefinitionSnapshot effectData)
        {
            IReadOnlyList<EffectActionData> actions = effectData.Actions;

            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i] is PeriodicDamageEffectActionData)
                {
                    return actions[i];
                }

                if (actions[i] is PeriodicHealEffectActionData)
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
            ICombatTarget target,
            EffectData effectData, object ownershipKey = null)
        {
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                RuntimeEffectInstance instance = _activeEffects[i];

                if (instance == null)
                    continue;

                if (instance.Target != target)
                    continue;

                if (!ReferenceEquals(instance.OwnershipKey, ownershipKey))
                    continue;

                if (instance.Data != effectData)
                    continue;

                if (instance.TargetLifetimeVersion != target.LifetimeVersion)
                    continue;

                return instance;
            }

            return null;
        }


        // ============================================================
        // Validation
        // ============================================================

        private bool ValidateRequest(EffectRequest request)
        {
            if (!request.TargetSnapshot.IsTargetable)
                return false;

            if (request.EffectData == null)
            {
                Debug.LogWarning("[RuntimeEffectManager] EffectData가 없는 EffectRequest입니다.");

                return false;
            }

            if (!CombatTargetUtility.Exists(request.Target))
            {
                Debug.LogWarning("[RuntimeEffectManager] Target이 없는 EffectRequest입니다.");

                return false;
            }

            if (!request.Target.IsAlive)
                return false;

            if (request.Target.RuntimeStatus == null)
            {
                Debug.LogWarning($"[RuntimeEffectManager] {request.Target.Transform.name}에 RuntimeStatus가 없습니다.");

                return false;
            }

            return true;
        }

        private bool IsValidInstance(RuntimeEffectInstance instance)
        {
            if (instance == null)
                return false;

            if (instance.Data == null)
                return false;

            if (!CombatTargetUtility.Exists(instance.Target))
            {
                return false;
            }

            if (instance.Target.LifetimeVersion != instance.TargetLifetimeVersion)
            {
                return false;
            }

            if (!instance.Target.IsAlive)
                return false;

            return true;
        }
    }
}