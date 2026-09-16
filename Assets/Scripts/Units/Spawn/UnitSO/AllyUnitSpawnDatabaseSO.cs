using System.Collections.Generic;
using UnityEngine;


namespace Units
{
    [CreateAssetMenu(
        fileName = "AllyUnitSpawnDatabase",
        menuName = "Units/Spawn/Ally Unit Database")]
    public class AllyUnitSpawnDatabaseSO : ScriptableObject
    {
        [SerializeField]
        private List<AllyUnitClassData> _classes =
            new();


        public IReadOnlyList<AllyUnitClassData> Classes
            => _classes;
    }
}