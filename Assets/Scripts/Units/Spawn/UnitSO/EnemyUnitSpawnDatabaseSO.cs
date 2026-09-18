using System.Collections.Generic;
using UnityEngine;


namespace Units
{
    [CreateAssetMenu(
        fileName = "EnemyUnitSpawnDatabase",
        menuName = "Units/Spawn/Enemy Unit Database")]
    public class EnemyUnitSpawnDatabaseSO : ScriptableObject
    {
        [SerializeField]
        private List<EnemyUnitFactionData> _factions =
            new();


        public IReadOnlyList<EnemyUnitFactionData> Factions
            => _factions;
    }
}