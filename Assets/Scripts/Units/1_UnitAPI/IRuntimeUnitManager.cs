using System;


namespace Units
{
    public interface IRuntimeUnitManager
    {
        // =========================
        // Events
        // =========================

        event Action<Unit_Gateway>
            UnitDied;


        // =========================
        // Unit Count
        // =========================

        int AllyUnitCount
        {
            get;
        }

        int EnemyUnitCount
        {
            get;
        }


        // =========================
        // Runtime Clear
        // =========================

        void ClearRuntime();
    }
}