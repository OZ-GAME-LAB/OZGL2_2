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

        private readonly List<RuntimePassiveSkill> _runtimePassives =
            new();


        // ============================================================
        // State
        // ============================================================

        private bool _isInitialized;

        private bool _isPaused;


        // ============================================================
        // Properties
        // ============================================================

        public IReadOnlyList<RuntimePassiveSkill> RuntimePassives =>
            _runtimePassives;


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Update()
        {
            if (!CanEvaluate())
                return;

            UpdateTickPassives(
                Time.deltaTime
            );
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

            _core =
                core;

            _owner =
                owner;

            if (_core == null ||
                _owner == null)
            {
                Debug.LogError(
                    $"[Unit_Passive] {name} : 초기화에 필요한 참조가 없습니다."
                );

                return;
            }

            _targetResolver =
                new TargetResolver(
                    _core
                );

            _effectExecutor =
                new PassiveEffectExecutor(
                    _core,
                    _targetResolver
                );

            InitializeRuntimePassives(
                passiveSkillDatas
            );

            _isInitialized =
                true;

            _isPaused =
                false;

            ExecuteInitializePassives();
        }


        private void InitializeRuntimePassives(
            IReadOnlyList<PassiveSkillData> passiveSkillDatas)
        {
            _runtimePassives.Clear();

            if (passiveSkillDatas == null)
                return;

            for (int i = 0;
                 i < passiveSkillDatas.Count;
                 i++)
            {
                PassiveSkillData data =
                    passiveSkillDatas[i];

                if (data == null)
                    continue;

                RuntimePassiveSkill runtimePassive =
                    new RuntimePassiveSkill(
                        data
                    );

                _runtimePassives.Add(
                    runtimePassive
                );
            }
        }


        // ============================================================
        // Initialize Trigger
        // ============================================================

        private void ExecuteInitializePassives()
        {
            EvaluateTrigger(
                PassiveSkillTriggerType.Initialize,
                null
            );
        }


        // ============================================================
        // Tick Trigger
        // ============================================================

        private void UpdateTickPassives(
            float deltaTime)
        {
            for (int i = 0;
                 i < _runtimePassives.Count;
                 i++)
            {
                RuntimePassiveSkill runtimePassive =
                    _runtimePassives[i];

                if (runtimePassive.Data.TriggerType !=
                    PassiveSkillTriggerType.Tick)
                {
                    continue;
                }

                runtimePassive.AddTickTime(
                    deltaTime
                );

                if (!runtimePassive.IsTickReady())
                    continue;

                runtimePassive.ResetTickTime();

                EvaluatePassive(
                    runtimePassive,
                    null
                );
            }
        }


        // ============================================================
        // Trigger Notification
        // ============================================================

        public void NotifyHealthChanged()
        {
            if (!CanEvaluate())
                return;

            EvaluateTrigger(
                PassiveSkillTriggerType.HealthChanged,
                null
            );
        }


        public void NotifyDamageDealt(
            ICombatTarget target)
        {
            if (!CanEvaluate())
                return;

            EvaluateTrigger(
                PassiveSkillTriggerType.DamageDealt,
                target
            );
        }


        public void NotifyDamageTaken(
            ICombatTarget attacker)
        {
            if (!CanEvaluate())
                return;

            EvaluateTrigger(
                PassiveSkillTriggerType.DamageTaken,
                attacker
            );
        }


        private void EvaluateTrigger(
            PassiveSkillTriggerType triggerType,
            ICombatTarget target)
        {
            for (int i = 0;
                 i < _runtimePassives.Count;
                 i++)
            {
                RuntimePassiveSkill runtimePassive =
                    _runtimePassives[i];

                if (runtimePassive.Data.TriggerType !=
                    triggerType)
                {
                    continue;
                }

                EvaluatePassive(
                    runtimePassive,
                    target
                );
            }
        }


        // ============================================================
        // Evaluate
        // ============================================================

        private void EvaluatePassive(
            RuntimePassiveSkill runtimePassive,
            ICombatTarget target)
        {
            if (runtimePassive == null ||
                runtimePassive.Data == null)
            {
                return;
            }

            PassiveContext context =
                CreateContext(
                    target
                );

            bool conditionPassed =
                EvaluateConditions(
                    runtimePassive.Data.Conditions,
                    context
                );

            UpdatePassiveState(
                runtimePassive,
                context,
                conditionPassed
            );
        }


        private bool EvaluateConditions(
            IReadOnlyList<PassiveSkillConditionData> conditions,
            PassiveContext context)
        {
            if (conditions == null ||
                conditions.Count == 0)
            {
                return true;
            }

            for (int i = 0;
                 i < conditions.Count;
                 i++)
            {
                PassiveSkillConditionData condition =
                    conditions[i];

                if (condition == null)
                    continue;

                if (!condition.Evaluate(
                        context))
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

            _effectExecutor.Execute(
                runtimePassive,
                context
            );
        }


        private void UpdateWhileConditionPassive(
            RuntimePassiveSkill runtimePassive,
            PassiveContext context,
            bool conditionPassed)
        {
            if (!conditionPassed)
            {
                if (!runtimePassive.IsActive)
                    return;

                _effectExecutor.Deactivate(
                    runtimePassive,
                    context
                );

                runtimePassive.SetActive(
                    false
                );

                return;
            }

            if (!runtimePassive.IsActive)
            {
                runtimePassive.SetActive(
                    true
                );

                _effectExecutor.Activate(
                    runtimePassive,
                    context
                );
            }

            _effectExecutor.Execute(
                runtimePassive,
                context
            );
        }


        private void ExecuteOncePassive(
            RuntimePassiveSkill runtimePassive,
            PassiveContext context,
            bool conditionPassed)
        {
            if (!conditionPassed)
                return;

            if (runtimePassive.HasExecutedOnce)
                return;

            runtimePassive.MarkExecutedOnce();

            _effectExecutor.Execute(
                runtimePassive,
                context
            );
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
            if (!_isInitialized ||
                results == null)
            {
                return;
            }

            for (int i = 0;
                 i < _runtimePassives.Count;
                 i++)
            {
                RuntimePassiveSkill runtimePassive =
                    _runtimePassives[i];

                if (runtimePassive == null ||
                    runtimePassive.Data == null)
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
            IReadOnlyList<PassiveSkillActionData> actions =
                runtimePassive.Data.Actions;

            if (actions == null)
                return;

            for (int i = 0;
                 i < actions.Count;
                 i++)
            {
                if (actions[i]
                    is not PassiveDamageModifierActionData damageModifier)
                {
                    continue;
                }

                if (damageModifier.OwnerType !=
                    ownerType)
                {
                    continue;
                }

                results.Add(
                    new PassiveDamageModifier(
                        runtimePassive,
                        damageModifier
                    )
                );
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
            ICombatTarget target)
        {
            if (!_isInitialized ||
                runtimePassive == null ||
                runtimePassive.Data == null)
            {
                return false;
            }

            PassiveContext context =
                CreateContext(
                    target
                );

            return EvaluateConditions(
                runtimePassive.Data.Conditions,
                context
            );
        }


        // ============================================================
        // Context
        // ============================================================

        private PassiveContext CreateContext(
            ICombatTarget target)
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

        private bool CanEvaluate()
        {
            return _isInitialized
                && !_isPaused
                && _owner != null
                && _owner.IsTargetable;
        }


        // ============================================================
        // Control
        // ============================================================

        public void Pause()
        {
            if (!_isInitialized)
                return;

            _isPaused =
                true;
        }


        public void Resume()
        {
            if (!_isInitialized)
                return;

            _isPaused =
                false;
        }


        public void Stop()
        {
            DeactivateAllPassives();

            _runtimePassives.Clear();

            _effectExecutor =
                null;

            _targetResolver =
                null;

            _owner =
                null;

            _core =
                null;

            _isInitialized =
                false;

            _isPaused =
                false;
        }


        private void DeactivateAllPassives()
        {
            if (_effectExecutor == null ||
                _owner == null ||
                _targetResolver == null)
            {
                return;
            }

            PassiveContext context =
                CreateContext(
                    null
                );

            for (int i = 0;
                 i < _runtimePassives.Count;
                 i++)
            {
                RuntimePassiveSkill runtimePassive =
                    _runtimePassives[i];

                if (!runtimePassive.IsActive)
                    continue;

                _effectExecutor.Deactivate(
                    runtimePassive,
                    context
                );

                runtimePassive.SetActive(
                    false
                );
            }
        }
    }
}