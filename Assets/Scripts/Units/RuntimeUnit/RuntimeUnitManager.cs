using System.Collections.Generic;
using UnityEngine;


namespace Units
{
    public class RuntimeUnitManager : MonoBehaviour
    {
        // =========================
        // Runtime States
        // =========================

        private bool _battleStarted;


        // =========================
        // Runtime Units
        // =========================

        private readonly List<Unit_Gateway> _allyUnits =
            new();

        private readonly List<Unit_Gateway> _enemyUnits =
            new();


        // =========================
        // Runtime Groups
        // =========================

        private readonly List<Unit_GroupAI> _allyGroups =
            new();

        private readonly List<Unit_GroupAI> _enemyGroups =
            new();


        // =========================
        // Group IDs
        // =========================

        private int _nextAllyGroupId =
            1;

        private int _nextEnemyGroupId =
            1;


        // =========================
        // Engagements
        // =========================

        private readonly List<EngagementContext> _engagements =
            new();


        // =========================
        // Controllers
        // =========================

        private RuntimeUnitRegistrator _unitRegistrator;

        private EngagementController _engagementController;

        private AdvanceReferenceController _advanceReferenceController;


        // =========================
        // Properties
        // =========================

        public IReadOnlyList<Unit_Gateway> AllyUnits
            => _allyUnits;

        public IReadOnlyList<Unit_Gateway> EnemyUnits
            => _enemyUnits;

        public IReadOnlyList<Unit_GroupAI> AllyGroups
            => _allyGroups;

        public IReadOnlyList<Unit_GroupAI> EnemyGroups
            => _enemyGroups;

        public IReadOnlyList<EngagementContext> Engagements
            => _engagements;


        // =========================
        // Unity Lifecycle
        // =========================

        private void Awake()
        {
            InitializeControllers();
        }


        // =========================
        // Initialization
        // =========================

        private void InitializeControllers()
        {
            _unitRegistrator =
                new RuntimeUnitRegistrator(
                    _allyUnits,
                    _enemyUnits,
                    _allyGroups,
                    _enemyGroups
                );


            _engagementController =
                new EngagementController(
                    _engagements,
                    _allyGroups,
                    _enemyGroups
                );


            _advanceReferenceController =
                new AdvanceReferenceController(
                    _allyGroups,
                    _enemyGroups
                );
        }


        // =========================
        // Unit Management
        // =========================

        public bool RegisterUnit(
            Unit_Gateway unit)
        {
            if (!_unitRegistrator.RegisterUnit(
                unit))
            {
                return false;
            }


            SubscribeUnitEvents(
                unit
            );


            return true;
        }


        public bool UnregisterUnit(
            Unit_Gateway unit)
        {
            if (unit == null)
                return false;


            if (!_unitRegistrator.UnregisterUnit(
                unit))
            {
                return false;
            }


            UnsubscribeUnitEvents(
                unit
            );


            return true;
        }


        // =========================
        // Group Management
        // =========================

        public bool RegisterGroup(
            Unit_GroupAI group)
        {
            if (!_unitRegistrator.RegisterGroup(
                group))
            {
                return false;
            }


            SubscribeGroupEvents(
                group
            );


            SubscribeGroupMemberEvents(
                group
            );


            if (_battleStarted &&
                group.Members.Count > 0)
            {
                group.StartAdvancing();
            }


            Debug.Log(
                $"[RuntimeUnitManager] " +
                $"Group 등록 완료 : " +
                $"{group.name} / " +
                $"{group.Members.Count}명"
            );


            return true;
        }


        public bool UnregisterGroup(
            Unit_GroupAI group)
        {
            if (group == null)
                return false;


            if (!_unitRegistrator.UnregisterGroup(
                group))
            {
                return false;
            }


            UnsubscribeGroupEvents(
                group
            );


            _engagementController.RemoveGroup(
                group
            );


            return true;
        }


        // =========================
        // Unit Events
        // =========================

        private void SubscribeUnitEvents(
            Unit_Gateway unit)
        {
            if (unit == null)
                return;


            unit.Died +=
                HandleUnitDied;
        }


        private void UnsubscribeUnitEvents(
            Unit_Gateway unit)
        {
            if (unit == null)
                return;


            unit.Died -=
                HandleUnitDied;
        }


        private void SubscribeGroupMemberEvents(
            Unit_GroupAI group)
        {
            if (group == null)
                return;


            IReadOnlyList<Unit_Gateway> members =
                group.Members;


            for (int i = 0;
                 i < members.Count;
                 i++)
            {
                Unit_Gateway unit =
                    members[i];


                if (unit == null)
                    continue;


                SubscribeUnitEvents(
                    unit
                );
            }
        }


        // =========================
        // Group Events
        // =========================

