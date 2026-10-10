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

        [SerializeField]
        private float _targetReevaluationInterval = 0.5f;


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

        private bool _isTargetReevaluationLocked;

        private bool _pendingSkillTargetReevaluation;

        private float _nextTargetReevaluationTime;

        private float _nextAssignmentRetryTime;
        private const float AssignmentRetryInterval = 0.5f;


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
            UnbindEvents();
            _core =
                core;


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

            _pendingSkillTargetReevaluation =
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
            // 같은 대상의 위치 재평가는 이동을 정지/재진입시키지 않는다.
            if (_isRunning && !_currentActionEnded && !_movementCompleted
                && IsCurrentAction(UnitAIActionType.Move) && _currentAssignment.HasValue
                && ReferenceEquals(_currentAssignment.Value.Target, assignment.Target))
            {
                bool destinationChanged = Vector2.Distance(
                    _currentAssignment.Value.PreferredPosition, assignment.PreferredPosition) > 0.01f;
                _currentAssignment = assignment;
                if (destinationChanged && !_core.IsCombatBusy)
                    _core.UpdateMovementDestination(assignment.PreferredPosition);
                EvaluateAction();
                return;
            }

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


            if (_pendingSkillTargetReevaluation)
            {
                _pendingSkillTargetReevaluation =
                    false;

                if (!ValidateCurrentAssignment())
                    return;

                RequestTargetReevaluation();

                return;
            }


            if (_core.IsCombatBusy) return;
            if (_core.TrySelectActiveSkill(CurrentTarget, out _))
            { ChangeAction(UnitAIActionType.ActiveSkill); return; }
            if (!ValidateCurrentAssignment()) return;


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
            if (_core == null || !_isRunning || _core.IsCombatBusy) return;
            if (_isRunning && _core.TrySelectActiveSkill(CurrentTarget, out _))
            { ChangeAction(UnitAIActionType.ActiveSkill); return; }
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
                    assignment, approaching: IsCurrentAction(UnitAIActionType.Move) && !_movementCompleted && !_currentActionEnded
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
            UnitAIActionType actionType, bool skillPrepared = false)
        {
            // 승인 실패는 행동 전환이 아니다. Move.Exit/Stop 전에 실행 준비를 마친다.
            if (actionType == UnitAIActionType.ActiveSkill && !skillPrepared)
            {
                _core.TryActiveSkill(_core.SelectedSkillDecision,
                    () => ChangeAction(UnitAIActionType.ActiveSkill, skillPrepared: true));
                return;
            }

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
            if (!_isRunning)
                return;

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
            if (!_isRunning)
                return;

            if (!IsCurrentAction(UnitAIActionType.Move))
                return;

            if (!_currentAssignment.HasValue)
            {
                RequestFullAssignment();

                return;
            }

            UnitAssignment assignment =
                _currentAssignment.Value;

            if (!IsTargetValid(assignment.Target))
            {
                RequestFullAssignment();

                return;
            }

            UnitAIActionType nextAction =
                _actionSelector.SelectAction(
                    assignment
                );

            // 이동에 실패했더라도 현재 타겟에 대해
            // 공격/스킬 사용 또는 대기가 가능하다면
            // 기존 Assignment를 유지하고 행동을 다시 판단한다.
            if (nextAction != UnitAIActionType.Move)
            {
                _currentActionEnded =
                    true;

                _movementCompleted =
                    false;

                return;
            }

            // 여전히 이동이 필요한 상태라면 현재 위치 배정으로는
            // 전투를 진행할 수 없으므로 전체 Assignment를 다시 요청한다.
            RequestFullAssignment();
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
            {
                if (Time.time >= _nextAssignmentRetryTime)
                    RequestFullAssignment();
                return false;
            }


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
            // 요청은 동기적으로 실패할 수 있다. 다음 Think에서 무제한 재귀/재시도하지 않는다.
            _nextAssignmentRetryTime = Time.time + AssignmentRetryInterval;
            ClearUnitAssignment();

            _core.RequestFullAssignment();
        }


        private void RequestTargetReevaluation()
        {
            _core.RequestTargetReevaluation();
        }


        private void RequestPositionAssignment()
        {
            _movementCompleted =
                false;

            _currentActionEnded =
                false;


            ClearUnitAssignment();
            _nextAssignmentRetryTime = Time.time + AssignmentRetryInterval;
            _core.RequestPositionAssignment();
        }


        public void QueueTargetReevaluation()
        {
            if (_isRunning)
            {
                _pendingSkillTargetReevaluation =
                    true;
            }
        }


        public void NotifyTargetReevaluation()
        {
            if (!_isRunning)
                return;


            if (Time.time <
                _nextTargetReevaluationTime)
            {
                return;
            }


            _nextTargetReevaluationTime =
                Time.time
                + _targetReevaluationInterval;


            RequestTargetReevaluation();
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

            _nextAssignmentRetryTime = 0f;

            _nextThinkTime =
                0f;


            EvaluateAction();
        }


        // ============================================================
        // Stop
        // ============================================================

        public void Stop()
        {
            if (!_isRunning)
                return;


            _pendingSkillTargetReevaluation =
                false;

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
            _pendingSkillTargetReevaluation =
                false;

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


            if (_currentAction.ActionType ==
                UnitAIActionType.Move)
            {
                DrawMoveGizmo(
                    assignment
                );

                return;
            }


            DrawTargetGizmo(
                assignment.Target
            );
        }


        private void DrawMoveGizmo(
            UnitAssignment assignment)
        {
            Vector3 destination =
                assignment.PreferredPosition;


            Gizmos.color =
                Color.green;


            // 현재 위치 -> 이동 목표 좌표
            Gizmos.DrawLine(
                transform.position,
                destination
            );


            Gizmos.DrawWireSphere(
                destination,
                0.15f
            );
        }


        private void DrawTargetGizmo(
            ICombatTarget target)
        {
            if (target == null)
                return;

            if (target.Transform == null)
                return;


            Vector3 targetPosition =
                target.Transform.position;


            Gizmos.color =
                Color.red;


            // 현재 위치 -> 현재 배정된 타겟
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