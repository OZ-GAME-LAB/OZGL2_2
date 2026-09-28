using System.Collections.Generic;
using Units.Skills;
using UnityEngine;



namespace Units
{
    public class Unit_Passive : MonoBehaviour
    {
        // ============================================================
        // References
        // ============================================================

        private Unit_Core _core;

        private ICombatTarget _owner;


        // ============================================================
        // Runtime
        // ============================================================

        private TargetResolver _targetResolver;

        private PassiveEffectExecutor _effectExecutor;

        private readonly List<RuntimePassiveSkill> _runtimePassives = new();


        // ============================================================
        // State
        // ============================================================

        private bool _isInitialized;

        private bool _isPaused;


        // ============================================================
        // Properties
        // ============================================================

        public IReadOnlyList<RuntimePassiveSkill> RuntimePassives => _runtimePassives;

        public event System.Action<SkillFXRequest> FXRequested;

        private void DispatchFX(SkillFXRequest request)
        {
            if (FXRequested == null)
                return;

            foreach (System.Action<SkillFXRequest> listener in FXRequested.GetInvocationList())
            {
                try
                {
                    listener(request);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Update()
        {
            if (!CanEvaluate())
                return;

            UpdateConditionWatches(Time.deltaTime);

            UpdateTickPassives(Time.deltaTime);
        }

        private void OnDisable()
        {
            Stop();
        }

        private void OnDestroy()
        {
            Stop();
        }


        // ============================================================
        // Initialize
        // ============================================================

        public void Initialize(
            Unit_Core core,
            ICombatTarget owner,
            IReadOnlyList<PassiveSkillData> passiveSkillDatas)
        {
            Stop();

            _core = core;

            _owner = owner;

            if (_core == null || _owner == null)
            {
                Debug.LogError($"[Unit_Passive] {name} : 초기화에 필요한 참조가 없습니다.");

                return;
            }

            _targetResolver = new TargetResolver();

            _effectExecutor = new PassiveEffectExecutor(_core, _targetResolver, DispatchFX);

            InitializeRuntimePassives(passiveSkillDatas);

            _isInitialized = true;

            _isPaused = false;

            ExecuteInitializePassives();
        }

        private void InitializeRuntimePassives(IReadOnlyList<PassiveSkillData> passiveSkillDatas)
        {
            _runtimePassives.Clear();

            if (passiveSkillDatas == null)
                return;

            for (int i = 0; i < passiveSkillDatas.Count; i++)
            {
                PassiveSkillData data = passiveSkillDatas[i];

                if (data == null)
                    continue;

                RuntimePassiveSkill runtimePassive = new RuntimePassiveSkill(data);

                _runtimePassives.Add(runtimePassive);
            }
        }


        // ============================================================
        // Initialize Trigger
        // ============================================================

        private void ExecuteInitializePassives()
        {
            EvaluateTrigger(PassiveSkillTriggerType.Initialize, null);
        }


        // ============================================================
        // Tick Trigger
        // ============================================================

        private void UpdateTickPassives(float deltaTime)
        {
            var owner = new CombatTargetSnapshot(_owner);

            foreach (var runtimePassive in _runtimePassives.ToArray())
            {
                if (!owner.IsTargetable || !CanEvaluate())
                    break;

                if (runtimePassive.Data.TriggerType != PassiveSkillTriggerType.Tick)
                    continue;

                runtimePassive.AddTickTime(deltaTime);

                if (!runtimePassive.IsTickReady())
                    continue;

                runtimePassive.ResetTickTime();

                EvaluatePassive(runtimePassive, null);
            }
        }


        // ============================================================
        // Trigger Notification
        // ============================================================

        public void NotifyHealthChanged()
        {
            if (!CanEvaluate())
                return;

            EvaluateTrigger(PassiveSkillTriggerType.HealthChanged, null);
        }

        public void NotifyShieldChanged()
        {
            if (!CanEvaluate())
                return;

            EvaluateTrigger(PassiveSkillTriggerType.ShieldChanged, null);
        }

        public void NotifyDamageDealt(ICombatTarget target)
        {
            if (!CanEvaluate())
                return;

            EvaluateTrigger(PassiveSkillTriggerType.DamageDealt, target);
        }

        public void NotifyDamageTaken(ICombatTarget attacker)
        {
            if (!CanEvaluate())
                return;

            EvaluateTrigger(PassiveSkillTriggerType.DamageTaken, attacker);
        }

        public void NotifySkillEvent(CombatSkillEvent notification)
        {
            if (!CanEvaluate())
                return;

            EvaluateTrigger(
                notification.EventType,
                notification.Target.IsTargetable ? notification.Target.Target : null,
                notification
            );
        }

        private void EvaluateTrigger(
            PassiveSkillTriggerType triggerType,
            ICombatTarget target,
            CombatSkillEvent notification = null)
        {
            var owner = new CombatTargetSnapshot(_owner);

            foreach (var runtimePassive in _runtimePassives.ToArray())
            {
                if (!owner.IsTargetable || !CanEvaluate())
                    break;

                if (runtimePassive.Data.TriggerType == triggerType)
                    EvaluatePassive(
                        runtimePassive,
                        target,
                        notification
                    );
            }
        }


        // ============================================================
        // Evaluate
        // ============================================================

        private void EvaluatePassive(
            RuntimePassiveSkill runtimePassive,
            ICombatTarget target,
            CombatSkillEvent notification = null)
        {
            if (runtimePassive == null || runtimePassive.Data == null || !CanEvaluate() || !runtimePassive.TryBeginExecution())
                return;

            try
            {
                var context = new PassiveContext(
                    _owner,
                    target,
                    _targetResolver,
                    notification
                );

                bool passed = EvaluateConditions(runtimePassive.Data.Conditions, context);

                UpdatePassiveState(
                    runtimePassive,
                    context,
                    passed
                );
            }
            finally
            {
                runtimePassive.EndExecution();

                if (runtimePassive.MaintenancePending)
                    ReevaluateMaintenance(runtimePassive);
            }
        }

        private bool EvaluateConditions(
            IReadOnlyList<PassiveSkillConditionData> conditions,
            PassiveContext context)
        {
            if (conditions == null || conditions.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                PassiveSkillConditionData condition = conditions[i];

                if (condition == null)
                    continue;

                if (!condition.Evaluate(context))
                {
                    return false;
                }
            }

            return true;
        }


        // ============================================================
        // Passive State
        // ============================================================

        private void UpdatePassiveState(
            RuntimePassiveSkill runtimePassive,
            PassiveContext context,
            bool conditionPassed)
        {
            switch (runtimePassive.Data.EffectMode)
            {
                case PassiveSkillEffectMode.Trigger:
                    ExecuteTriggerPassive(
                        runtimePassive,
                        context,
                        conditionPassed
                    );

                    break;

                case PassiveSkillEffectMode.WhileCondition:
                    UpdateWhileConditionPassive(
                        runtimePassive,
                        context,
                        conditionPassed
                    );

                    break;

                case PassiveSkillEffectMode.Once:
                    ExecuteOncePassive(
                        runtimePassive,
                        context,
                        conditionPassed
                    );

                    break;
            }
        }

        private void ExecuteTriggerPassive(
            RuntimePassiveSkill runtimePassive,
            PassiveContext context,
            bool conditionPassed)
        {
            if (!conditionPassed)
                return;

            _effectExecutor.Execute(runtimePassive, context);
        }

        private void UpdateWhileConditionPassive(
            RuntimePassiveSkill runtimePassive,
            PassiveContext context,
            bool conditionPassed)
        {
            runtimePassive.Watch?.Dispose();

            runtimePassive.ActivationMetadata = context.Metadata.WithAdditionalAttack(runtimePassive.IsActive && runtimePassive.ActivationMetadata.IsAdditionalAttack);

            runtimePassive.Watch = new PassiveConditionWatch(
                runtimePassive.Data,
                context,
                () => ReevaluateMaintenance(runtimePassive)
            );

            if (!conditionPassed)
            {
                runtimePassive.Watch.Dispose();

                runtimePassive.Watch = null;

                if (!runtimePassive.IsActive)
                    return;

                runtimePassive.SetActive(false);

                _effectExecutor.Deactivate(runtimePassive, context);

                return;
            }

            if (!runtimePassive.IsActive)
            {
                runtimePassive.SetActive(true);

                _effectExecutor.Activate(runtimePassive, context);
            }

            _effectExecutor.Execute(runtimePassive, context);
        }

        private void ExecuteOncePassive(
            RuntimePassiveSkill runtimePassive,
            PassiveContext context,
            bool conditionPassed)
        {
            if (!conditionPassed || runtimePassive.HasExecutedOnce)
                return;

            var executor = _effectExecutor;

            var lifetime = new CombatTargetSnapshot(context.Owner);

            if (executor == null || !lifetime.IsTargetable)
                return;

            // 보정 등록의 알림 중 Stop이 재진입해도 이 소유자의 보정을 정리할 수 있게 먼저 표시한다.
            runtimePassive.ActivationMetadata = context.Metadata;

            runtimePassive.SetActive(true);

            bool applied = executor.ActivateWithResult(runtimePassive, context);

            if (!applied)
                runtimePassive.SetActive(false);

            if (lifetime.IsTargetable)
                applied |= executor.ExecuteWithResult(runtimePassive, context);

            if (applied && lifetime.MatchesLifetime)
                runtimePassive.MarkExecutedOnce();
        }


        // ============================================================
        // Damage Modifier
        // ============================================================


        // DamageContext 생성 시 현재 유닛이 계산에 제공할
        // DamageModifier Action을 OwnerType 기준으로 수집한다.
        //
        // DamageModifier는 일반 Passive Action과 달리
        // Trigger / Once / WhileCondition의 EffectMode에 영향을 받지 않는다.
        // 실제 적용 여부는 Damage 계산 시점의 PassiveContext를 기준으로
        // 해당 Runtime Passive의 Condition을 다시 평가하여 결정한다.
        public void CollectDamageModifiers(
            PassiveDamageOwnerType ownerType,
            List<PassiveDamageModifier> results)
        {
            if (!_isInitialized || results == null)
            {
                return;
            }

            for (int i = 0; i < _runtimePassives.Count; i++)
            {
                RuntimePassiveSkill runtimePassive = _runtimePassives[i];

                if (runtimePassive == null || runtimePassive.Data == null)
                {
                    continue;
                }

                CollectDamageModifiers(
                    runtimePassive,
                    ownerType,
                    results
                );
            }
        }

        private void CollectDamageModifiers(
            RuntimePassiveSkill runtimePassive,
            PassiveDamageOwnerType ownerType,
            List<PassiveDamageModifier> results)
        {
            IReadOnlyList<PassiveSkillActionData> actions = runtimePassive.Data.Actions;

            if (actions == null)
                return;

            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i] is not PassiveDamageModifierActionData damageModifier)
                {
                    continue;
                }

                if (damageModifier.OwnerType != ownerType)
                {
                    continue;
                }

                results.Add(new PassiveDamageModifier(runtimePassive, damageModifier));
            }
        }


