using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

//실제 연결전 보상대기 및 전투준비연출 -> 전투로 이어지는 대기 구현
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
        SetHighlight(3, choice);
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
        _gameFlowController.TryStartWave().Forget();
    }

    public void ResultBtn()
    {
        Debug.Log($"[TestWaitingScript] 전투 종료 테스트");
        _waveController.SetSuccess();
    }

    public void ResetBtn()
    {
        Debug.Log($"[TestWaitingScript] 게임 리셋 테스트");
        _gameFlowController.ResetRun();
    }

    public void ChooseResultBtn()
    {
        Debug.Log($"[TestWaitingScript] 보상 선택 테스트");
        if (_gameFlowController.CurPhase != GamePhase.Reward &&
            !_gameFlowController.IsWaitingForArtifactSelection) return;
        _toggle = true;
    }
    public async UniTask WaitForSeconds(float time, CancellationToken cts)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(time), cancellationToken: cts);
    }

    public async UniTask WaitToggle(CancellationToken cts)
    {
        _toggle = false;

        await UniTask.WaitUntil(
            () => _toggle,
            cancellationToken: cts);
    }
}
