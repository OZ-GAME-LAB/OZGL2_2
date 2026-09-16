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

        event Action<UnitTeam> TeamWiped;


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

        bool GetRemain(
            out int enemy,
            out int allies
        );


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