using System.Collections.Generic;
using UnityEngine;



namespace Units
{
    public class EngagementController
    {
        // =========================
        // References
        // =========================

        private readonly List<EngagementContext> _engagements;

        private readonly List<Unit_GroupAI> _allyGroups;

        private readonly List<Unit_GroupAI> _enemyGroups;


        // =========================
        // Runtime
        // =========================

        private readonly Dictionary<Unit_GroupAI, EngagementContext>
            _groupEngagementMap =
            new();


        // =========================
        // Constructor
        // =========================

        public EngagementController(
            List<EngagementContext> engagements,
            List<Unit_GroupAI> allyGroups,
            List<Unit_GroupAI> enemyGroups)
        {
            _engagements =
                engagements;

            _allyGroups =
                allyGroups;

            _enemyGroups =
                enemyGroups;
        }


        // =========================
        // Engagement
        // =========================

        public bool TryStartEngagement(
            Unit_GroupAI allyGroup,
            Unit_GroupAI enemyGroup)
        {
            if (!IsValidAllyGroup(
                allyGroup))
            {
                return false;
            }

            if (!IsValidEnemyGroup(
                enemyGroup))
            {
                return false;
            }


            bool allyEngaged =
                _groupEngagementMap.TryGetValue(
                    allyGroup,
                    out EngagementContext allyContext
                );

            bool enemyEngaged =
                _groupEngagementMap.TryGetValue(
                    enemyGroup,
                    out EngagementContext enemyContext
                );


            if (!allyEngaged
                && !enemyEngaged)
            {
                CreateEngagement(
                    allyGroup,
                    enemyGroup
                );

                return true;
            }


            if (allyEngaged
                && enemyEngaged)
            {
                return ReferenceEquals(
                    allyContext,
                    enemyContext
                );
            }


            if (allyEngaged)
            {
                return TryAddEnemyGroup(
                    allyContext,
                    enemyGroup
                );
            }


            return TryAddAllyGroup(
                enemyContext,
                allyGroup
            );
        }


        // =========================
        // Engagement Creation
        // =========================

        private void CreateEngagement(
            Unit_GroupAI allyGroup,
            Unit_GroupAI enemyGroup)
        {
            EngagementContext context =
                new EngagementContext(
                    allyGroup,
                    enemyGroup
                );


            _engagements.Add(
                context
            );

            _groupEngagementMap.Add(
                allyGroup,
                context
            );

            _groupEngagementMap.Add(
                enemyGroup,
                context
            );


            Debug.Log(
                $"[EngagementController] 새로운 교전 발생"
                + $" | AllyGroup: {allyGroup.name}"
                + $" | EnemyGroup: {enemyGroup.name}"
                + $" | AllyGroups: {context.AllyGroups.Count}"
                + $" | EnemyGroups: {context.EnemyGroups.Count}"
            );


            RefreshEngagement(
                context
            );
        }


        // =========================
        // Group Addition
        // =========================

        private bool TryAddAllyGroup(
            EngagementContext context,
            Unit_GroupAI allyGroup)
        {
            if (context == null)
                return false;

            if (!IsValidAllyGroup(
                allyGroup))
            {
                return false;
            }

            if (_groupEngagementMap.ContainsKey(
                allyGroup))
            {
                return false;
            }

            if (!context.AddAllyGroup(
                allyGroup))
            {
                return false;
            }


            _groupEngagementMap.Add(
                allyGroup,
                context
            );


            Debug.Log(
                $"[EngagementController] 교전 확대"
                + $" | AllyGroup 합류: {allyGroup.name}"
                + $" | AllyGroups: {context.AllyGroups.Count}"
                + $" | EnemyGroups: {context.EnemyGroups.Count}"
            );


            RefreshEngagement(
                context
            );

            return true;
        }


        private bool TryAddEnemyGroup(
            EngagementContext context,
            Unit_GroupAI enemyGroup)
        {
            if (context == null)
                return false;

            if (!IsValidEnemyGroup(
                enemyGroup))
            {
                return false;
            }

            if (_groupEngagementMap.ContainsKey(
                enemyGroup))
            {
                return false;
            }

            if (!context.AddEnemyGroup(
                enemyGroup))
            {
                return false;
            }


            _groupEngagementMap.Add(
                enemyGroup,
                context
            );


            Debug.Log(
                $"[EngagementController] 교전 확대"
                + $" | EnemyGroup 합류: {enemyGroup.name}"
                + $" | AllyGroups: {context.AllyGroups.Count}"
                + $" | EnemyGroups: {context.EnemyGroups.Count}"
            );


            RefreshEngagement(
                context
            );

            return true;
        }


