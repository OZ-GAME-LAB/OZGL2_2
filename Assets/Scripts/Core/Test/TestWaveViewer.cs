using Game.Core;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class TestWaveViewer : MonoBehaviour
{
    [SerializeField] private WaveController _waveController;
    [SerializeField] private GameFlowController _gameFlowController;
    [SerializeField] private bool _showPresetInfo;
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
            if (_showPresetInfo)
            {
                WaveSO preset = _waveController.CurrentPreset;
                if (preset == null)
                {
                    _text.text = "웨이브 프리셋 없음";
                    return;
                }
                string type = preset.BattleType == WaveBattleType.Normal ? "일반" :
                    preset.BattleType == WaveBattleType.Elite ? "정예" : "보스";
                string faction;
                switch (_waveController.CurrentFaction)
                {
                    case EnemyFaction.Irregulars: faction = "비정규군"; break;
                    case EnemyFaction.RegularArmy: faction = "정규군"; break;
                    case EnemyFaction.EliteArmy: faction = "정규군(정예)"; break;
                    default: faction = "성전군"; break;
                }
                _text.text = $"{type} | {faction} | {preset.WaveName}";
                return;
            }
        _text.text = _gameFlowController.CurPhase == GamePhase.Finished
            ? "gameFinished"
            : $"Quarter : {_waveController.CurQuarter} / Wave : {_waveController.CurWave}";
    }
}