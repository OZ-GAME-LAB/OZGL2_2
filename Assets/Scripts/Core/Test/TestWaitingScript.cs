using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

// 임시 보상·부착 콘텐츠 완료 대기와 테스트 버튼 입력을 담당한다.
public class TestWaitingScript : MonoBehaviour
{
    private GameFlowController _gameFlowController;
    private WaveController _waveController;
    private bool _toggle;
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _clearButton;
    [SerializeField] private Button _failButton;
    [SerializeField] private Button _chooseButton;
    [SerializeField] private Button _resetButton;
    [SerializeField] private Button _finishButton;
    [SerializeField] private Button _continueButton;
    [SerializeField] private Button _lastWaveButton;
    [SerializeField] private Button _lastQuarterButton;

    private Button[] _phaseButtons;
    private ColorBlock[] _originalColors;
    private bool[] _highlighted;
    private long _nextContentRequestId;
    private UniTaskCompletionSource _contentCompletion;
    private CancellationToken _contentToken;
    public long PostBattleRequestId { get; private set; }
    public PostBattleEventType PendingPostBattleContent { get; private set; }
    public bool IsWaitingForPostBattleContent => _contentCompletion != null &&
        !_contentToken.IsCancellationRequested &&
        _contentCompletion.Task.Status == UniTaskStatus.Pending;

    private void Awake()
    {
        _phaseButtons = new[] { _startButton, _clearButton, _failButton, _chooseButton, _resetButton, _finishButton, _continueButton, _lastWaveButton, _lastQuarterButton };
        _originalColors = new ColorBlock[_phaseButtons.Length];
        _highlighted = new bool[_phaseButtons.Length];
        for (int i = 0; i < _phaseButtons.Length; i++)
        {
            if (_phaseButtons[i] != null)
                _originalColors[i] = _phaseButtons[i].colors;
        }
    }

    // PhaseChanged는 잠금 해제 전에도 발행되므로 프레임 후반의 최신 입력 조건을 확인한다.
    private void LateUpdate()
    {
        bool ready = _gameFlowController != null && _waveController != null;
        bool battle = ready && _gameFlowController.CurPhase == GamePhase.Battle;
        bool choice = ready && !_toggle &&
            (_gameFlowController.CurPhase == GamePhase.Reward ||
             _gameFlowController.IsWaitingForArtifactSelection);

        SetHighlight(0, ready && _waveController.CurrentPreset != null && _gameFlowController.CanEnterBuildMode());
        SetHighlight(1, battle);
        SetHighlight(2, battle);
        SetHighlight(3, choice || (ready && IsWaitingForPostBattleContent &&
            (_gameFlowController.CurPhase == GamePhase.Event ||
             _gameFlowController.CurPhase == GamePhase.Store)));
        SetHighlight(4, ready);
        bool runChoice = ready && _gameFlowController.CanChooseRunDecision;
        SetHighlight(5, runChoice);
        SetHighlight(6, runChoice);
        SetHighlight(7, ready && _waveController.CanJumpToLastWave);
        SetHighlight(8, ready && _waveController.CanJumpToLastQuarter);
    }

    private void SetHighlight(int index, bool available)
    {
        Button button = _phaseButtons[index];
        if (button == null) return;
        available = available && button.IsInteractable();
        if (_highlighted[index] == available) return;

        _highlighted[index] = available;
        ColorBlock colors = _originalColors[index];
        if (available)
        {
            colors.normalColor = Color.yellow;
            colors.highlightedColor = Color.yellow;
            colors.selectedColor = Color.yellow;
            colors.pressedColor = new Color(0.8f, 0.8f, 0f, 1f);
        }
        button.colors = colors;
    }

    private void OnDisable()
    {
        if (_phaseButtons == null) return;
        for (int i = 0; i < _phaseButtons.Length; i++)
        {
            if (_phaseButtons[i] != null)
                _phaseButtons[i].colors = _originalColors[i];
            _highlighted[i] = false;
        }
    }



    public void Initialize(GameFlowController gameFlowController, WaveController waveController)
    {
        _gameFlowController = gameFlowController;
        _waveController = waveController;
    }

    public void GoToLastWave()
    {
        if (_waveController != null)
            _waveController.JumpToLastWaveForTest();
    }

    public void GoToLastQuarter()
    {
        if (_waveController != null)
            _waveController.JumpToLastQuarterForTest();
    }

    public void WaveStartBtn()
    {
        Debug.Log($"[TestWaitingScript] 웨이브 시작 테스트");
        _gameFlowController.TrySpawnUnits().Forget();
    }

    public void ResetBtn()
    {
        Debug.Log($"[TestWaitingScript] 게임 리셋 테스트");
        _gameFlowController.ResetRun();
    }

    public void ChooseResultBtn()
    {
        if (_gameFlowController == null) return;
        if (_gameFlowController.CurPhase == GamePhase.Event ||
            _gameFlowController.CurPhase == GamePhase.Store)
        {
            CompletePostBattleContent(PostBattleRequestId);
            return;
        }
        Debug.Log($"[TestWaitingScript] 보상 선택 테스트");
        if (_gameFlowController.CurPhase != GamePhase.Reward &&
            !_gameFlowController.IsWaitingForArtifactSelection) return;
        _toggle = true;
    }
    public async UniTask WaitToggle(CancellationToken cts)
    {
        _toggle = false;

        await UniTask.WaitUntil(
            () => _toggle,
            cancellationToken: cts);
    }

    // 테스트용 부착 콘텐츠 대기. 각 요청의 완료 대상과 취소 등록은 공유하지 않는다.
    public async UniTask WaitPostBattleContentAsync(Node node, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (node == null) throw new ArgumentNullException(nameof(node));
        if (node.PostBattleEvent == PostBattleEventType.None) return;
        if (IsWaitingForPostBattleContent)
            throw new InvalidOperationException("부착 콘텐츠가 이미 진행 중입니다.");

        long requestId = checked(++_nextContentRequestId);
        var completion = new UniTaskCompletionSource();
        _contentCompletion = completion;
        _contentToken = token;
        PostBattleRequestId = requestId;
        PendingPostBattleContent = node.PostBattleEvent;
        Debug.Log($"[TestWaitingScript] {PendingPostBattleContent} 완료 대기 (요청 {requestId})");
        try
        {
            using (token.Register(() => completion.TrySetCanceled(token)))
                await completion.Task;
            token.ThrowIfCancellationRequested();
        }
        finally
        {
            // 이전 판의 정리가 새 요청을 지우지 않게 한다.
            if (PostBattleRequestId == requestId)
            {
                _contentCompletion = null;
                _contentToken = default;
                PostBattleRequestId = 0;
                PendingPostBattleContent = PostBattleEventType.None;
            }
        }
    }

    public void CompletePostBattleContent(long requestId)
    {
        if (!IsWaitingForPostBattleContent || requestId != PostBattleRequestId) return;
        _contentCompletion.TrySetResult();
    }

    private void OnDestroy()
    {
        _contentCompletion?.TrySetCanceled();
    }
}
