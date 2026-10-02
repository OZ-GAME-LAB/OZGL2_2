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

        private readonly List<RuntimeEffectInstance> _activeEffects = new();

        private readonly Dictionary<UnitStatusEffectType, int> _statusCounts = new();

        private readonly Func<UnitStatusEffectType, bool> _isImmuneToStatus;

        private readonly Dictionary<RuntimeEffectInstance, HashSet<UnitStatusEffectType>> _registeredStatuses = new();


        // ============================================================
        // Properties
        // ============================================================

        public IReadOnlyList<RuntimeEffectInstance> ActiveEffects => _activeEffects;


        // ============================================================
        // Events
        // ============================================================

        public event Action<UnitStatusEffectType, bool> StatusChanged;


        // ============================================================
        // Constructor
        // ============================================================

        public EffectStatus(Func<UnitStatusEffectType, bool> isImmuneToStatus)
        {
            _isImmuneToStatus = isImmuneToStatus;
        }


        // ============================================================
        // Effect Methods
        // ============================================================

        public bool AddEffect(RuntimeEffectInstance instance)
        {
            if (instance == null || instance.Data == null || _activeEffects.Contains(instance))
            {
                return false;
            }

            _activeEffects.Add(instance);

            AddStatuses(instance);

            return true;
        }

        public bool RemoveEffect(RuntimeEffectInstance instance)
        {
            if (instance == null || !_activeEffects.Remove(instance))
            {
                return false;
            }

            RemoveStatuses(instance);

            return true;
        }

        public bool HasEffect(string effectId)
        {
            return GetEffect(effectId) != null;
        }

        public RuntimeEffectInstance GetEffect(string effectId)
        {
            if (string.IsNullOrEmpty(effectId))
            {
                return null;
            }

            for (int i = 0; i < _activeEffects.Count; i++)
            {
                RuntimeEffectInstance instance = _activeEffects[i];

                if (instance == null || instance.Data == null)
                {
                    continue;
                }

                if (instance.Definition.EffectId == effectId)
                {
                    return instance;
                }
            }

            return null;
        }


        // ============================================================
        // Status Methods
        // ============================================================

        public bool HasStatus(UnitStatusEffectType statusType)
        {
            return _statusCounts.TryGetValue(statusType, out int count) && count > 0;
        }


        // ============================================================
        // Status Control
        // ============================================================

        private void AddStatuses(RuntimeEffectInstance instance)
        {
            var statuses = new HashSet<UnitStatusEffectType>();
            foreach (var action in instance.Definition.Actions)
                if (action is StatusEffectActionData status && !IsImmuneToStatus(status.StatusType))
                    statuses.Add(status.StatusType);

            _registeredStatuses.Add(instance, statuses);
            foreach (var status in statuses)
                AddStatus(status);
        }

        private void RemoveStatuses(RuntimeEffectInstance instance)
        {
            // 제거 시 현재 면역을 다시 판정하지 않고 실제 등록했던 상태만 해제한다.
            if (!_registeredStatuses.TryGetValue(instance, out var statuses))
                return;

            _registeredStatuses.Remove(instance);
            foreach (var status in statuses)
                RemoveStatus(status);
        }

        private void AddStatus(UnitStatusEffectType statusType)
        {
            if (_statusCounts.TryGetValue(statusType, out int count))
            {
                _statusCounts[statusType] = count + 1;

                return;
            }

            _statusCounts.Add(statusType, 1);

            StatusChanged?.Invoke(statusType, true);
        }

        private void RemoveStatus(UnitStatusEffectType statusType)
        {
            if (!_statusCounts.TryGetValue(statusType, out int count))
            {
                return;
            }

            count--;

            if (count > 0)
            {
                _statusCounts[statusType] = count;

                return;
            }

            _statusCounts.Remove(statusType);

            StatusChanged?.Invoke(statusType, false);
        }


        // ============================================================
        // Immunity
        // ============================================================

        private bool IsImmuneToStatus(UnitStatusEffectType statusType)
        {
            return _isImmuneToStatus != null && _isImmuneToStatus(statusType);
        }
    }
}