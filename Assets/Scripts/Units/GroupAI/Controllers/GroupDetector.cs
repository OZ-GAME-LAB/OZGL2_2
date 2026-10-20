using System;
using System.Collections.Generic;
using UnityEngine;

namespace Units
{
    public class GroupDetector
    {
        // =========================
        // Constants
        // =========================

        private const int DetectionBufferSize =
            64;


        // =========================
        // References
        // =========================

        private CombatQueryContext _combatQueries;

        private readonly List<Unit_Gateway> _nearby = new();



        private readonly List<Unit_Gateway> _members;

        private readonly List<Unit_GroupAI> _enemyGroups;


        // =========================
        // Settings
        // =========================

        private readonly UnitTeam _team;

        private readonly float _detectionRange;

        private readonly ContactFilter2D _contactFilter;


        // =========================
        // Runtime
        // =========================

        private readonly Collider2D[] _detectionBuffer =
            new Collider2D[DetectionBufferSize];

        private readonly HashSet<Unit_GroupAI> _detectedGroups =
            new();


        // =========================
        // Events
        // =========================

        public event Action<Unit_GroupAI> NewEnemyGroupDetected;


        // =========================
        // Constructor
        // =========================

        public GroupDetector(
            UnitTeam team,
            List<Unit_Gateway> members,
            List<Unit_GroupAI> enemyGroups,
            LayerMask unitLayer,
            float detectionRange)
        {
            _team =
                team;

            _members =
                members;

            _enemyGroups =
                enemyGroups;

            _detectionRange =
                detectionRange;

            _contactFilter =
                new ContactFilter2D
                {
                    useLayerMask = true,
                    layerMask = unitLayer
                };
        }


        // =========================
        // Shared Combat Queries
        // =========================

        public void SetCombatQueries(CombatQueryContext queries)
        {
            _combatQueries = queries;
        }



        // =========================
        // Detection
        // =========================

        public void Detect()
        {
            _detectedGroups.Clear();

            for (int i = 0;
                 i < _members.Count;
                 i++)
            {
                Unit_Gateway member =
                    _members[i];

                if (member == null)
                    continue;

                DetectFromMember(
                    member
                );
            }

            ReportDetectedGroups();
        }


        private void DetectFromMember(
            Unit_Gateway member)
        {
            // 긴 사거리 스킬이 고정 탐지 반경 때문에 합류 기회를 잃지 않게 한다.
            float detectionRange = Mathf.Max(
                _detectionRange,
                member.EngagementEntryRange + CombatRangeUtility.Expansion(member.BodyRadius)
            );

            if (_combatQueries != null)
            {
                _combatQueries.CollectNearby(
                    member.Transform.position, Mathf.Max(_detectionRange, member.EngagementEntryRange),
                    member.BodyRadius, _nearby
                );

                for (int i = 0; i < _nearby.Count; i++)
                {
                    Unit_Gateway target = _nearby[i];
                    Unit_GroupAI group = target.GroupAI;

                    if (group != null && group.Team != _team && !_enemyGroups.Contains(group))
                        _detectedGroups.Add(group);
                }

                return;
            }

            int detectedCount =
                Physics2D.OverlapCircle(
                    member.Transform.position,
                    detectionRange,
                    _contactFilter,
                    _detectionBuffer
                );

            for (int i = 0;
                 i < detectedCount;
                 i++)
            {
                Collider2D detectedCollider =
                    _detectionBuffer[i];

                if (detectedCollider == null)
                    continue;

                Unit_Gateway detectedUnit =
                    detectedCollider.GetComponentInParent<Unit_Gateway>();

                if (detectedUnit == null)
                    continue;

                if (!detectedUnit.IsAlive)
                    continue;

                Unit_GroupAI detectedGroup =
                    detectedUnit.GroupAI;

                if (detectedGroup == null)
                    continue;

                if (detectedGroup.Team == _team)
                    continue;

                if (_enemyGroups.Contains(
                    detectedGroup))
                {
                    continue;
                }

                _detectedGroups.Add(
                    detectedGroup
                );
            }
        }


        // =========================
        // Report
        // =========================

        private void ReportDetectedGroups()
        {
            foreach (Unit_GroupAI detectedGroup
                     in _detectedGroups)
            {
                NewEnemyGroupDetected?.Invoke(
                    detectedGroup
                );
            }
        }
    }
}