using System;
using System.Collections.Generic;
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

    public void Initialize(WaveController waveController, EffectManager effectManager)
    {
        _wave = waveController;
        _effects = effectManager;
        if (_artifacts != null && !_artifacts.IsInitialized)
        {
            _artifacts.Initialize(_wave, _effects);
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
    }

    public void CancelReward()
    {
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
            foreach (ArtifactData artifact in _candidates)
            {
                GUILayout.Label(artifact.Description);
                if (GUILayout.Button($"{artifact.DisplayName} [{artifact.Rarity}] 선택"))
                {
                    if (_artifacts.TryAdd(artifact))
                    {
                        CompleteReward();
                    }
                    else
                    {
                        _result = "획득 실패: 중첩 및 효과 설정 확인";
                    }
                    GUIUtility.ExitGUI();
                }
            }
            if (_candidates.Count == 0)
            {
                GUILayout.Label("획득 가능한 후보가 없습니다.");
                if (GUILayout.Button("보상 확인 / 다음 웨이브"))
                {
                    CompleteReward();
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
