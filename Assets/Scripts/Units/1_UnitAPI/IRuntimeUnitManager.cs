using System;


namespace Units
{
    public interface IRuntimeUnitManager
    {
        // =========================
        // Events
        // =========================

        event Action PreparationCompleted;

        event Action<Unit_Gateway> UnitDied;


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
        // Unit Count
        // =========================

        void StartBattlePhase();


        // =========================
        // Runtime Clear
        // =========================

        void ClearRuntime();
    }
}