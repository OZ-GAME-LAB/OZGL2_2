using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;

// 아티팩트 후보 생성·보유 상태를 관리하고 공통 창구에 효과를 등록
public class ArtifactManager : MonoBehaviour
{
    public bool IsInitialized => _inventory != null;
    // 현재 웨이브가 분기의 마지막 웨이브인 경우 보스전으로 판단
    public bool IsBossWave => IsInitialized && _waveController != null && _waveController.IsLastWave;
    public IReadOnlyList<ArtifactInstance> Instances =>
        _inventory != null ? _inventory.Instances : new List<ArtifactInstance>();

    // 획득·중첩 완료 후 Instance, 이전 중첩, 현재 중첩을 알림
    public event Action<ArtifactInstance, int, int> StackChanged;

    // 보유 목록 제거 후 제거된 Instance 전달 (효과 해제에 사용)
    public event Action<IReadOnlyList<ArtifactInstance>> Cleared;

    [SerializeField] private ArtifactCatalog _artifactCatalog;
    [SerializeField] private ArtifactRewardTable _rewardTable;

    private ArtifactInventory _inventory;
    private WaveController _waveController;
    private ArtifactCandidateSelector _candidateSelector;
    private EffectManager _effectManager;
    private bool _isAdding;

    // 웨이브와 공통 효과 매니저를 주입받아 새로운 Run의 보유 목록을 준비
    public void Initialize(WaveController waveController, EffectManager effectManager)
    {
        if (IsInitialized)
        {
            Debug.LogWarning("[Artifacts/ArtifactManager] 이미 초기화되어 있습니다.", this);
            return;
        }

        if (_artifactCatalog == null)
        {
            Debug.LogError("[Artifacts/ArtifactManager] ArtifactCatalog 참조가 없습니다. Inspector에서 연결해주세요.", this);
            return;
        }

        if (_rewardTable == null || !_rewardTable.IsValid)
        {
            Debug.LogError("[Artifacts/ArtifactManager] ArtifactRewardTable 연결과 설정을 확인해주세요.", this);
            return;
        }

        if (waveController == null)
        {
            Debug.LogError("[Artifacts/ArtifactManager] WaveController 참조가 없습니다.", this);
            return;
        }

        if (effectManager == null)
        {
            Debug.LogError("[Artifacts/ArtifactManager] EffectManager 참조가 없습니다.", this);
            return;
        }

        _effectManager = effectManager;
        _waveController = waveController;
        _inventory = new ArtifactInventory(_artifactCatalog);
        _candidateSelector = new ArtifactCandidateSelector(_artifactCatalog, _inventory, _rewardTable);
        _inventory.Cleared += HandleCleared;
    }

    // 웨이브 진행 전 호출 : 아티팩트 후보 생성 (true와 빈 목록이면 획득 가능한 후보 없음)
    public bool TryCreateCandidates(out IReadOnlyList<ArtifactData> candidates)
    {
        candidates = new List<ArtifactData>();
        if (!IsInitialized || _waveController == null ||
            _waveController.CurQuarter < 1 || _waveController.CurWave < 1)
        {
            Debug.LogWarning("[Artifacts/ArtifactManager] 초기화 및 웨이브 시작 후 후보를 요청해주세요.", this);
            return false;
        }

        return _candidateSelector.TryCreateCandidates(IsBossWave, out candidates);
    }

