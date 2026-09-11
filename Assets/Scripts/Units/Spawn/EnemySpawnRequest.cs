using System;
using UnityEngine;



namespace Units
{
    // ================================================================
    // Enemy Spawn Request
    // ================================================================

    [Serializable]
    public class EnemySpawnRequest
    {
        [SerializeField]
        private EnemyUnitType _unitType;

        [SerializeField]
        private int _count;


        public EnemyUnitType UnitType
            => _unitType;

        public int Count
            => _count;
    }
}
