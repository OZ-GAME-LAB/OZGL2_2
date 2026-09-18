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

        private readonly EngagementExpansionPolicy _expansionPolicy;

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
            List<Unit_GroupAI> enemyGroups,
            EngagementExpansionPolicy expansionPolicy = null)
        {
            _expansionPolicy = expansionPolicy ?? new EngagementExpansionPolicy();
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


            // 탐지만으로 기존 교전끼리 병합하지 않는다. 신규 그룹 합류에도 같은 상한을 적용한다.
            if (!_expansionPolicy.Allows(
                GetParticipants(allyContext, allyGroup),
                GetParticipants(enemyContext, enemyGroup)))
                return false;

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


        // 스킬 요청 한 번에 선택된 상대 교전 하나만 검토한다.
        public SkillEngagementResult TryExpandForSkill(
            Unit_GroupAI sourceGroup,
            Unit_GroupAI targetGroup)
        {
            if (sourceGroup == null || targetGroup == null || sourceGroup.Team == targetGroup.Team)
                return SkillEngagementResult.Invalid;

            Unit_GroupAI ally = sourceGroup.Team == UnitTeam.Ally ? sourceGroup : targetGroup;
            Unit_GroupAI enemy = sourceGroup.Team == UnitTeam.Enemy ? sourceGroup : targetGroup;
            if (!IsValidAllyGroup(ally) || !IsValidEnemyGroup(enemy)
                || ally.Members.Count == 0 || enemy.Members.Count == 0)
                return SkillEngagementResult.Invalid;

            _groupEngagementMap.TryGetValue(sourceGroup, out EngagementContext source);
            _groupEngagementMap.TryGetValue(targetGroup, out EngagementContext target);
            if (source != null && ReferenceEquals(source, target))
                return SkillEngagementResult.AlreadyEngaged;

            List<Unit_GroupAI> sourceGroups = GetParticipants(source, sourceGroup);
            List<Unit_GroupAI> targetGroups = GetParticipants(target, targetGroup);
            if (!AreRegistered(sourceGroups) || !AreRegistered(targetGroups))
                return SkillEngagementResult.Invalid;
            if (!_expansionPolicy.Allows(sourceGroups, targetGroups))
                return SkillEngagementResult.Denied;

            if (source != null && target != null)
            {
                // 종료 처리 없이 이전하여 중간 Advancing 전환과 배정 해제를 방지한다.
                for (int i = 0; i < target.AllyGroups.Count; i++)
                    source.AddAllyGroup(target.AllyGroups[i]);
                for (int i = 0; i < target.EnemyGroups.Count; i++)
                    source.AddEnemyGroup(target.EnemyGroups[i]);
                for (int i = 0; i < targetGroups.Count; i++)
                    _groupEngagementMap[targetGroups[i]] = source;

                _engagements.Remove(target);
                RefreshEngagement(source);
                return SkillEngagementResult.Expanded;
            }

            if (source == null && target == null)
                CreateEngagement(ally, enemy);
            else
            {
                EngagementContext context = source ?? target;
                Unit_GroupAI joining = source == null ? sourceGroup : targetGroup;
                bool added = joining.Team == UnitTeam.Ally
                    ? TryAddAllyGroup(context, joining)
                    : TryAddEnemyGroup(context, joining);
                if (!added)
                    return SkillEngagementResult.Invalid;
            }

            return SkillEngagementResult.Expanded;
        }

        private bool AreRegistered(IReadOnlyList<Unit_GroupAI> groups)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                Unit_GroupAI group = groups[i];
                if (group == null || group.Members.Count == 0
                    || !(IsValidAllyGroup(group) || IsValidEnemyGroup(group)))
                    return false;
            }
            return true;
        }

        private static List<Unit_GroupAI> GetParticipants(
            EngagementContext context, Unit_GroupAI single)
        {
            var result = new List<Unit_GroupAI>();
            if (context == null)
                result.Add(single);
            else
            {
                result.AddRange(context.AllyGroups);
                result.AddRange(context.EnemyGroups);
            }
            return result;
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
            // 모든 상대 목록을 먼저 반영하고, 이후 상태 진입/배정을 허용한다.
            IReadOnlyList<Unit_GroupAI> allyGroups =
                new List<Unit_GroupAI>(context.AllyGroups);

            IReadOnlyList<Unit_GroupAI> enemyGroups =
                new List<Unit_GroupAI>(context.EnemyGroups);


            for (int i = 0;
                 i < allyGroups.Count;
                 i++)
            {
                Unit_GroupAI allyGroup =
                    allyGroups[i];

                if (allyGroup == null)
                    continue;

                allyGroup.SetEnemyGroups(
                    enemyGroups,
                    false
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
                    allyGroups,
                    false
                );
            }

            for (int i = 0; i < allyGroups.Count; i++)
                if (allyGroups[i] != null)
                    allyGroups[i].RefreshEngagementState();
            for (int i = 0; i < enemyGroups.Count; i++)
                if (enemyGroups[i] != null)
                    enemyGroups[i].RefreshEngagementState();
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