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
        // Battle Management
        // =========================

        void StartBattlePhase();

        void PauseBattle();

        void ResumeBattle(); // 불안정한 기능 가급적 사용하지 말 것


        // =========================
        // Runtime Clear
        // =========================

        void ClearRuntime();
    }
}