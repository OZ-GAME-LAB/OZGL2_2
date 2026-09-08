using Game.Core;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class TestWaveViewer : MonoBehaviour
{
    [SerializeField] private WaveController _waveController;
    [SerializeField] private GameFlowController _gameFlowController;
    private TextMeshProUGUI _text;

    private void Awake()
    {
        _text = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        if (_waveController == null || _gameFlowController == null)
        {
            _text.text = "Wave references missing";
            Debug.LogWarning("[TestWaveViewer] 컨트롤러 참조가 없습니다.", this);
            return;
        }
        _waveController.WaveChanged += UpdateWave;
        _gameFlowController.PhaseChanged += UpdatePhase;
        Refresh();
    }

    private void OnDisable()
    {
        if (_waveController != null)
            _waveController.WaveChanged -= UpdateWave;
        if (_gameFlowController != null)
            _gameFlowController.PhaseChanged -= UpdatePhase;
    }

    private void UpdateWave(int wave) => Refresh();
    private void UpdatePhase(GamePhase phase) => Refresh();

    private void Refresh()
    {
        _text.text = _gameFlowController.CurPhase == GamePhase.Finished
            ? "gameFinished"
            : $"Current Wave : {_waveController.CurWave}";
    }
}