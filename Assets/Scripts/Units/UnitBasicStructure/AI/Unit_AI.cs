using System.Collections.Generic;
using UnityEngine;



namespace Units
{
    public class Unit_AI : MonoBehaviour
    {
        // ============================================================
        // Reference
        // ============================================================

        private Unit_Core _core;


        // ============================================================
        // AI
        // ============================================================

        private UnitAIActionSelector _actionSelector;

        private readonly Dictionary<
            UnitAIActionType,
            IUnitAIAction> _actions = new();

        private bool _currentActionEnded;


        // ============================================================
        // Settings
        // ============================================================

        [SerializeField]
        private float _thinkInterval = 0.1f;


        // ============================================================
        // Assignment
        // ============================================================

        private UnitAssignment? _currentAssignment;


        // ============================================================
        // Runtime State
        // ============================================================

        private IUnitAIAction _currentAction;

        private float _nextThinkTime;

        private bool _isRunning;

        private bool _movementCompleted;


        // ============================================================
        // Properties
        // ============================================================

        public ICombatTarget CurrentTarget
            => _currentAssignment?.Target;

        public bool HasAssignment
            => _currentAssignment.HasValue;

        public UnitAIActionType CurrentActionType
            => _currentAction != null
                ? _currentAction.ActionType
                : UnitAIActionType.Idle;


        // ============================================================
        // Initialize
        // ============================================================

        public void Initialize(
            Unit_Core core)
        {
            _core = core;


            InitializeSelector();

            InitializeActions();

            BindEvents();


            _currentAssignment =
                null;

            _nextThinkTime =
                0f;

            _currentActionEnded =
                false;

            _movementCompleted =
                false;

            _isRunning =
                false;


            ChangeAction(
                UnitAIActionType.Idle
            );
        }


        private void InitializeSelector()
        {
            _actionSelector =
                new UnitAIActionSelector(
                    _core
                );
        }


        private void InitializeActions()
        {
            _actions.Clear();


            _actions.Add(
                UnitAIActionType.Idle,
                new UnitAIAction_Idle(
                    _core,
                    _actionSelector
                )
            );


            _actions.Add(
                UnitAIActionType.Move,
                new UnitAIAction_Move(
                    _core,
                    _actionSelector
                )
            );


            _actions.Add(
                UnitAIActionType.BasicAttack,
                new UnitAIAction_BasicAttack(
                    _core
                )
            );


            _actions.Add(
                UnitAIActionType.ActiveSkill,
                new UnitAIAction_ActiveSkill(
                    _core
                )
            );
        }


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Update()
        {
            if (!_isRunning)
                return;


            UpdateThinking();
        }


        private void OnDestroy()
        {
            UnbindEvents();
        }


        // ============================================================
        // Events
        // ============================================================

        private void BindEvents()
        {
            if (_core == null)
                return;


            _core.MovementCompleted +=
                OnMovementCompleted;

            _core.MovementFailed +=
                OnMovementFailed;

            _core.BasicAttackCompleted +=
                OnBasicAttackCompleted;

            _core.ActiveSkillCompleted +=
                OnActiveSkillCompleted;
        }


        private void UnbindEvents()
        {
            if (_core == null)
                return;


            _core.MovementCompleted -=
                OnMovementCompleted;

            _core.MovementFailed -=
                OnMovementFailed;

            _core.BasicAttackCompleted -=
                OnBasicAttackCompleted;

            _core.ActiveSkillCompleted -=
                OnActiveSkillCompleted;
        }


        // ============================================================
        // Assignment
        // ============================================================

        public void SetUnitAssignment(
            UnitAssignment assignment)
        {
            _currentAssignment =
                assignment;

            _currentActionEnded =
                true;

            _movementCompleted =
                false;


            EvaluateAction();
        }


        public void ClearUnitAssignment()
        {
            _currentAssignment =
                null;

            _currentActionEnded =
                false;

            _movementCompleted =
                false;


            ChangeAction(
                UnitAIActionType.Idle
            );
        }


        // ============================================================
        // Thinking
        // ============================================================

        private void UpdateThinking()
        {
            if (_core == null)
                return;

            if (_currentAction == null)
                return;

            if (Time.time < _nextThinkTime)
                return;


            _nextThinkTime =
                Time.time + _thinkInterval;


            if (!ValidateCurrentAssignment())
                return;


            if (_currentActionEnded)
            {
                EvaluateAction();

                return;
            }


            UnitAIActionType? nextAction =
                _currentAction.Evaluate(
                    _currentAssignment
                );


            if (!nextAction.HasValue)
                return;


            ChangeAction(
                nextAction.Value
            );
        }


        // ============================================================
        // Decision
        // ============================================================

        private void EvaluateAction()
        {
            if (!HasAssignment)
            {
                ChangeAction(
                    UnitAIActionType.Idle
                );

                return;
            }


            UnitAssignment assignment =
                _currentAssignment.Value;


            if (!IsTargetValid(
                assignment.Target))
            {
                RequestFullAssignment();

                return;
            }


            UnitAIActionType nextAction =
                _actionSelector.SelectAction(
                    assignment
                );


            if (ShouldRequestPositionAssignment(
                nextAction))
            {
                RequestPositionAssignment();

                return;
            }


            _movementCompleted =
                false;


            ChangeAction(
                nextAction
            );
        }


