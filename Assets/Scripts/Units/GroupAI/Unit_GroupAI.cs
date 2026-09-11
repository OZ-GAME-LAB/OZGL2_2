using System;
using System.Collections.Generic;
using UnityEngine;


namespace Units
{
    public class Unit_GroupAI : MonoBehaviour
    {
        // =========================
        // Settings
        // =========================

        [SerializeField]
        private LayerMask _unitLayer;

        [SerializeField]
        private float _detectionRange =
            3f;

        [SerializeField]
        private float _groupUpdateInterval =
            0.3f;

        [SerializeField]
        private float _advanceDistance =
            5f;

        [SerializeField]
        private float _advanceThreshold =
            0.5f;

        [SerializeField]
        private float _advanceReferenceRequestInterval =
            0.5f;


        // =========================
        // Runtime Timer
        // =========================

        private float _groupUpdateTimer;

        private float _advanceReferenceRequestTimer;


        // =========================
        // State
        // =========================

        private UnitTeam _team;

        private GroupAIState _state =
            GroupAIState.Idle;


        // =========================
        // Runtime
        // =========================

        private readonly List<Unit_Gateway> _members =
            new();

        private Unit_GroupAI _advanceReference;

        private readonly List<Unit_GroupAI> _enemyGroups =
            new();

        private readonly Dictionary<
            Unit_Gateway,
            UnitAssignmentContext>
            _assignmentContexts =
            new();


        // =========================
        // Controllers
        // =========================

        private MemberController _memberController;

        private EnemyGroupController _enemyGroupController;

        private TargetSelector _targetSelector;

        private PositionAssigner _positionAssigner;

        private AssignmentBuilder _assignmentBuilder;

        private GroupDetector _groupDetector;

        private AdvanceController _advanceController;


        // =========================
        // Events
        // =========================

        public event Action<
            Unit_GroupAI,
            Unit_GroupAI>
            NewEnemyGroupDetected;

        public event Action<Unit_GroupAI>
            Eliminated;

        public event Action<Unit_GroupAI>
            AdvanceReferenceRequested;


        // =========================
        // Properties
        // =========================

        public IReadOnlyList<Unit_Gateway> Members
            => _members;

        public Unit_GroupAI AdvanceReference
            => _advanceReference;

        public IReadOnlyList<Unit_GroupAI> EnemyGroups
            => _enemyGroups;

        public IReadOnlyDictionary<
            Unit_Gateway,
            UnitAssignmentContext>
            AssignmentContexts
            => _assignmentContexts;

        public UnitTeam Team
            => _team;

        public GroupAIState State
            => _state;


        public Vector2 CenterPosition
        {
            get
            {
                if (_members.Count == 0)
                    return transform.position;


                Vector2 sum =
                    Vector2.zero;

                int count =
                    0;


                for (int i = 0;
                     i < _members.Count;
                     i++)
                {
                    Unit_Gateway member =
                        _members[i];

                    if (member == null)
                        continue;


                    sum +=
                        (Vector2)member.Transform.position;

                    count++;
                }


                if (count == 0)
                    return transform.position;


                return sum / count;
            }
        }


        // =========================
        // Unity Lifecycle
        // =========================

        private void Update()
        {
            if (_state != GroupAIState.Advancing &&
                _state != GroupAIState.Engaged)
            {
                return;
            }


            if (_state == GroupAIState.Advancing)
            {
                UpdateAdvanceReferenceRequest();
            }


            _groupUpdateTimer +=
                Time.deltaTime;


            if (_groupUpdateTimer <
                _groupUpdateInterval)
            {
                return;
            }


            _groupUpdateTimer =
                0f;


            if (_state == GroupAIState.Advancing)
            {
                _advanceController
                    .UpdateAdvance();
            }


            _groupDetector?.Detect();
        }


        // =========================
        // Initialization
        // =========================

        public void Initialize(
            UnitTeam team)
        {
            _team =
                team;


            InitializeControllers();
        }


        private void InitializeControllers()
        {
            _memberController =
                new MemberController(
                    _members,
                    _assignmentContexts
                );


            _enemyGroupController =
                new EnemyGroupController(
                    _enemyGroups
                );


            _targetSelector =
                new TargetSelector(
                    _enemyGroups,
                    _assignmentContexts
                );


            _positionAssigner =
                new PositionAssigner(
                    _assignmentContexts
                );


            _assignmentBuilder =
                new AssignmentBuilder(
                    _assignmentContexts
                );


            _groupDetector =
                new GroupDetector(
                    _team,
                    _members,
                    _enemyGroups,
                    _unitLayer,
                    _detectionRange
                );


            _advanceController =
                new AdvanceController(
                    _members,
                    _advanceDistance,
                    _advanceThreshold
                );


            _groupDetector.NewEnemyGroupDetected +=
                HandleNewEnemyGroupDetected;

            _advanceController.AdvanceReferenceRequired +=
                HandleAdvanceReferenceRequired;
        }


