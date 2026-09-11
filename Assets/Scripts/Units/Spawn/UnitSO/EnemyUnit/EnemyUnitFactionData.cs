using System;
using System.Collections.Generic;
using UnityEngine;



namespace Units
{
    [Serializable]
    public class EnemyUnitFactionData
    {
        // =========================
        // Faction
        // =========================

        [SerializeField]
        private EnemyUnitFaction _faction;


        // =========================
        // Units
        // =========================

        [SerializeField]
        private List<GameObject> _unitPrefabs =
            new();


        // =========================
        // Properties
        // =========================

        public EnemyUnitFaction Faction
            => _faction;

        public IReadOnlyList<GameObject> UnitPrefabs
            => _unitPrefabs;
    }
}