        // ============================================================
        // Action
        // ============================================================

        private void ChangeAction(
            UnitAIActionType actionType)
        {
            if (!_actions.TryGetValue(
                actionType,
                out IUnitAIAction nextAction))
            {
                return;
            }


            if (_currentAction == nextAction
                && !_currentActionEnded)
            {
                return;
            }


            _currentActionEnded =
                false;


            _currentAction?.Exit();


            _currentAction =
                nextAction;


            _currentAction.Enter(
                _currentAssignment
            );
        }


        // ============================================================
        // Action Complete
        // ============================================================

        private void OnMovementCompleted()
        {
            if (!IsCurrentAction(
                UnitAIActionType.Move))
            {
                return;
            }


            _movementCompleted =
                true;

            _currentActionEnded =
                true;
        }

        private void OnMovementFailed()
        {
            if (!IsCurrentAction(UnitAIActionType.Move))
                return;

            RequestPositionAssignment();
        }


        private void OnBasicAttackCompleted()
        {
            if (!IsCurrentAction(
                UnitAIActionType.BasicAttack))
            {
                return;
            }


            _currentActionEnded =
                true;
        }


        private void OnActiveSkillCompleted()
        {
            if (!IsCurrentAction(
                UnitAIActionType.ActiveSkill))
            {
                return;
            }


            _currentActionEnded =
                true;
        }


        // ============================================================
        // Assignment Validation
        // ============================================================

        private bool ValidateCurrentAssignment()
        {
            if (!HasAssignment)
                return true;


            UnitAssignment assignment =
                _currentAssignment.Value;


            if (IsTargetValid(
                assignment.Target))
            {
                return true;
            }


            RequestFullAssignment();

            return false;
        }


        private bool ShouldRequestPositionAssignment(
            UnitAIActionType nextAction)
        {
            if (!_movementCompleted)
                return false;


            return nextAction
                == UnitAIActionType.Move;
        }


        // ============================================================
        // Assignment Request
        // ============================================================

        private void RequestFullAssignment()
        {
            ClearUnitAssignment();

            _core.RequestFullAssignment();
        }


        private void RequestPositionAssignment()
        {
            _movementCompleted =
                false;

            _currentActionEnded =
                false;


            _core.StopMovement();

            _core.RequestPositionAssignment();
        }


        // ============================================================
        // Start AI
        // ============================================================

        public void StartAI()
        {
            if (_isRunning)
                return;


            _isRunning =
                true;

            _nextThinkTime =
                0f;
        }


        // ============================================================
        // Stop
        // ============================================================

        public void Stop()
        {
            if (!_isRunning)
                return;


            _isRunning =
                false;

            _currentAssignment =
                null;

            _currentActionEnded =
                false;

            _movementCompleted =
                false;


            _currentAction?.Exit();


            _currentAction =
                null;
        }


        public void PauseAI()
        {
            _isRunning =
                false;

            _currentActionEnded =
                false;

            _movementCompleted =
                false;


            _currentAction?.Exit();


            ChangeAction(
                UnitAIActionType.Idle
            );
        }


        // ============================================================
        // Validation
        // ============================================================

        private bool IsCurrentAction(
            UnitAIActionType actionType)
        {
            return _currentAction != null
                && _currentAction.ActionType
                == actionType;
        }


        private bool IsTargetValid(
            ICombatTarget target)
        {
            return target != null
                && target.IsTargetable
                && target.Transform != null;
        }


        // ============================================================
        // Gizmo
        // ============================================================

#if UNITY_EDITOR

        private void OnDrawGizmos()
        {
            if (!_isRunning)
                return;

            if (!_currentAssignment.HasValue)
                return;

            if (_currentAction == null)
                return;


            UnitAssignment assignment =
                _currentAssignment.Value;


            switch (_currentAction.ActionType)
            {
                case UnitAIActionType.Move:
                    DrawMoveGizmo(
                        assignment
                    );
                    break;


                case UnitAIActionType.BasicAttack:
                case UnitAIActionType.ActiveSkill:
                    DrawAttackGizmo(
                        assignment.Target
                    );
                    break;
            }
        }


        private void DrawMoveGizmo(
            UnitAssignment assignment)
        {
            Vector3 targetPosition =
                assignment.Target.Transform.position;


            Gizmos.color =
                Color.green;


            Gizmos.DrawLine(
                transform.position,
                targetPosition
            );


            Gizmos.DrawWireSphere(
                targetPosition,
                0.15f
            );
        }


        private void DrawAttackGizmo(
            ICombatTarget target)
        {
            if (!IsTargetValid(target))
                return;


            Vector3 targetPosition =
                target.Transform.position;


            Gizmos.color =
                Color.red;


            Gizmos.DrawLine(
                transform.position,
                targetPosition
            );


            Gizmos.DrawWireSphere(
                targetPosition,
                0.15f
            );
        }

#endif
    }
}