using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;

// 재화 패널이 시작한 보상을 표시하고 선택 완료 시 진행 요청
public class ArtifactTestPanel : MonoBehaviour
{
    public ArtifactManager Artifacts => _artifacts;
    [SerializeField] private ArtifactManager _artifacts;
    [SerializeField] private EffectManager _effects;
    [SerializeField] private WaveController _wave;
    private IReadOnlyList<ArtifactData> _candidates = new List<ArtifactData>();
    private Action _completed;
    private bool _waiting;
    private int _gold;
    private int _gem;
    private string _result = "재화 패널에서 웨이브 보상을 지급하세요.";
    private Vector2 _scroll;
    private CancellationTokenSource _rewardWait;

    public void Initialize(WaveController waveController, EffectManager effectManager, RunCurrencyManager runCurrencyManager)
    {
        _wave = waveController;
        _effects = effectManager;
        if (_artifacts != null && !_artifacts.IsInitialized)
        {
            _artifacts.Initialize(_wave, _effects, runCurrencyManager);
        }
    }

    public void EndRun()
    {
        CancelReward();
        if (_artifacts != null && _artifacts.IsInitialized)
        {
            _artifacts.TryEndRun();
        }
    }

    public bool TryPrepareReward()
    {
        return !_waiting && _artifacts != null &&
            _artifacts.TryCreateCandidates(out _candidates);
    }

    public void ShowReward(int gold, int gem, Action completed)
    {
        _gold = gold;
        _gem = gem;
        _completed = completed;
        _waiting = true;
        _result = $"{_wave.CurQuarter}분기 / {_wave.CurWave}웨이브 보상";
        RunSelectionAsync().Forget();
    }

    public void CancelReward()
    {
        if (_rewardWait != null)
        {
            CancellationTokenSource wait = _rewardWait;
            _rewardWait = null;
            wait.Cancel();
        }
        _waiting = false;
        _completed = null;
        _candidates = new List<ArtifactData>();
        _gold = 0;
        _gem = 0;
        _result = "재화 패널에서 웨이브 보상을 지급하세요.";
    }

    public void DrawPanel(bool runActive)
    {
        GUILayout.Label("아티팩트 / 웨이브 보상");
        _scroll = GUILayout.BeginScrollView(_scroll);
        GUILayout.Label(_result);
        if (runActive && _waiting)
        {
            GUILayout.Label($"지급 완료: 골드 {_gold} / 보석 {_gem}");
            if (_artifacts.IsRewardApplied)
            {
                GUILayout.Label("아티팩트 지급 완료.");
            }
            if (_rewardWait == null && GUILayout.Button("선택 처리 재개"))
            {
                RunSelectionAsync().Forget();
                GUIUtility.ExitGUI();
            }
            foreach (ArtifactData artifact in _artifacts.SelectionCandidates)
            {
                if (_artifacts.IsRewardApplied)
                {
                    break;
                }
                GUILayout.Label(artifact.Description);
                if (GUILayout.Button($"{artifact.DisplayName} [{artifact.Rarity}] 선택"))
                {
                    if (_artifacts.TrySelectReward(artifact))
                    {
                        _result = "지급 완료. 다음 웨이브로 진행합니다.";
                    }
                    else
                    {
                        _result = "획득 실패: 중첩 및 효과 설정 확인";
                    }
                    GUIUtility.ExitGUI();
                }
            }
        }
        if (_artifacts != null && _artifacts.IsInitialized)
        {
            GUILayout.Space(8);
            GUILayout.Label("보유 아티팩트");
            foreach (ArtifactInstance instance in _artifacts.Instances)
            {
                GUILayout.Label($"{instance.Data.DisplayName}: {instance.StackCount}중첩");
            }
        }
        GUILayout.EndScrollView();
    }

    private async UniTask RunSelectionAsync()
    {
        if (_rewardWait != null)
        {
            return;
        }
        CancellationTokenSource wait = new CancellationTokenSource();
        _rewardWait = wait;
        var outcome = await _artifacts.TestSelectAndApplyAsync(_candidates, wait.Token).SuppressCancellationThrow();
        if (_rewardWait == wait)
        {
            _rewardWait = null;
            if (!outcome.IsCanceled && outcome.Result)
            {
                CompleteReward();
            }
            else
            {
                _result = "선택 처리 중단. 초기화 및 다른 대기 상태를 확인한 뒤 재개하세요.";
            }
        }
        wait.Dispose();
    }

    private void OnDisable()
    {
        CancelReward();
    }

    private void CompleteReward()
    {
        Action completed = _completed;
        _waiting = false;
        _completed = null;
        _candidates = new List<ArtifactData>();
        _result = $"보상 선택 완료 (골드 {_gold} / 보석 {_gem})";
        completed?.Invoke();
    }
}
