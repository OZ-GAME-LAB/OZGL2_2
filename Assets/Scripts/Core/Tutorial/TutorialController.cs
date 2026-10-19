using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Cameras;
using Game.Core;
using Game.UI.InGame;
using OZGL.KDH;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>첫 원정 안내의 순서와 수명 관리. 기존 화면을 열어 설명하며 구매 명령은 호출하지 않습니다.</summary>
public sealed class TutorialController : MonoBehaviour
{
    public enum Step
    {
        Altar, Traits, Totems, OutDone,
        Core, Slot, Construction, BuildingInfo, CoreUpgrade,
        Inventory, Archive, Speed, Consumables, InDone
    }

    [SerializeField] private TutorialData _data;
    [SerializeField] private TutorialOverlayView _view;
    [Header("OutGame")]
    [SerializeField] private RectTransform _altarTarget;
    [SerializeField] private RectTransform _traitTarget;
    [SerializeField] private RectTransform _totemTarget;
    [Header("InGame")]
    [SerializeField] private InGameUIManager _ui;
    [SerializeField] private InGameCameraController _camera;
    [SerializeField] private BuildingSlot _coreSlot;
    [SerializeField] private BuildingSlot[] _slots;
    [SerializeField] private BuildingUIConnection _buildingUI;
    [SerializeField] private RectTransform _buildingPanel;
    [SerializeField] private RectTransform _upgradeTarget;
    [SerializeField] private RectTransform _inventoryTarget;
    [SerializeField] private RectTransform _archiveTarget;
    [SerializeField] private RectTransform _speedTarget;
    [SerializeField] private RectTransform _consumablesTarget;

    private OutGameSaveCoordinator _save;
    private OutGameUIController _outUI;
    private GameFlowController _flow;
    private BuildingSlot _slot;
    private bool _inGame;
    private bool _running;
    private bool _waiting;
    private bool _navigationEnabled;
    private bool _cameraEnabled;
    private bool _previousInputBlock;
    private float _previousSpeed;
    private GameObject _previousSelection;
    private EventSystem _eventSystem;
    private TutorialData.Entry[] _sequence;
    private int _stepIndex;
    private int _stepVersion;
    private float _acceptInputAt;

    public Step CurrentStep { get; private set; }
    public bool IsRunning => _running;

    public void InitializeOutGame(OutGameSaveCoordinator save, OutGameUIController ui)
    {
        _save = save;
        _outUI = ui;
        _inGame = false;
        _view.Hide();
    }

    public void StartOutGame()
    {
        if (_save == null || !_save.NeedsTutorial(false) || _running) return;
        if (Begin()) Enter();
    }

    public void StartInGame(OutGameSaveCoordinator save, GameFlowController flow)
    {
        if (_running || _waiting) return;
        _save = save;
        _flow = flow;
        _inGame = true;
        _view.Hide();
        if (!save.NeedsTutorial(true)) return;
        _waiting = true;
        _flow.PhaseChanged += OnPhaseChanged;
        if (_flow.CurPhase == GamePhase.Preparation) StartWhenReadyAsync().Forget();
    }

    private void OnPhaseChanged(GamePhase phase)
    {
        if (phase == GamePhase.Preparation && _waiting) StartWhenReadyAsync().Forget();
    }

