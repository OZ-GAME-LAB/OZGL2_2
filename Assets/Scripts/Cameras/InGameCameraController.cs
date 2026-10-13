using System;
using Game.Core;
using OZGL.KDH;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Cameras
{
    public enum FocusField
    {
        Base,
        Battlefield,
        ZoomIn
    }

    public class InGameCameraController : MonoBehaviour
    {
        // ============================================================
        // Battle Preparation State
        // ============================================================

        private enum BattlePrepareCameraState
        {
            None,

            // 캠프 화면에서 대기
            Waiting,

            // 전장 전체 보기로 이동
            MovingToOverview,

            // 전장 전체 보기 유지
            HoldingOverview,

            // 전투 기본 위치로 복귀
            ReturningToEntry,

            // 준비 연출 완료
            Completed
        }

        // ============================================================
        // Reference
        // ============================================================

        [SerializeField]
        private CinemachineBrain _brain;

        [SerializeField]
        private Camera _worldCamera;

        [SerializeField]
        private CinemachineCamera _baseCamera;

        [SerializeField]
        private CinemachineCamera _battlefieldCamera;

        [SerializeField]
        private CinemachineCamera _focusCamera;

        [SerializeField]
        private GameFlowController _gameFlowController;

        [SerializeField]
        private BuildingBuildController _buildController;

        [SerializeField]
        private Transform _focusTarget;

        // ============================================================
        // Scene Area
        // ============================================================

        [Header("Scene Areas")]

        [Tooltip("건설 단계에서 사용할 Base CameraArea입니다.")]
        [SerializeField]
        private CameraArea _baseArea;

        [Tooltip("전투 및 전투 준비 연출에서 사용할 Battlefield CameraArea입니다.")]
        [SerializeField]
        private CameraArea _battlefieldArea;

        // ============================================================
        // Manual Control
        // ============================================================

        [Header("Battle Manual Control")]

        [Tooltip("전투 단계 카메라 이동 속도입니다.")]
        [SerializeField, Min(0f)]
        private float _moveSpeed = 12f;

        [SerializeField, Min(0f)]
        private float _edgeWidth = 20f;

        [Tooltip("화면 짧은 변에 대한 가장자리 감지 비율입니다.")]
        [SerializeField, Range(0f, 0.25f)]
        private float _edgeScreenRatio = 0.05f;

        [SerializeField]
        private bool _edgeScrollEnabled = true;

        [SerializeField, Min(0.01f)]
        private float _zoomSensitivity = 0.15f;

        [SerializeField, Min(0.01f)]
        private float _zoomResponse = 12f;

        // ============================================================
        // Battle Preparation View
        // ============================================================

        [Header("Battle Preparation View")]

        [Tooltip("BattlePreparing 진입 후 캠프 화면에서 기다리는 시간입니다.")]
        [SerializeField, Min(0f)]
        private float _battlePrepareDelay = 1f;

        [Tooltip("캠프 화면에서 전장 전체 보기까지 이동하는 시간입니다.")]
        [SerializeField, Min(0f)]
        private float _battleOverviewMoveDuration = 2f;

        [Tooltip("전장 전체 화면을 유지하는 시간입니다.")]
        [SerializeField, Min(0f)]
        private float _battleOverviewHoldDuration = 1f;

        [Tooltip("전장 전체 보기에서 실제 전투 기본 위치로 복귀하는 시간입니다.")]
        [SerializeField, Min(0f)]
        private float _battleReturnDuration = 1f;

        [Tooltip("전장 전체 보기에서 허용할 최대 Orthographic Size입니다.")]
        [SerializeField, Min(0.01f)]
        private float _maxOverviewOrthographicSize = 12f;

        // ============================================================
        // Building Focus
        // ============================================================

        [Header("Building Focus")]

        [SerializeField, Min(0.01f)]
        private float _buildingFocusSize = 2f;

        // ============================================================
        // Runtime
        // ============================================================

        private readonly CameraInputReader _inputReader =
            new CameraInputReader();

        private readonly CameraMotionController _motionController =
            new CameraMotionController();

        private CameraAreaState _baseState;
        private CameraAreaState _battleState;

        private FocusField _focusField;

        private GamePhase _phase;

        private BattlePrepareCameraState _prepareCameraState;

        private float _prepareStateElapsed;

        private bool _ready;
        private bool _bound;
        private bool _reportedMissingArea;

        // ============================================================
        // Properties / Events
        // ============================================================

        public FocusField FocusField =>
            _focusField;

        public float MaxOverviewOrthographicSize =>
            Mathf.Max(
                0.01f,
                _maxOverviewOrthographicSize);

        public CameraControlState ControlState
        {
            get
            {
                if (!_ready ||
                    !isActiveAndEnabled)
                {
                    return CameraControlState.Locked;
                }

                if (_phase ==
                    GamePhase.BattlePreparing)
                {
                    return CameraControlState.BattleOverview;
                }

                if (_phase != GamePhase.Preparation &&
                    _phase != GamePhase.Battle)
                {
                    return CameraControlState.Locked;
                }

                if (_focusField ==
                    FocusField.ZoomIn)
                {
                    return CameraControlState.BuildingFocus;
                }

                return _phase ==
                       GamePhase.Preparation
                    ? CameraControlState.BuildControl
                    : CameraControlState.BattleControl;
            }
        }

        public event Action<FocusField> FocusChanged;

        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Awake()
        {
            EnsureReady();
        }

        private void OnEnable()
        {
            if (!EnsureReady())
                return;

            BindEvents();

            ApplyPhase(
                _gameFlowController != null
                    ? _gameFlowController.CurPhase
                    : GamePhase.None);
        }

        private void OnDisable()
        {
            UnbindEvents();

            ResetBattlePreparation();

            RestoreFollow(_baseState);
            RestoreFollow(_battleState);
        }

        public void OnDestroy()
        {
            UnbindEvents();

            RestoreFollow(_baseState);
            RestoreFollow(_battleState);
        }

        private void Update()
        {
            if (!_ready)
                return;

            // ============================================================
            // Battle Preparing
            // ============================================================

            if (_phase == GamePhase.BattlePreparing)
            {
                UpdateBattlePreparation(
                    Time.unscaledDeltaTime);

                return;
            }

            // ============================================================
            // Building Focus
            // ============================================================

            // 건물 선택으로 확대된 상태는 Focus Camera가 담당한다.
            if (_focusField == FocusField.ZoomIn)
            {
                ClampFocusTarget();
                return;
            }

            // ============================================================
            // Preparation
            // ============================================================

            // 건설 단계에서는 Base Camera의 수동 이동 / 줌을 막고,
            // CameraArea 밖이 보이지 않는 최대 View를 유지한다.
            if (_phase == GamePhase.Preparation)
            {
                ApplyBaseOverview();
                return;
            }

            // ============================================================
            // Battle
            // ============================================================

            // 전투 단계에서만 기존 수동 카메라 조작을 허용한다.
            if (!CanControlBattleCamera())
                return;

            UpdateManualControl(
                Time.unscaledDeltaTime);
        }

        // ============================================================
        // Initialize
        // ============================================================

        public void Initialize(
            BuildingBuildController buildController,
            GameFlowController flowController)
        {
            UnbindEvents();

            _buildController =
                buildController;

            _gameFlowController =
                flowController;

            if (!EnsureReady())
                return;

            if (isActiveAndEnabled)
            {
                BindEvents();
            }

            ApplyPhase(
                flowController != null
                    ? flowController.CurPhase
                    : GamePhase.None);
        }

        private bool EnsureReady()
        {
            if (_baseArea == null ||
                _battlefieldArea == null)
            {
                if (!_reportedMissingArea)
                {
                    _reportedMissingArea = true;

                    Debug.LogError(
                        "[InGameCameraController] Base Area와 Battlefield Area를 모두 연결해주세요.",
                        this);
                }

                return false;
            }

            _reportedMissingArea = false;

            if (_baseCamera == null ||
                _battlefieldCamera == null)
            {
                return false;
            }

            if (_ready)
            {
                _baseCamera.Follow = null;
                _battlefieldCamera.Follow = null;

                return true;
            }

            if (_worldCamera == null &&
                _brain != null)
            {
                _worldCamera =
                    _brain.GetComponent<Camera>();
            }

            _baseState =
                CreateArea(
                    _baseCamera,
                    _baseArea);

            _battleState =
                CreateArea(
                    _battlefieldCamera,
                    _battlefieldArea);

            _ready = true;

            SelectArea(
                FocusField.Base);

            return true;
        }

        private CameraAreaState CreateArea(
            CinemachineCamera camera,
            CameraArea definition)
        {
            var state =
                new CameraAreaState(
                    camera,
                    definition);

            // 자유 카메라는 Transform을 직접 제어한다.
            // 기존 Follow는 종료 시 복구한다.
            camera.Follow = null;

            return state;
        }

        private static void RestoreFollow(
            CameraAreaState state)
        {
            if (state == null ||
                state.Camera == null)
            {
                return;
            }

            state.Camera.Follow =
                state.OriginalFollow;
        }

        // ============================================================
        // Event Binding
        // ============================================================

        private void BindEvents()
        {
            if (_bound)
                return;

            if (_buildController != null)
            {
                _buildController.SlotSelected +=
                    FocusAt;

                _buildController.SlotDeselected +=
                    FocusOut;
            }

            if (_gameFlowController != null)
            {
                _gameFlowController.PhaseChanged +=
                    ApplyPhase;
            }

            _bound = true;
        }

        private void UnbindEvents()
        {
            if (!_bound)
                return;

            if (_buildController != null)
            {
                _buildController.SlotSelected -=
                    FocusAt;

                _buildController.SlotDeselected -=
                    FocusOut;
            }

            if (_gameFlowController != null)
            {
                _gameFlowController.PhaseChanged -=
                    ApplyPhase;
            }

            _bound = false;
        }

        // ============================================================
        // Game Phase
        // ============================================================

        private void ApplyPhase(
            GamePhase phase)
        {
            _phase = phase;

            if (!_ready)
                return;

            switch (phase)
            {
                case GamePhase.BattlePreparing:
                    {
                        BeginBattlePreparation();
                        break;
                    }

                case GamePhase.Battle:
                    {
                        FinishBattlePreparationForBattle();
                        break;
                    }

                case GamePhase.Preparation:
                case GamePhase.None:
                    {
                        ResetBattlePreparation();
                        ApplyBaseView();
                        break;
                    }
            }
        }

        // ============================================================
        // Preparation View
        // ============================================================

        /// <summary>
        /// 건설 단계 기본 화면.
        /// Base CameraArea 전체를 표시한다.
        /// </summary>
        private void ApplyBaseView()
        {
            _baseState.Zoom = 1f;

            SelectArea(
                FocusField.Base);

            ApplyBaseOverview();
        }

        /// <summary>
        /// Base CameraArea.Bounds 전체가 화면에 들어오도록 고정한다.
        /// 건설 단계에서는 매 프레임 이 View를 유지한다.
        /// </summary>
        private void ApplyBaseOverview()
        {
            Rect bounds =
                BoundsFor(_baseState);

            // CameraArea 밖이 보이지 않는 최대 크기
            float size =
                MaximumSize(bounds);

            Vector3 position =
                new Vector3(
                    bounds.center.x,
                    bounds.center.y,
                    _baseState.EntryPosition.z);

            _motionController.SetView(
                _baseState,
                position,
                size);
        }

        // ============================================================
        // Battle Preparation
        // ============================================================

        private void BeginBattlePreparation()
        {
            ResetBattlePreparation();

            // 준비 연출은 현재 캠프 전체 화면에서 시작한다.
            ApplyBaseOverview();

            SelectArea(
                FocusField.Base);

            _prepareCameraState =
                BattlePrepareCameraState.Waiting;

            _prepareStateElapsed = 0f;
        }

        private void UpdateBattlePreparation(
            float deltaTime)
        {
            switch (_prepareCameraState)
            {
                case BattlePrepareCameraState.Waiting:
                    {
                        UpdatePrepareWaiting(
                            deltaTime);

                        break;
                    }

                case BattlePrepareCameraState.MovingToOverview:
                    {
                        UpdatePrepareMoveToOverview(
                            deltaTime);

                        break;
                    }

                case BattlePrepareCameraState.HoldingOverview:
                    {
                        UpdatePrepareOverviewHold(
                            deltaTime);

                        break;
                    }

                case BattlePrepareCameraState.ReturningToEntry:
                    {
                        UpdatePrepareReturn(
                            deltaTime);

                        break;
                    }

                case BattlePrepareCameraState.None:
                case BattlePrepareCameraState.Completed:
                    break;
            }
        }

        // ============================================================
        // Battle Preparation - Waiting
        // ============================================================

        private void UpdatePrepareWaiting(
            float deltaTime)
        {
            // 대기 중에도 캠프 전체 화면을 유지한다.
            ApplyBaseOverview();

            _prepareStateElapsed +=
                deltaTime;

            if (_prepareStateElapsed <
                _battlePrepareDelay)
            {
                return;
            }

            BeginMoveToOverview();
        }

        // ============================================================
        // Battle Preparation - Move To Overview
        // ============================================================

        private void BeginMoveToOverview()
        {
            Rect overviewBounds =
                BattlefieldOverviewBounds();

            float overviewSize =
                BattlefieldOverviewSize(
                    overviewBounds);

            Vector3 overviewPosition =
                new Vector3(
                    overviewBounds.center.x,
                    overviewBounds.center.y,
                    _battleState.EntryPosition.z);

            // Battlefield Camera를 현재 실제 화면과 동일하게 맞춘다.
            // Priority 전환 순간의 화면 점프를 최소화한다.
            Vector3 currentPosition =
                GetCurrentViewPosition();

            float currentSize =
                GetCurrentViewSize();

            _motionController.SetView(
                _battleState,
                currentPosition,
                currentSize);

            _battleState.Zoom = 1f;

            // 여기서 Battlefield Camera로 전환한다.
            SelectArea(
                FocusField.Battlefield);

            _motionController.BeginTransition(
                _battleState);

            _prepareCameraState =
                BattlePrepareCameraState.MovingToOverview;

            _prepareStateElapsed = 0f;

            // Duration == 0 대응
            _motionController.UpdateTransition(
                _battleState,
                overviewPosition,
                overviewSize,
                _battleOverviewMoveDuration,
                0f);
        }

        private void UpdatePrepareMoveToOverview(
            float deltaTime)
        {
            Rect overviewBounds =
                BattlefieldOverviewBounds();

            float overviewSize =
                BattlefieldOverviewSize(
                    overviewBounds);

            Vector3 destination =
                new Vector3(
                    overviewBounds.center.x,
                    overviewBounds.center.y,
                    _battleState.EntryPosition.z);

            bool completed =
                _motionController.UpdateTransition(
                    _battleState,
                    destination,
                    overviewSize,
                    _battleOverviewMoveDuration,
                    deltaTime);

            if (!completed)
                return;

            _motionController.SetView(
                _battleState,
                destination,
                overviewSize);

            _prepareCameraState =
                BattlePrepareCameraState.HoldingOverview;

            _prepareStateElapsed = 0f;
        }

        // ============================================================
        // Battle Preparation - Hold Overview
        // ============================================================

        private void UpdatePrepareOverviewHold(
            float deltaTime)
        {
            ApplyBattlefieldOverview();

            _prepareStateElapsed +=
                deltaTime;

            if (_prepareStateElapsed <
                _battleOverviewHoldDuration)
            {
                return;
            }

            BeginReturnToBattleEntry();
        }

        // ============================================================
        // Battle Preparation - Return To Entry
        // ============================================================

        private void BeginReturnToBattleEntry()
        {
            _battleState.Zoom = 1f;

            _motionController.BeginTransition(
                _battleState);

            _prepareCameraState =
                BattlePrepareCameraState.ReturningToEntry;

            _prepareStateElapsed = 0f;
        }

        private void UpdatePrepareReturn(
            float deltaTime)
        {
            Rect bounds =
                BoundsFor(_battleState);

            float size =
                ManualSize(_battleState);

            Vector3 destination =
                ClampPosition(
                    _battleState.EntryPosition,
                    bounds,
                    size);

            bool completed =
                _motionController.UpdateTransition(
                    _battleState,
                    destination,
                    size,
                    _battleReturnDuration,
                    deltaTime);

            if (!completed)
                return;

            _motionController.SetView(
                _battleState,
                destination,
                size);

            _prepareCameraState =
                BattlePrepareCameraState.Completed;

            _prepareStateElapsed = 0f;
        }

        // ============================================================
        // Battle Preparation - Complete / Reset
        // ============================================================

        private void FinishBattlePreparationForBattle()
        {
            // 연출이 정상적으로 끝났다면
            // 현재 Battlefield View를 그대로 사용한다.
            if (_prepareCameraState ==
                BattlePrepareCameraState.Completed)
            {
                _prepareCameraState =
                    BattlePrepareCameraState.None;

                _prepareStateElapsed = 0f;

                SelectArea(
                    FocusField.Battlefield);

                return;
            }

            // GameFlow가 연출 완료 전에 Battle로 넘어온 경우
            // 전투 기본 위치로 즉시 복귀한다.
            ApplyBattleEntryView();

            ResetBattlePreparation();

            SelectArea(
                FocusField.Battlefield);
        }

        private void ResetBattlePreparation()
        {
            _prepareCameraState =
                BattlePrepareCameraState.None;

            _prepareStateElapsed = 0f;
        }

        // ============================================================
        // Battlefield Views
        // ============================================================

        private void ApplyBattlefieldOverview()
        {
            Rect bounds =
                BattlefieldOverviewBounds();

            float size =
                BattlefieldOverviewSize(
                    bounds);

            Vector3 position =
                new Vector3(
                    bounds.center.x,
                    bounds.center.y,
                    _battleState.EntryPosition.z);

            _motionController.SetView(
                _battleState,
                position,
                size);
        }

        private void ApplyBattleEntryView()
        {
            _battleState.Zoom = 1f;

            Rect bounds =
                BoundsFor(_battleState);

            float size =
                ManualSize(_battleState);

            Vector3 position =
                ClampPosition(
                    _battleState.EntryPosition,
                    bounds,
                    size);

            _motionController.SetView(
                _battleState,
                position,
                size);
        }

        // ============================================================
        // Public View API
        // ============================================================

        public void GetFocusTarget()
        {
        }

        /// <summary>
        /// 일반 건설 단계 Toggle.
        ///
        /// BattlePreparing 연출과는 별개의 기능이다.
        /// 기존처럼 Base / Battlefield Camera를 즉시 선택한다.
        /// Cinemachine Brain의 기본 Blend를 그대로 사용한다.
        /// </summary>
        public void Toggle()
        {
            if (!CanBuild())
                return;

            if (_focusField ==
                FocusField.Battlefield)
            {
                ShowBase();
            }
            else
            {
                ShowBattleField();
            }
        }

        public void ShowBase()
        {
            if (!EnsureReady())
                return;

            if (_phase ==
                    GamePhase.BattlePreparing ||
                _phase ==
                    GamePhase.Battle)
            {
                return;
            }

            SelectArea(
                FocusField.Base);
        }

        public void ShowBattleField()
        {
            if (!EnsureReady())
                return;

            if (_phase ==
                    GamePhase.BattlePreparing ||
                _phase ==
                    GamePhase.Battle)
            {
                return;
            }

            SelectArea(
                FocusField.Battlefield);
        }

        private void SelectArea(
            FocusField field)
        {
            _baseCamera.Priority =
                field == FocusField.Base
                    ? 10
                    : 0;

            _battlefieldCamera.Priority =
                field == FocusField.Battlefield
                    ? 10
                    : 0;

            if (_focusCamera != null)
            {
                _focusCamera.Priority = -1;
            }

            SetFocusField(
                field);
        }

        private void SetFocusField(
            FocusField field)
        {
            _focusField = field;

            FocusChanged?.Invoke(
                field);
        }

        // ============================================================
        // Building Focus
        // ============================================================

        public void FocusAt(
            BuildingSlot slot)
        {
            if (slot != null)
            {
                FocusAt(
                    slot.BuildPosition);
            }
        }

        public void FocusAt(
            Vector3 worldPosition)
        {
            if (!CanBuild() ||
                _focusField ==
                    FocusField.Battlefield ||
                _focusCamera == null ||
                _focusTarget == null)
            {
                return;
            }

            _focusCamera.Lens.OrthographicSize =
                Mathf.Min(
                    _buildingFocusSize,
                    MaximumSize(
                        BoundsFor(
                            _baseState)));

            _focusTarget.position =
                ClampPosition(
                    new Vector3(
                        worldPosition.x,
                        worldPosition.y,
                        0f),
                    BoundsFor(
                        _baseState),
                    _focusCamera.Lens.OrthographicSize);

            _focusCamera.Priority = 20;

            SetFocusField(
                FocusField.ZoomIn);
        }

        public void FocusOut()
        {
            if (_focusField !=
                FocusField.ZoomIn)
            {
                return;
            }

            SelectArea(
                FocusField.Base);

            // 건설 단계 기본 View를 즉시 복구한다.
            if (_phase ==
                GamePhase.Preparation)
            {
                ApplyBaseOverview();
            }
        }

        private void ClampFocusTarget()
        {
            if (_focusCamera == null ||
                _focusTarget == null)
            {
                return;
            }

            Rect bounds =
                BoundsFor(
                    _baseState);

            _focusCamera.Lens.OrthographicSize =
                Mathf.Min(
                    _buildingFocusSize,
                    MaximumSize(
                        bounds));

            _focusTarget.position =
                ClampPosition(
                    _focusTarget.position,
                    bounds,
                    _focusCamera.Lens.OrthographicSize);
        }

        // ============================================================
        // Control Check
        // ============================================================

        private bool CanBuild()
        {
            return
                _ready &&
                _gameFlowController != null &&
                _gameFlowController.CanEnterBuildMode();
        }

        /// <summary>
        /// 수동 카메라 이동은 Battle에서만 허용한다.
        /// Preparation에서는 Base 전체 화면을 고정한다.
        /// </summary>
        private bool CanControlBattleCamera()
        {
            return
                Application.isFocused &&
                Time.timeScale > 0f &&
                _phase == GamePhase.Battle &&
                _focusField == FocusField.Battlefield &&
                (_brain == null ||
                 !_brain.IsBlending);
        }

        // ============================================================
        // Manual Battle Camera
        // ============================================================

        private void UpdateManualControl(
            float deltaTime)
        {
            _inputReader.Read(
                _worldCamera,
                _edgeScrollEnabled,
                _edgeWidth,
                _edgeScreenRatio,
                out Vector2 direction,
                out float zoom);

            _motionController.Move(
                _battleState,
                BoundsFor(
                    _battleState),
                Aspect,
                direction,
                zoom,
                _moveSpeed,
                _zoomSensitivity,
                _zoomResponse,
                deltaTime);
        }

        // ============================================================
        // Bounds
        // ============================================================

        private float Aspect =>
            Mathf.Max(
                0.01f,
                _worldCamera != null
                    ? _worldCamera.aspect
                    : (float)Mathf.Max(
                          1,
                          Screen.width)
                      /
                      Mathf.Max(
                          1,
                          Screen.height));

        private Rect BoundsFor(
            CameraAreaState area)
        {
            return area.Definition.Bounds;
        }

        private Rect BattlefieldOverviewBounds()
        {
            return _battlefieldArea.OverviewBounds;
        }

        // ============================================================
        // Size
        // ============================================================

        private float MaximumSize(
            Rect bounds)
        {
            return CameraBoundsUtility.MaximumSize(
                bounds,
                Aspect);
        }

        private float ManualSize(
            CameraAreaState area)
        {
            return CameraBoundsUtility.ManualSize(
                area,
                BoundsFor(area),
                Aspect);
        }

        private float BattlefieldOverviewSize(
            Rect bounds)
        {
            float calculatedSize =
                CameraBoundsUtility.OverviewSize(
                    bounds,
                    Aspect,
                    _battleState.Definition.OverviewPadding);

            return Mathf.Min(
                calculatedSize,
                MaxOverviewOrthographicSize);
        }

        // ============================================================
        // Position
        // ============================================================

        private Vector3 ClampPosition(
            Vector3 position,
            Rect bounds,
            float size)
        {
            return CameraBoundsUtility.ClampPosition(
                position,
                bounds,
                size,
                Aspect);
        }

        // ============================================================
        // Current View
        // ============================================================

        /// <summary>
        /// 실제 출력 카메라의 현재 위치를 가져온다.
        ///
        /// 준비 연출 시작 시 Battlefield Camera를 현재 화면과
        /// 동일한 View에서 출발시키기 위해 사용한다.
        /// </summary>
        private Vector3 GetCurrentViewPosition()
        {
            if (_worldCamera != null)
            {
                return _worldCamera.transform.position;
            }

            return _baseCamera.transform.position;
        }

        private float GetCurrentViewSize()
        {
            if (_worldCamera != null &&
                _worldCamera.orthographic)
            {
                return _worldCamera.orthographicSize;
            }

            return _baseCamera.Lens.OrthographicSize;
        }
    }
}