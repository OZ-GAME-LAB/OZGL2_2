using System.Collections.Generic;
using Game.Core;
using UnityEngine;

// EconomyTestScene 전용 아티팩트 테스트. 화면 출력은 CurrencyTestPanel에서 호출
public class ArtifactTestPanel : MonoBehaviour
{
    [SerializeField] private ArtifactManager _artifacts;
    [SerializeField] private EffectManager _effects;
    [SerializeField] private WaveController _wave;
    [SerializeField] private ArtifactCatalog _catalog;

    private IReadOnlyList<ArtifactData> _candidates = new List<ArtifactData>();
    private int _quarter;
    private int _waveNumber;
    private bool _showCatalog;
    private string _result = "Run 시작 후 후보를 생성하거나 Catalog에서 직접 획득하세요.";

    public void Initialize()
    {
        if (_artifacts == null || _effects == null || _wave == null || _catalog == null)
        {
            _result = "ArtifactManager, EffectManager, WaveController, Catalog 연결을 확인하세요.";
            return;
        }

        if (!_artifacts.IsInitialized)
        {
            _artifacts.Initialize(_wave, _effects);
            _candidates = new List<ArtifactData>();
        }
    }

    public void EndRun()
    {
        if (_artifacts != null && _artifacts.IsInitialized)
        {
            _artifacts.TryEndRun();
        }
        _candidates = new List<ArtifactData>();
        _result = "아티팩트 보유 목록과 해당 Source의 효과 제거 완료";
    }

    // 기존 재화 테스트 창의 스크롤 영역에서 호출
    public void DrawPanel(bool runActive)
    {
        GUILayout.Label("아티팩트 테스트 — 후보 선택 / 중첩 / 효과 확인");
        if (_artifacts == null || _effects == null || _wave == null || _catalog == null)
        {
            GUILayout.Label("테스트 패널의 매니저 및 Catalog 연결을 확인하세요.");
            return;
        }

        if (!runActive || !_artifacts.IsInitialized)
        {
            GUILayout.Label("위의 Run 시작 버튼을 먼저 누르세요.");
            GUILayout.Label(_result);
            DrawEffects();
            return;
        }

        if (_quarter != _wave.CurQuarter || _waveNumber != _wave.CurWave)
        {
            _candidates = new List<ArtifactData>();
        }

        GUILayout.Label($"현재: {_wave.CurQuarter}분기 / {_wave.CurWave}웨이브 | 보스: {_wave.IsLastWave}");
        GUILayout.Label("재화 탭의 웨이브 보상 버튼으로 진행. 마지막 웨이브에서는 신화 후보 추첨");
        if (GUILayout.Button("현재 웨이브 후보 생성 / 다시 추첨"))
        {
            _quarter = _wave.CurQuarter;
            _waveNumber = _wave.CurWave;
            bool success = _artifacts.TryCreateCandidates(out _candidates);
            _result = !success ? "후보 생성 실패" : $"후보 {_candidates.Count}개 생성 (0개면 획득 가능한 후보 없음)";
        }

        // 성공 시 이번 후보 목록을 닫아 한 번의 선택에서 중복 획득 방지
        for (int i = 0; i < _candidates.Count; i++)
        {
            ArtifactData candidate = _candidates[i];
            if (DrawArtifactButton(candidate, "선택"))
            {
                if (Grant(candidate))
                {
                    _candidates = new List<ArtifactData>();
                    break;
                }
            }
        }

        _showCatalog = GUILayout.Toggle(_showCatalog, "Catalog 직접 획득 (등급 제한 없이 중첩 테스트)");
        if (_showCatalog)
        {
            foreach (ArtifactData artifact in _catalog.Artifacts)
            {
                if (DrawArtifactButton(artifact, "획득"))
                {
                    Grant(artifact);
                }
            }
        }

        if (GUILayout.Button("아티팩트만 초기화 — 재화 잔액 유지"))
        {
            EndRun();
            Initialize();
        }
        GUILayout.Label(_result);
        GUILayout.Space(8);
        GUILayout.Label($"보유 아티팩트: {_artifacts.Instances.Count}종");
        foreach (ArtifactInstance instance in _artifacts.Instances)
        {
            GUILayout.Label($"{instance.Data.DisplayName}: {instance.StackCount}/{instance.Data.MaxStacks}중첩");
        }
        DrawEffects();
        GUILayout.Label("유닛 항목은 변환 결과 확인용. 실제 유닛 스탯 반영은 유닛 초기화 연결 이후 확인");
        GUILayout.Label("재화 탭에서 생산·웨이브·혈석 정산 버튼으로 실제 보정된 지급량 확인");
    }

    private bool DrawArtifactButton(ArtifactData artifact, string action)
    {
        int stacks = 0;
        if (_artifacts.TryGetById(artifact.Id, out ArtifactInstance instance))
        {
            stacks = instance.StackCount;
        }
        bool clicked = GUILayout.Button($"{action}: {artifact.DisplayName} [{artifact.Rarity}] {stacks}/{artifact.MaxStacks}");
        GUILayout.Label(artifact.Description);
        return clicked;
    }

    private bool Grant(ArtifactData artifact)
    {
        bool success = _artifacts.TryAdd(artifact);
        _result = success ? $"{artifact.DisplayName}: 획득 및 효과 등록 완료"
            : $"{artifact.DisplayName}: 획득 실패 (최대 중첩 또는 효과 설정 확인)";
        Debug.Log($"[Artifacts/Test] {_result}", this);
        return success;
    }

    private void DrawEffects()
    {
        GUILayout.Space(8);
        GUILayout.Label($"등록된 효과 — 아군 {_effects.AllyModifiers.Count} / 적군 {_effects.EnemyModifiers.Count} / 재화 {_effects.CurrencyModifiers.Count}");
        foreach (var modifier in _effects.AllyModifiers)
        {
            GUILayout.Label($"아군 | {modifier.ApplyType} / {modifier.TargetClass} / {modifier.TargetUnitType} | {modifier.StatType} {modifier.ModifierType} {modifier.Value:0.###} | {SourceName(modifier.Source)}");
        }
        foreach (var modifier in _effects.EnemyModifiers)
        {
            GUILayout.Label($"적군 | {modifier.ApplyType} / {modifier.TargetClass} / {modifier.TargetUnitType} | {modifier.StatType} {modifier.ModifierType} {modifier.Value:0.###} | {SourceName(modifier.Source)}");
        }
        foreach (CurrencyModifier modifier in _effects.CurrencyModifiers)
        {
            GUILayout.Label($"재화 | {modifier.CurrencyType} / {modifier.RewardType} | {modifier.ModifierType} {modifier.Value:0.###} | {SourceName(modifier.Source)}");
        }
    }

    private string SourceName(object source)
    {
        if (source is ArtifactInstance instance)
        {
            return instance.Data.DisplayName;
        }
        return "기타 Source";
    }
}
