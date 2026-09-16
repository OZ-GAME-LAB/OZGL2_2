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
            int detectedCount =
                Physics2D.OverlapCircle(
                    member.Transform.position,
                    _detectionRange,
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