        // =========================
        // Member Management
        // =========================

        public bool AddMember(
            Unit_Gateway unit)
        {
            if (!_memberController.Add(
                unit))
            {
                return false;
            }


            unit.SetGroupAI(
                this
            );


            unit.Died +=
                HandleMemberDied;

            unit.RequestFullAssignmentEvent +=
                HandleFullAssignmentRequested;

            unit.RequestPositionAssignmentEvent +=
                HandlePositionAssignmentRequested;


            if (_state == GroupAIState.Engaged)
            {
                AssignUnit(
                    unit
                );

                unit.StartAI();
            }


            return true;
        }


        public bool RemoveMember(
            Unit_Gateway unit)
        {
            if (!_memberController.Remove(
                unit))
            {
                return false;
            }


            unit.Died -=
                HandleMemberDied;

            unit.RequestFullAssignmentEvent -=
                HandleFullAssignmentRequested;

            unit.RequestPositionAssignmentEvent -=
                HandlePositionAssignmentRequested;


            unit.ClearGroupAI();


            CheckEliminated();


            return true;
        }


        private void HandleMemberDied(
            Unit_Gateway unit)
        {
            RemoveMember(
                unit
            );
        }


        private void CheckEliminated()
        {
            if (_members.Count > 0)
                return;

            Debug.Log(
                $"[GroupAI] {name} → 그룹이 전멸했습니다."
            );

            Eliminated?.Invoke(
                this
            );
        }


        // =========================
        // Detection
        // =========================

        public void DetectEnemyGroups()
        {
            _groupDetector?.Detect();
        }


        private void HandleNewEnemyGroupDetected(
            Unit_GroupAI enemyGroup)
        {
            if (enemyGroup == null)
                return;


            Debug.Log(
                $"[GroupAI] {name} → 새로운 적 그룹 감지 : {enemyGroup.name}"
            );


            NewEnemyGroupDetected?.Invoke(
                this,
                enemyGroup
            );
        }


        // =========================
        // Engagement
        // =========================

        public void SetEnemyGroups(
            IReadOnlyList<Unit_GroupAI> enemyGroups)
        {
            _enemyGroupController.SetEnemyGroups(
                enemyGroups
            );


            UpdateEngagementState();
        }


        public void ClearEnemyGroups()
        {
            _enemyGroupController.Clear();


            UpdateEngagementState();
        }


        private void UpdateEngagementState()
        {
            if (_enemyGroupController.IsEngaged)
            {
                EnterEngaged();

                return;
            }


            EnterAdvancing();
        }


        // =========================
        // State Management
        // =========================

        public void StartAdvancing()
        {
            EnterAdvancing();
        }


        private void EnterAdvancing()
        {
            if (_state == GroupAIState.Advancing)
                return;


            PauseAllUnitAI();

            ClearAllUnitAssignments();

            ClearAdvanceReference();


            SetState(
                GroupAIState.Advancing
            );


            _advanceReferenceRequestTimer =
                0f;


            RequestAdvanceReference();
        }


        private void EnterEngaged()
        {
            if (_state == GroupAIState.Engaged)
                return;


            SetState(
                GroupAIState.Engaged
            );


            _advanceReferenceRequestTimer =
                0f;


            _advanceController.StopAdvance();


            ClearAdvanceReference();


            AssignAllUnits();


            StartAllUnitAI();
        }


        private void SetState(
            GroupAIState state)
        {
            if (_state == state)
                return;


            _state =
                state;
        }


        // =========================
        // Assignment
        // =========================

        private void AssignAllUnits()
        {
            for (int i = 0;
                 i < _members.Count;
                 i++)
            {
                Unit_Gateway unit =
                    _members[i];


                if (unit == null)
                    continue;


                AssignUnit(
                    unit
                );
            }
        }


        private void AssignUnit(
            Unit_Gateway unit)
        {
            if (!TryGetAssignmentContext(
                unit,
                out UnitAssignmentContext context))
            {
                return;
            }


            context.Clear();


            if (!_targetSelector.TryAssignTarget(
                unit))
            {
                return;
            }


            if (!_positionAssigner.TryAssignPosition(
                unit))
            {
                return;
            }


            if (!_assignmentBuilder.TryBuild(
                unit))
            {
                return;
            }


            ApplyAssignment(
                unit,
                context
            );
        }


