using System.Collections.Generic;
using UnityEngine;


namespace Units.Effects
{
    public class RuntimeEffectInstance
    {
        // ============================================================
        // Data
        // ============================================================

        private readonly EffectData _data;

        private readonly ICombatTarget _source;

        private readonly ICombatTarget _target;

        private readonly int _sourceLifetimeVersion;

        private readonly int _targetLifetimeVersion;


        // ============================================================
        // Runtime Data
        // ============================================================

        private int _stackCount;

        private readonly float _appliedTime;

        private float _expireTime;

        private float _nextTickTime;


        // ============================================================
        // Properties
        // ============================================================

        public EffectData Data =>
            _data;

        public ICombatTarget Source =>
            _source;

        public ICombatTarget Target =>
            _target;

        public int SourceLifetimeVersion =>
            _sourceLifetimeVersion;

        public int TargetLifetimeVersion =>
            _targetLifetimeVersion;

        public int StackCount =>
            _stackCount;

        public float AppliedTime =>
            _appliedTime;

        public float ExpireTime =>
            _expireTime;

        public float NextTickTime =>
            _nextTickTime;

        public bool IsInfinite =>
            _data.DurationType ==
            EffectDurationType.Infinite;

        public bool HasPeriodicTick =>
            _nextTickTime > 0f;


        // ============================================================
        // Constructor
        // ============================================================

        public RuntimeEffectInstance(
            EffectData data,
            ICombatTarget source,
            ICombatTarget target,
            float currentTime)
        {
            _data =
                data;

            _source =
                source;

            _target =
                target;

            _sourceLifetimeVersion =
                source != null
                    ? source.LifetimeVersion
                    : 0;

            _targetLifetimeVersion =
                target != null
                    ? target.LifetimeVersion
                    : 0;


            _stackCount =
                1;

            _appliedTime =
                currentTime;


            InitializeDuration(
                currentTime
            );

            InitializePeriodic(
                currentTime
            );
        }


        // ============================================================
        // Duration
        // ============================================================

        public bool IsExpired(
            float currentTime)
        {
            if (IsInfinite)
                return false;

            return currentTime >=
                _expireTime;
        }


        public float GetRemainingDuration(
            float currentTime)
        {
            if (IsInfinite)
                return float.PositiveInfinity;

            return Mathf.Max(
                0f,
                _expireTime - currentTime
            );
        }


        public void RefreshDuration(
            float currentTime)
        {
            if (IsInfinite)
                return;

            _expireTime =
                currentTime +
                _data.Duration;
        }


        public void ExtendDuration()
        {
            if (IsInfinite)
                return;

            _expireTime +=
                _data.Duration;
        }


        // ============================================================
        // Stack
        // ============================================================

        public bool TryAddStack()
        {
            if (_stackCount >=
                _data.MaxStack)
            {
                return false;
            }

            _stackCount++;

            return true;
        }


        // ============================================================
        // Periodic
        // ============================================================

        public bool CanTick(
            float currentTime)
        {
            if (!HasPeriodicTick)
                return false;

            return currentTime >=
                _nextTickTime;
        }


        public float GetTickProcessUntilTime(
            float currentTime)
        {
            if (IsInfinite)
                return currentTime;

            return Mathf.Min(
                currentTime,
                _expireTime
            );
        }


        public void AdvanceTick(
            float interval)
        {
            if (interval <= 0f)
                return;

            _nextTickTime +=
                interval;
        }


        // ============================================================
        // Initialize
        // ============================================================

        private void InitializeDuration(
            float currentTime)
        {
            if (IsInfinite)
            {
                _expireTime =
                    float.PositiveInfinity;

                return;
            }

            _expireTime =
                currentTime +
                _data.Duration;
        }


        private void InitializePeriodic(
            float currentTime)
        {
            float interval =
                GetPeriodicInterval();

            if (interval <= 0f)
            {
                _nextTickTime =
                    0f;

                return;
            }

            _nextTickTime =
                currentTime +
                interval;
        }


        private float GetPeriodicInterval()
        {
            IReadOnlyList<EffectActionData> actions =
                _data.Actions;

            for (int i = 0;
                 i < actions.Count;
                 i++)
            {
                if (actions[i]
                    is PeriodicDamageEffectActionData periodicDamage)
                {
                    return periodicDamage.Interval;
                }

                if (actions[i]
                    is PeriodicHealEffectActionData periodicHeal)
                {
                    return periodicHeal.Interval;
                }
            }

            return 0f;
        }
    }
}