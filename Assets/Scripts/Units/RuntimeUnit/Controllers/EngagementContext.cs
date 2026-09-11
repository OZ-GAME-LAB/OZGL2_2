using System.Collections.Generic;



namespace Units
{
    public class EngagementContext
    {
        // =========================
        // Runtime Groups
        // =========================

        private readonly List<Unit_GroupAI> _allyGroups =
            new();

        private readonly List<Unit_GroupAI> _enemyGroups =
            new();


        // =========================
        // Properties
        // =========================

        public IReadOnlyList<Unit_GroupAI> AllyGroups
            => _allyGroups;

        public IReadOnlyList<Unit_GroupAI> EnemyGroups
            => _enemyGroups;

        public bool IsEmpty
            => _allyGroups.Count == 0
            || _enemyGroups.Count == 0;


        // =========================
        // Constructor
        // =========================

        public EngagementContext(
            Unit_GroupAI allyGroup,
            Unit_GroupAI enemyGroup)
        {
            if (allyGroup != null)
            {
                _allyGroups.Add(
                    allyGroup
                );
            }

            if (enemyGroup != null)
            {
                _enemyGroups.Add(
                    enemyGroup
                );
            }
        }


        // =========================
        // Ally Group
        // =========================

        internal bool AddAllyGroup(
            Unit_GroupAI group)
        {
            if (group == null)
                return false;

            if (_allyGroups.Contains(group))
                return false;

            _allyGroups.Add(
                group
            );

            return true;
        }

        internal bool RemoveAllyGroup(
            Unit_GroupAI group)
        {
            if (group == null)
                return false;

            return _allyGroups.Remove(
                group
            );
        }


        // =========================
        // Enemy Group
        // =========================

        internal bool AddEnemyGroup(
            Unit_GroupAI group)
        {
            if (group == null)
                return false;

            if (_enemyGroups.Contains(group))
                return false;

            _enemyGroups.Add(
                group
            );

            return true;
        }

        internal bool RemoveEnemyGroup(
            Unit_GroupAI group)
        {
            if (group == null)
                return false;

            return _enemyGroups.Remove(
                group
            );
        }


        // =========================
        // Group Check
        // =========================

        public bool Contains(
            Unit_GroupAI group)
        {
            if (group == null)
                return false;

            return _allyGroups.Contains(group)
                || _enemyGroups.Contains(group);
        }

        public bool ContainsAllyGroup(
            Unit_GroupAI group)
        {
            return group != null
                && _allyGroups.Contains(group);
        }

        public bool ContainsEnemyGroup(
            Unit_GroupAI group)
        {
            return group != null
                && _enemyGroups.Contains(group);
        }
    }
}