        private void ReassignPosition(
            Unit_Gateway unit)
        {
            if (!TryGetAssignmentContext(
                unit,
                out UnitAssignmentContext context))
            {
                return;
            }


            if (!context.HasTarget)
            {
                AssignUnit(
                    unit
                );

                return;
            }


            context.ClearPosition();


            if (!_positionAssigner.TryAssignPosition(
                unit))
            {
                AssignUnit(
                    unit
                );

                return;
            }


            if (!_assignmentBuilder.TryBuild(
                unit))
            {
                AssignUnit(
                    unit
                );

                return;
            }


            ApplyAssignment(
                unit,
                context
            );
        }


        private void ApplyAssignment(
            Unit_Gateway unit,
            UnitAssignmentContext context)
        {
            if (!context.HasAssignment)
                return;


            unit.SetUnitAssignment(
                context.Assignment.Value
            );
        }


        private bool TryGetAssignmentContext(
            Unit_Gateway unit,
            out UnitAssignmentContext context)
        {
            context =
                null;


            if (unit == null)
                return false;


            return _assignmentContexts.TryGetValue(
                unit,
                out context
            );
        }


        private void ClearAllUnitAssignments()
        {
            for (int i = 0;
                 i < _members.Count;
                 i++)
            {
                Unit_Gateway unit =
                    _members[i];


                if (unit == null)
                    continue;


                unit.ClearUnitAssignment();


                if (_assignmentContexts.TryGetValue(
                    unit,
                    out UnitAssignmentContext context))
                {
                    context.Clear();
                }
            }
        }


        // =========================
        // Assignment Request
        // =========================

        private void HandleFullAssignmentRequested(
            Unit_Gateway unit)
        {
            if (!CanProcessAssignmentRequest(
                unit))
            {
                return;
            }


            AssignUnit(
                unit
            );
        }


        private void HandlePositionAssignmentRequested(
            Unit_Gateway unit)
        {
            if (!CanProcessAssignmentRequest(
                unit))
            {
                return;
            }


            ReassignPosition(
                unit
            );
        }


        private bool CanProcessAssignmentRequest(
            Unit_Gateway unit)
        {
            if (unit == null)
                return false;


            if (_state != GroupAIState.Engaged)
                return false;


            if (!_memberController.Contains(
                unit))
            {
                return false;
            }


            return true;
        }


        // =========================
        // Advance
        // =========================

        private void UpdateAdvanceReferenceRequest()
        {
            if (_advanceReference != null)
            {
                _advanceReferenceRequestTimer =
                    0f;

                return;
            }


            _advanceReferenceRequestTimer +=
                Time.deltaTime;


            if (_advanceReferenceRequestTimer <
                _advanceReferenceRequestInterval)
            {
                return;
            }


            _advanceReferenceRequestTimer =
                0f;


            RequestAdvanceReference();
        }


        private void RequestAdvanceReference()
        {
            AdvanceReferenceRequested?.Invoke(
                this
            );
        }


        private void ClearAdvanceReference()
        {
            _advanceReference =
                null;


            _advanceController
                .ClearAdvanceReference();
        }


        public void SetAdvanceReference(
            Unit_GroupAI reference)
        {
            if (reference == null)
                return;


            if (reference.Team == _team)
                return;


            _advanceReference =
                reference;


            _advanceReferenceRequestTimer =
                0f;


            _advanceController.SetAdvanceReference(
                reference
            );


            _advanceController.StartAdvance();
        }


        private void HandleAdvanceReferenceRequired()
        {
            if (_state != GroupAIState.Advancing)
                return;


            ClearAdvanceReference();


            _advanceReferenceRequestTimer =
                0f;


            RequestAdvanceReference();
        }


        // =========================
        // AI Handling
        // =========================

        private void StartAllUnitAI()
        {
            for (int i = 0;
                 i < _members.Count;
                 i++)
            {
                Unit_Gateway unit =
                    _members[i];


                if (unit == null)
                    continue;


                unit.StartAI();
            }
        }


        private void PauseAllUnitAI()
        {
            for (int i = 0;
                 i < _members.Count;
                 i++)
            {
                Unit_Gateway unit =
                    _members[i];


                if (unit == null)
                    continue;


                unit.PauseAI();
            }
        }
    }
}