        // =========================
        // Group Removal
        // =========================

        public bool RemoveGroup(
            Unit_GroupAI group)
        {
            if (group == null)
                return false;

            if (!_groupEngagementMap.TryGetValue(
                group,
                out EngagementContext context))
            {
                return false;
            }


            bool removed =
                false;


            if (context.ContainsAllyGroup(
                group))
            {
                removed =
                    context.RemoveAllyGroup(
                        group
                    );
            }
            else if (context.ContainsEnemyGroup(
                group))
            {
                removed =
                    context.RemoveEnemyGroup(
                        group
                    );
            }


            if (!removed)
                return false;


            _groupEngagementMap.Remove(
                group
            );


            group.ClearEnemyGroups();


            Debug.Log(
                $"[EngagementController] 그룹 이탈"
                + $" | Group: {group.name}"
                + $" | Team: {group.Team}"
                + $" | AllyGroups: {context.AllyGroups.Count}"
                + $" | EnemyGroups: {context.EnemyGroups.Count}"
            );


            if (context.IsEmpty)
            {
                EndEngagement(
                    context
                );

                return true;
            }


            RefreshEngagement(
                context
            );

            return true;
        }


        // =========================
        // Engagement End
        // =========================

        private void EndEngagement(
            EngagementContext context)
        {
            if (context == null)
                return;


            Debug.Log(
                $"[EngagementController] 교전 종료"
                + $" | AllyGroups: {context.AllyGroups.Count}"
                + $" | EnemyGroups: {context.EnemyGroups.Count}"
            );


            ClearGroupMappings(
                context.AllyGroups
            );

            ClearGroupMappings(
                context.EnemyGroups
            );


            ClearEnemyGroups(
                context.AllyGroups
            );

            ClearEnemyGroups(
                context.EnemyGroups
            );


            _engagements.Remove(
                context
            );
        }


        // =========================
        // Engagement Refresh
        // =========================

        private void RefreshEngagement(
            EngagementContext context)
        {
            IReadOnlyList<Unit_GroupAI> allyGroups =
                context.AllyGroups;

            IReadOnlyList<Unit_GroupAI> enemyGroups =
                context.EnemyGroups;


            for (int i = 0;
                 i < allyGroups.Count;
                 i++)
            {
                Unit_GroupAI allyGroup =
                    allyGroups[i];

                if (allyGroup == null)
                    continue;

                allyGroup.SetEnemyGroups(
                    enemyGroups
                );
            }


            for (int i = 0;
                 i < enemyGroups.Count;
                 i++)
            {
                Unit_GroupAI enemyGroup =
                    enemyGroups[i];

                if (enemyGroup == null)
                    continue;

                enemyGroup.SetEnemyGroups(
                    allyGroups
                );
            }
        }


        // =========================
        // Mapping
        // =========================

        private void ClearGroupMappings(
            IReadOnlyList<Unit_GroupAI> groups)
        {
            for (int i = 0;
                 i < groups.Count;
                 i++)
            {
                Unit_GroupAI group =
                    groups[i];

                if (group == null)
                    continue;

                _groupEngagementMap.Remove(
                    group
                );
            }
        }


        // =========================
        // Enemy Group Clear
        // =========================

        private void ClearEnemyGroups(
            IReadOnlyList<Unit_GroupAI> groups)
        {
            for (int i = 0;
                 i < groups.Count;
                 i++)
            {
                Unit_GroupAI group =
                    groups[i];

                if (group == null)
                    continue;

                group.ClearEnemyGroups();
            }
        }


        // =========================
        // Validation
        // =========================

        private bool IsValidAllyGroup(
            Unit_GroupAI group)
        {
            return group != null
                && group.Team == UnitTeam.Ally
                && _allyGroups.Contains(group);
        }


        private bool IsValidEnemyGroup(
            Unit_GroupAI group)
        {
            return group != null
                && group.Team == UnitTeam.Enemy
                && _enemyGroups.Contains(group);
        }
    }
}