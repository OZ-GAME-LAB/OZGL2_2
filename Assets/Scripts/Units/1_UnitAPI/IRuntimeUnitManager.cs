using System;
using System.Collections.Generic;


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

        event Action<Unit_Gateway> BossSpawned;

        event Action<Unit_Gateway> BossDied;

        event Action<Unit_Gateway> BossRemoved;

        event Action AllBossesDefeated;


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

        int BossUnitCount
        {
            get;
        }

        IReadOnlyList<Unit_Gateway> BossUnits { get; }


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