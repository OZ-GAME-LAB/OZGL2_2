using System;
using System.Collections.Generic;
using Units.Effects;


namespace Units
{
    internal class EffectStatus
    {
        // ============================================================
        // Data
        // ============================================================

        private readonly List<RuntimeEffectInstance> _activeEffects =
            new();

        private readonly Dictionary<UnitStatusEffectType, int> _statusCounts =
            new();

        private readonly Func<UnitStatusEffectType, bool> _isImmuneToStatus;


        // ============================================================
        // Properties
        // ============================================================

        public IReadOnlyList<RuntimeEffectInstance> ActiveEffects =>
            _activeEffects;


        // ============================================================
        // Events
        // ============================================================

        public event Action<UnitStatusEffectType, bool> StatusChanged;


        // ============================================================
        // Constructor
        // ============================================================

        public EffectStatus(
            Func<UnitStatusEffectType, bool> isImmuneToStatus)
        {
            _isImmuneToStatus =
                isImmuneToStatus;
        }


        // ============================================================
        // Effect Methods
        // ============================================================

        public bool AddEffect(
            RuntimeEffectInstance instance)
        {
            if (instance == null ||
                instance.Data == null ||
                _activeEffects.Contains(instance))
            {
                return false;
            }


            _activeEffects.Add(
                instance
            );


            AddStatuses(
                instance
            );


            return true;
        }


        public bool RemoveEffect(
            RuntimeEffectInstance instance)
        {
            if (instance == null ||
                !_activeEffects.Remove(instance))
            {
                return false;
            }


            RemoveStatuses(
                instance
            );


            return true;
        }


        public bool HasEffect(
            string effectId)
        {
            return GetEffect(
                effectId
            ) != null;
        }


        public RuntimeEffectInstance GetEffect(
            string effectId)
        {
            if (string.IsNullOrEmpty(
                    effectId))
            {
                return null;
            }


            for (int i = 0;
                 i < _activeEffects.Count;
                 i++)
            {
                RuntimeEffectInstance instance =
                    _activeEffects[i];


                if (instance == null ||
                    instance.Data == null)
                {
                    continue;
                }


                if (instance.Data.EffectId ==
                    effectId)
                {
                    return instance;
                }
            }


            return null;
        }


        // ============================================================
        // Status Methods
        // ============================================================

        public bool HasStatus(
            UnitStatusEffectType statusType)
        {
            return _statusCounts.TryGetValue(
                       statusType,
                       out int count)
                   && count > 0;
        }


        // ============================================================
        // Status Control
        // ============================================================

        private void AddStatuses(
            RuntimeEffectInstance instance)
        {
            IReadOnlyList<EffectActionData> actions =
                instance.Data.Actions;


            for (int i = 0;
                 i < actions.Count;
                 i++)
            {
                if (actions[i]
                    is not StatusEffectActionData statusAction)
                {
                    continue;
                }


                if (IsImmuneToStatus(
                        statusAction.StatusType))
                {
                    continue;
                }


                AddStatus(
                    statusAction.StatusType
                );
            }
        }


        private void RemoveStatuses(
            RuntimeEffectInstance instance)
        {
            IReadOnlyList<EffectActionData> actions =
                instance.Data.Actions;


            for (int i = 0;
                 i < actions.Count;
                 i++)
            {
                if (actions[i]
                    is not StatusEffectActionData statusAction)
                {
                    continue;
                }


                // 현재는 UnitData 기반의 기본 면역만 사용하므로
                // Effect의 생존 중 면역 상태가 변하지 않는 것을 전제로 한다.
                if (IsImmuneToStatus(
                        statusAction.StatusType))
                {
                    continue;
                }


                RemoveStatus(
                    statusAction.StatusType
                );
            }
        }


        private void AddStatus(
            UnitStatusEffectType statusType)
        {
            if (_statusCounts.TryGetValue(
                    statusType,
                    out int count))
            {
                _statusCounts[statusType] =
                    count + 1;

                return;
            }


            _statusCounts.Add(
                statusType,
                1
            );


            StatusChanged?.Invoke(
                statusType,
                true
            );
        }


        private void RemoveStatus(
            UnitStatusEffectType statusType)
        {
            if (!_statusCounts.TryGetValue(
                    statusType,
                    out int count))
            {
                return;
            }


            count--;


            if (count > 0)
            {
                _statusCounts[statusType] =
                    count;

                return;
            }


            _statusCounts.Remove(
                statusType
            );


            StatusChanged?.Invoke(
                statusType,
                false
            );
        }


        // ============================================================
        // Immunity
        // ============================================================

        private bool IsImmuneToStatus(
            UnitStatusEffectType statusType)
        {
            return _isImmuneToStatus != null &&
                _isImmuneToStatus(
                    statusType
                );
        }
    }
}