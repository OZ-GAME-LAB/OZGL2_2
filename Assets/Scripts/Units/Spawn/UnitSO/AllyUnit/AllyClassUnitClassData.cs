using System;
using System.Collections.Generic;
using UnityEngine;


namespace Units
{
    [Serializable]
    public class AllyUnitClassData
    {
        // =========================
        // Class
        // =========================

        [SerializeField]
        private AllyUnitClass _unitClass;


        // =========================
        // Units
        // =========================

        [SerializeField]
        private List<GameObject> _unitPrefabs =
            new();


        // =========================
        // Properties
        // =========================

        public AllyUnitClass UnitClass
            => _unitClass;

        public IReadOnlyList<GameObject> UnitPrefabs
            => _unitPrefabs;
    }
}