        // ============================================================
        // Damage Modifier Condition
        // ============================================================


        // DamageModifier는 Passive의 활성 상태나 EffectMode를 사용하지 않는다.
        // Damage 계산 시점의 공격자와 피격자를 기준으로
        // 해당 Runtime Passive의 Condition을 직접 평가한다.
        public bool EvaluateDamageModifierConditions(
            RuntimePassiveSkill runtimePassive,
            ICombatTarget target,
            CombatSourceSnapshot frozenTarget = null)
        {
            if (!_isInitialized || runtimePassive == null || runtimePassive.Data == null)
            {
                return false;
            }

            PassiveContext context = new PassiveContext(
                _owner,
                target,
                _targetResolver,
                frozenTarget: frozenTarget
            );

            return EvaluateConditions(runtimePassive.Data.Conditions, context);
        }


        // ============================================================
        // Context
        // ============================================================

        private PassiveContext CreateContext(ICombatTarget target)
        {
            return new PassiveContext(
                _owner,
                target,
                _targetResolver
            );
        }


        // ============================================================
        // State
        // ============================================================

        // ============================================================
        // WhileCondition Maintenance
        // ============================================================
        private void UpdateConditionWatches(float deltaTime)
        {
            foreach (var runtime in _runtimePassives.ToArray())
                if (CanEvaluate() && runtime.IsActive)
                    runtime.Watch?.Tick(deltaTime, runtime.Data.ConditionCheckInterval);
        }