    private async UniTaskVoid StartWhenReadyAsync()
    {
        // PhaseChanged는 체크포인트 저장보다 먼저 발생합니다. 전환이 끝난 뒤 시작합니다.
        await UniTask.WaitUntil(() => !_waiting || _flow.CanEnterBuildMode(),
            cancellationToken: this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
        if (this == null || !_waiting || !_save.NeedsTutorial(true)) return;
        _waiting = false;
        _flow.PhaseChanged -= OnPhaseChanged;
        _slot = _slots.FirstOrDefault(slot => slot != null && !slot.IsOccupied);
        if (_slot == null)
        {
            Debug.LogWarning("[Tutorial] 빈 건설 슬롯이 없어 안내를 시작하지 않았습니다.", this);
            return;
        }
        if (Begin()) Enter();
    }

    private bool Begin()
    {
        if (_data == null || !_data.TryGetSequence(_inGame, out _sequence)) return false;
        _stepIndex = 0;
        _running = true;
        _eventSystem = EventSystem.current;
        if (_eventSystem != null)
        {
            _navigationEnabled = _eventSystem.sendNavigationEvents;
            _previousSelection = _eventSystem.currentSelectedGameObject;
            _eventSystem.SetSelectedGameObject(null);
            _eventSystem.sendNavigationEvents = false;
        }
        if (!_inGame) return true;
        _previousSpeed = Time.timeScale;
        Time.timeScale = 0;
        _previousInputBlock = _ui.ExternalInputBlocked;
        _ui.ExternalInputBlocked = true;
        _cameraEnabled = _camera.enabled;
        _camera.enabled = false;
        return true;
    }

    // 콜백은 생성된 단계에서만 유효합니다. 더블 클릭과 이전 단계의 지연 이벤트를 무시합니다.
    private Action Guard(Action action)
    {
        int version = _stepVersion;
        return () =>
        {
            if (!_running || version != _stepVersion || Time.unscaledTime < _acceptInputAt) return;
            action();
        };
    }

    private void Enter()
    {
        TutorialData.Entry entry = _sequence[_stepIndex];
        CurrentStep = entry.Type;
        _stepVersion++;
        _acceptInputAt = Time.unscaledTime + .2f;
        RectTransform ui = null;
        Collider2D world = null;
        Action target = null;

        // 각 단계가 필요한 화면을 직접 준비하므로 SO에서 순서를 바꿔도 이전 화면에 의존하지 않습니다.
        if (_inGame) _buildingUI.ClearSelection();
        switch (CurrentStep)
        {
            case Step.Altar:
                _outUI.ShowAltar();
                ui = _altarTarget;
                break;
            case Step.Traits:
                _outUI.ShowTraits();
                ui = _traitTarget;
                break;
            case Step.Totems:
                _outUI.ShowTotems();
                ui = _totemTarget;
                break;
            case Step.OutDone:
                _outUI.ShowTotems();
                break;
            case Step.Core:
                world = _coreSlot.GetComponent<Collider2D>();
                break;
            case Step.Slot:
                world = _slot.GetComponent<Collider2D>();
                target = OpenConstruction;
                break;
            case Step.Construction:
            case Step.BuildingInfo:
                _buildingUI.SelectSlot(_slot);
                ui = _buildingPanel;
                break;
            case Step.CoreUpgrade:
                _buildingUI.SelectSlot(_coreSlot);
                ui = _upgradeTarget;
                break;
            case Step.Inventory: ui = _inventoryTarget; break;
            case Step.Archive: ui = _archiveTarget; break;
            case Step.Speed: ui = _speedTarget; break;
            case Step.Consumables: ui = _consumablesTarget; break;
        }
        string progress = (_inGame ? "원정 안내" : "출발 안내") + $"  {_stepIndex + 1} / {_sequence.Length}";
        _view.Show(entry.Title, entry.Body, progress, target == null ? Guard(Next) : null, Guard(Finish),
            ui, world, target == null ? null : Guard(target), entry.NextLabel);
    }

    private void Next()
    {
        if (_stepIndex == _sequence.Length - 1) Finish();
        else { _stepIndex++; Enter(); }
    }

    private void OpenConstruction()
    {
        // 슬롯 클릭은 실제 건설 화면만 엽니다. 다음 설명은 SO의 목록 순서를 따릅니다.
        _buildingUI.SelectSlot(_slot);
        Next();
    }

    private void Finish()
    {
        if (!_save.TryCompleteTutorial(_inGame, out string error))
        {
            _stepVersion++;
            _view.Show("안내 완료 저장 실패", error + "\n다시 시도하거나 이번 안내를 닫을 수 있습니다. 닫으면 다음 접속 때 다시 안내합니다.",
                "저장 확인", Guard(Finish), Guard(End), nextLabel: "저장 재시도");
            return;
        }
        End();
    }

    private void End()
    {
        if (!_running) return;
        _running = false;
        _stepVersion++;
        if (_inGame && _buildingUI != null) _buildingUI.ClearSelection();
        _view.Hide();
        if (_eventSystem != null)
        {
            _eventSystem.sendNavigationEvents = _navigationEnabled;
            _eventSystem.SetSelectedGameObject(_previousSelection != null && _previousSelection.activeInHierarchy ? _previousSelection : null);
        }
        if (_inGame)
        {
            if (_ui != null) _ui.ExternalInputBlocked = _previousInputBlock;
            if (_camera != null) _camera.enabled = _cameraEnabled;
            Time.timeScale = _previousSpeed;
        }
        else if (_outUI != null) _outUI.ShowTotems();
    }

    private void OnDisable()
    {
        _waiting = false;
        if (_flow != null) _flow.PhaseChanged -= OnPhaseChanged;
        End();
    }
}
