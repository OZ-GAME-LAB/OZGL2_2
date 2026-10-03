using System;
using UnityEngine;
using Units;

namespace Game.QA
{
    public enum QABattlePhase { Preparing, Ready, Fighting, Paused, Finished }

    [Serializable]
    public sealed class QAUnitEntry
    {
        public int Id;
        public UnitTeam Team;
        public GameObject Prefab;
        public string Label;
        public Vector2 Position;
        [NonSerialized] public Unit_Gateway Unit;
    }

    public sealed class QAUnitOption
    {
        public UnitTeam Team;
        public GameObject Prefab;
        public string Label;
        public string UnavailableReason;
        public bool Available => Prefab != null && string.IsNullOrEmpty(UnavailableReason);
    }
}
