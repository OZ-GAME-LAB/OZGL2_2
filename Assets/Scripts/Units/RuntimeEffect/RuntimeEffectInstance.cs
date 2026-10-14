using UnityEngine;



namespace Units
{
    public class RuntimeEffectInstance
    {
        // ============================================================
        // Data
        // ============================================================

        public EffectData Data { get; }

        public Unit_Gateway Source { get; }

        public Unit_Gateway Target { get; }


        // ============================================================
        // Runtime
        // ============================================================

        public int StackCount { get; private set; }

        public float AppliedTime { get; private set; }

        public float ExpireTime { get; private set; }


        // ============================================================
        // Properties
        // ============================================================

        public bool IsInfinite =>
            Data.DurationType == EffectDurationType.Infinite;

        public bool IsExpired(float currentTime)
        {
            return !IsInfinite &&
                   currentTime >= ExpireTime;
        }

        public float GetRemainingDuration(float currentTime)
        {
            if (IsInfinite)
                return float.PositiveInfinity;

            return Mathf.Max(
                0f,
                ExpireTime - currentTime
            );
        }

        // ============================================================
        // Management
        // ============================================================

        public void Refresh(float currentTime)
        {
            if (IsInfinite)
                return;

            ExpireTime =
                currentTime + Data.Duration;
        }

        public void Extend()
        {
            if (IsInfinite)
                return;

            ExpireTime +=
                Data.Duration;
        }

        public bool TryAddStack()
        {
            if (StackCount >= Data.MaxStack)
                return false;

            StackCount++;

            return true;
        }
    }
}