        private void ReevaluateMaintenance(RuntimePassiveSkill runtime)
        {
            if (!CanEvaluate() || !runtime.IsActive || runtime.Watch == null)
                return;

            if (runtime.IsExecuting)
            {
                runtime.MaintenancePending = true;

                return;
            }

            runtime.MaintenancePending = false;

            var watch = runtime.Watch;

            if (watch.IsValid && EvaluateConditions(runtime.Data.Conditions, watch.Context))
                return;

            runtime.SetActive(false);

            watch.Dispose();

            runtime.Watch = null;

            _effectExecutor?.Deactivate(runtime, watch.Context);
        }

        private bool CanEvaluate()
        {
            return _isInitialized && !_isPaused && _owner != null && _owner.IsTargetable;
        }


        // ============================================================
        // Control
        // ============================================================

        public void Pause()
        {
            if (!_isInitialized)
                return;

            _isPaused = true;
        }

        public void Resume()
        {
            if (!_isInitialized)
                return;

            _isPaused = false;

            foreach (var runtime in _runtimePassives.ToArray())
                ReevaluateMaintenance(runtime);
        }

        public void Stop()
        {
            // 먼저 이전 실행의 소유권을 분리한다. 해제 알림 중 재초기화된 새 패시브를 지우지 않는다.
            var executor = _effectExecutor;

            var passives = _runtimePassives.ToArray();

            var owner = new CombatTargetSnapshot(_owner);

            var context = new PassiveContext(
                _owner,
                null,
                _targetResolver
            );

            _runtimePassives.Clear();

            _effectExecutor = null;

            _targetResolver = null;

            _owner = null;

            _core = null;

            _isInitialized = false;

            _isPaused = false;

            foreach (var runtime in passives)
            {
                runtime.StopExecution();

                runtime.Watch?.Dispose();

                runtime.Watch = null;
            }

            DeactivateAllPassives(
                executor,
                passives,
                context,
                owner
            );
        }

        private void DeactivateAllPassives(
            PassiveEffectExecutor executor,
            RuntimePassiveSkill[] passives,
            PassiveContext context,
            CombatTargetSnapshot owner)
        {
            if (executor == null)
                return;

            foreach (var runtime in passives)
            {
                if (!owner.MatchesLifetime)
                    break;

                if (!runtime.IsActive)
                    continue;

                runtime.SetActive(false);

                executor.Deactivate(runtime, context);
            }
        }
    }
}