        private void SubscribeGroupEvents(
            Unit_GroupAI group)
        {
            if (group == null)
                return;


            group.NewEnemyGroupDetected +=
                HandleNewEnemyGroupDetected;

            group.Eliminated +=
                HandleGroupEliminated;

            group.AdvanceReferenceRequested +=
                HandleAdvanceReferenceRequested;
        }


        private void UnsubscribeGroupEvents(
            Unit_GroupAI group)
        {
            if (group == null)
                return;


            group.NewEnemyGroupDetected -=
                HandleNewEnemyGroupDetected;

            group.Eliminated -=
                HandleGroupEliminated;

            group.AdvanceReferenceRequested -=
                HandleAdvanceReferenceRequested;
        }


        // =========================
        // Event Handlers
        // =========================

        private void HandleUnitDied(
            Unit_Gateway unit)
        {
            UnregisterUnit(
                unit
            );
        }


        private void HandleGroupEliminated(
            Unit_GroupAI group)
        {
            UnregisterGroup(
                group
            );
        }


        private void HandleNewEnemyGroupDetected(
            Unit_GroupAI sourceGroup,
            Unit_GroupAI detectedGroup)
        {
            if (sourceGroup == null ||
                detectedGroup == null)
            {
                return;
            }


            if (sourceGroup.Team ==
                detectedGroup.Team)
            {
                return;
            }


            if (sourceGroup.Team ==
                UnitTeam.Ally)
            {
                _engagementController.TryStartEngagement(
                    sourceGroup,
                    detectedGroup
                );

                return;
            }


            _engagementController.TryStartEngagement(
                detectedGroup,
                sourceGroup
            );
        }


        private void HandleAdvanceReferenceRequested(
            Unit_GroupAI sourceGroup)
        {
            if (sourceGroup == null)
                return;


            Unit_GroupAI reference =
                _advanceReferenceController.SelectReference(
                    sourceGroup
                );


            if (reference == null)
                return;


            sourceGroup.SetAdvanceReference(
                reference
            );
        }


        // =========================
        // Group ID
        // =========================

        public int GetNextGroupId(
            UnitTeam team)
        {
            switch (team)
            {
                case UnitTeam.Ally:
                    return _nextAllyGroupId++;

                case UnitTeam.Enemy:
                    return _nextEnemyGroupId++;

                default:
                    Debug.LogError(
                        $"[RuntimeUnitManager] " +
                        $"지원하지 않는 Team입니다. : {team}"
                    );

                    return -1;
            }
        }


        private void ResetGroupIds()
        {
            _nextAllyGroupId =
                1;

            _nextEnemyGroupId =
                1;
        }


        // =========================
        // Battle State Management
        // =========================

        public void StartBattlePhase()
        {
            if (_battleStarted)
                return;


            _battleStarted =
                true;


            StartAdvancingGroups(
                _allyGroups
            );

            StartAdvancingGroups(
                _enemyGroups
            );
        }


        private void StartAdvancingGroups(
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


                if (group.Members.Count == 0)
                    continue;


                group.StartAdvancing();
            }
        }


        // =========================
        // Runtime Clear
        // =========================

        public void ClearRuntime()
        {
            ClearGroups(
                _allyGroups
            );

            ClearGroups(
                _enemyGroups
            );


            ClearUnits(
                _allyUnits
            );

            ClearUnits(
                _enemyUnits
            );


            _engagements.Clear();


            _battleStarted =
                false;


            ResetGroupIds();
        }


        private void ClearGroups(
            List<Unit_GroupAI> groups)
        {
            for (int i =
                     groups.Count - 1;
                 i >= 0;
                 i--)
            {
                Unit_GroupAI group =
                    groups[i];


                if (group == null)
                {
                    groups.RemoveAt(
                        i
                    );

                    continue;
                }


                UnregisterGroup(
                    group
                );


                ClearGroupMembers(
                    group
                );


                Destroy(
                    group.gameObject
                );
            }
        }


        private void ClearGroupMembers(
            Unit_GroupAI group)
        {
            for (int i =
                     group.Members.Count - 1;
                 i >= 0;
                 i--)
            {
                Unit_Gateway unit =
                    group.Members[i];


                if (unit == null)
                    continue;


                group.RemoveMember(
                    unit
                );
            }
        }


        private void ClearUnits(
            List<Unit_Gateway> units)
        {
            for (int i =
                     units.Count - 1;
                 i >= 0;
                 i--)
            {
                Unit_Gateway unit =
                    units[i];


                if (unit == null)
                {
                    units.RemoveAt(
                        i
                    );

                    continue;
                }


                UnregisterUnit(
                    unit
                );


                Destroy(
                    unit.gameObject
                );
            }
        }
    }
}