    // 웨이브 보상 시 호출 : 아티팩트 선택 및 적용 (UI 미연결 또는 처리 실패 시 false)
    public UniTask<bool> SelectAndApplyAsync(WaveBattleType battleType, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (!IsInitialized || _isAdding)
        {
            Debug.LogWarning("[Artifacts/ArtifactManager] 초기화 및 획득 처리 완료 후 보상을 요청해주세요.", this);
            return UniTask.FromResult(false);
        }

        bool isBossWave = battleType == WaveBattleType.Boss;
        if (!_candidateSelector.TryCreateCandidates(isBossWave, out IReadOnlyList<ArtifactData> candidates))
        {
            return UniTask.FromResult(false);
        }

        // 최대 중첩 등으로 받을 수 있는 후보가 없다면 지급 없이 완료
        if (candidates.Count == 0)
        {
            return UniTask.FromResult(true);
        }

        // TODO: UI 연결 시 async UniTask<bool>로 변경하고 아래 선택 흐름을 구현
        // 선택 대기 중에는 중복 요청을 차단하고 Run 종료 시 대기를 취소
        // ArtifactData selected = await _selectionUI.SelectAsync(candidates, token);
        // token.ThrowIfCancellationRequested();
        // selected가 이번 candidates에 포함되어 있는지 확인
        
        // bool applied = TryAdd(selected);
        // 적용 성공 시 UI를 닫고 true 반환. 실패 시 완료 처리 X
        // 취소 시에도 UI와 선택 대기 상태를 정리

        Debug.LogWarning("[Artifacts/ArtifactManager] 후보 생성 완료. 아티팩트 선택 UI 연결이 필요합니다.", this);
        return UniTask.FromResult(false);
    }

    public bool Contains(ArtifactData artifact)
    {
        return IsInitialized && _inventory.Contains(artifact);
    }

    public bool TryGetById(string id, out ArtifactInstance instance)
    {
        instance = null;
        return IsInitialized && _inventory.TryGetById(id, out instance);
    }

    // 효과 변환 성공 후 획득·중첩과 효과 등록을 완료하고 변경을 알림
    public bool TryAdd(ArtifactData artifact)
    {
        if (!IsInitialized)
        {
            Debug.LogWarning("[Artifacts/ArtifactManager] 초기화 후 아티팩트를 획득할 수 있습니다.", this);
            return false;
        }

        if (_isAdding || _effectManager == null || !_inventory.CanAdd(artifact))
        {
            return false;
            
        }

        int previousStacks = 0;
        if (_inventory.TryGetById(artifact.Id, out ArtifactInstance instance))
        {
            previousStacks = instance.StackCount;
        }
        else
        {
            instance = new ArtifactInstance(artifact);
        }

        int currentStacks = previousStacks + 1;
        if (!_effectManager.TryConvertEffects(instance, artifact.UnitStatEffects,
            artifact.CurrencyEffects, currentStacks, out ConvertedEffects converted))
        {
            return false;
        }

        // 변환 성공 후 보유 상태 변경 (아직 이벤트 발생 X)
        if (!_inventory.TryAdd(instance))
        {
            return false;
        }

        // 이벤트 처리 중 중복 획득·종료 요청을 방지
        _isAdding = true;
        _effectManager.RegisterEffects(instance, converted);
        StackChanged?.Invoke(instance, previousStacks, currentStacks);
        _isAdding = false;
        return true;
    }

    // 정산 완료 후 호출 : 보유 목록 제거 및 Inventory 구독 해제
    public bool TryEndRun()
    {
        if (!IsInitialized || _isAdding)
        {
            return false;
        }

        ArtifactInventory inventory = _inventory;
        _inventory = null;
        _waveController = null;
        _candidateSelector = null;
        // 먼저 구독을 해제하고, 제거된 목록을 직접 전달
        List<ArtifactInstance> removed = new List<ArtifactInstance>(inventory.Instances);
        inventory.Cleared -= HandleCleared;
        inventory.Clear();
        if (removed.Count > 0)
        {
            HandleCleared(removed);
        }

        return true;
    }

    private void HandleCleared(IReadOnlyList<ArtifactInstance> removed)
    {
        if (_effectManager != null)
        {
            foreach (ArtifactInstance instance in removed)
            {
                _effectManager.RemoveEffects(instance);
            }
        }

        Cleared?.Invoke(removed);
    }

    private void OnDestroy()
    {
        TryEndRun();
    }
}
