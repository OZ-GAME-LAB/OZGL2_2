using System.Collections.Generic;

namespace Units
{
    public class EnemyGroupController
    {
        // =========================
        // References
        // =========================

        private readonly List<Unit_GroupAI> _enemyGroups;


        // =========================
        // Properties
        // =========================

        public bool IsEngaged =>
            _enemyGroups.Count > 0;


        // =========================
        // Constructor
        // =========================

        public EnemyGroupController(
            List<Unit_GroupAI> enemyGroups)
        {
            _enemyGroups = enemyGroups;
        }


        // =========================
        // Enemy Group Management
        // =========================

        public void SetEnemyGroups(
            IReadOnlyList<Unit_GroupAI> enemyGroups)
        {
            _enemyGroups.Clear();

            if (enemyGroups == null)
                return;

            for (int i = 0; i < enemyGroups.Count; i++)
            {
                Unit_GroupAI enemyGroup =
                    enemyGroups[i];

                if (enemyGroup == null)
                    continue;

                if (_enemyGroups.Contains(
                    enemyGroup))
                {
                    continue;
                }

                _enemyGroups.Add(
                    enemyGroup
                );
            }
        }

        public bool AddEnemyGroup(
            Unit_GroupAI enemyGroup)
        {
            if (enemyGroup == null)
                return false;

            if (_enemyGroups.Contains(
                enemyGroup))
            {
                return false;
            }

            _enemyGroups.Add(
                enemyGroup
            );

            return true;
        }

        public bool RemoveEnemyGroup(
            Unit_GroupAI enemyGroup)
        {
            if (enemyGroup == null)
                return false;

            return _enemyGroups.Remove(
                enemyGroup
            );
        }

        public void Clear()
        {
            _enemyGroups.Clear();
        }
    }
}