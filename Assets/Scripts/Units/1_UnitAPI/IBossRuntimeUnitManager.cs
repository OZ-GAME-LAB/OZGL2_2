using System;
using System.Collections.Generic;
namespace Units
{
    public interface IBossRuntimeUnitManager
    {
        IReadOnlyList<Unit_Gateway> BossUnits { get; }
        int BossUnitCount { get; }
        event Action<Unit_Gateway> BossSpawned;
        event Action<Unit_Gateway> BossDied;
        event Action<Unit_Gateway> BossRemoved;
        event Action AllBossesDefeated;
    }
}
