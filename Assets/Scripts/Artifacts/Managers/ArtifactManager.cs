using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.UI;
using UnityEngine;

// 아티팩트 후보 생성·보유 상태를 관리하고 공통 창구에 효과를 등록
public class ArtifactManager : MonoBehaviour, IArtifactFlow, IArtifactReader, IArtifactInventory, ISaveDataProvider<ArtifactSaveData>
{
     // 보유 상태 변경 전에 준비한 중첩 수량과 효과
    private struct PreparedChange
    {
        public ArtifactInstance Instance;
        public int PreviousStacks;
        public int CurrentStacks;
        public ConvertedEffects Effects;
    }

    public bool IsInitialized => _inventory != null;
    // 현재 웨이브가 분기의 마지막 웨이브인 경우 보스전으로 판단
    public bool IsBossWave => IsInitialized && _waveController != null && _waveController.IsLastWave;
    public IReadOnlyList<ArtifactInstance> Instances =>
        _inventory != null ? _inventory.Instances : new List<ArtifactInstance>();

    public ArtifactCatalog ArtifactCatalog => _artifactCatalog;

    // 획득·차감 완료 후 Instance, 이전 중첩, 현재 중첩을 알림 (0이면 제거)
    public event Action<ArtifactInstance, int, int> StackChanged;

    // 보유 목록 제거 후 제거된 Instance 전달 (효과 해제에 사용)
    public event Action<IReadOnlyList<ArtifactInstance>> Cleared;

    [SerializeField] private ArtifactCatalog _artifactCatalog;
    [SerializeField] private ArtifactRewardTable _rewardTable;
    
    [Header("아티팩트 획득 테스트")]
    [Tooltip("플레이 중 F5로 카탈로그 획득 창을 엽니다. 일반 플레이에서는 꺼두세요.")]
    [SerializeField] private bool _enableCatalogTest = true;
    private bool _catalogTestOpen;
    private Vector2 _catalogTestScroll;
    private string _catalogTestSearch = "";
    private string _catalogTestMessage = "목록에서 획득 버튼을 누르세요.";
    private GUIStyle _catalogTestDescriptionStyle;
    private RunCurrencyManager _runCurrencyManager;

    private ArtifactInventory _inventory;
    private WaveController _waveController;
    private ArtifactCandidateSelector _candidateSelector;
    private EffectManager _effectManager;
    private IArtifactSelectionUI _selectionUI; // 10.2(문규성) : UI와 실제 연결시도용으로 추가했습니다. Initialize에도 인자를 추가해서 받을 수 있도록 수정했습니다. 
    private bool _isChanging;
    private List<ArtifactData> _testCandidates;
    private CancellationTokenSource _selectionWait;
    private List<ArtifactData> _selectionCandidates = new List<ArtifactData>();
    private bool _rewardApplied;
    private bool _hasRewardCandidates;
    private int _rewardQuarter;
    private int _rewardWave;
    public bool IsSelectingReward => _selectionWait != null;
    public bool IsRewardApplied => _rewardApplied;
    public IReadOnlyList<ArtifactData> SelectionCandidates => _selectionCandidates;

    // 별도 테스트 컴포넌트나 Canvas 없이 사용하는 카탈로그 획득 창입니다.
    private void Update()
    {
        if (!_enableCatalogTest)
        {
            _catalogTestOpen = false;
            return;
        }
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        bool toggle = keyboard != null && keyboard.f5Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        bool toggle = Input.GetKeyDown(KeyCode.F5);
#else
        bool toggle = false;
#endif
        if (toggle) _catalogTestOpen = !_catalogTestOpen;
    }

    private void OnGUI()
    {
        if (!_enableCatalogTest || !_catalogTestOpen) return;

        if (_catalogTestDescriptionStyle == null)
            _catalogTestDescriptionStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };

        float width = Mathf.Min(720f, Screen.width - 20f);
        float height = Mathf.Min(720f, Screen.height - 20f);
        GUI.Window(GetInstanceID(), new Rect((Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f, width, height), DrawCatalogTestWindow,
            "아티팩트 획득 테스트 — F5로 열기 / 닫기");
    }

    private void DrawCatalogTestWindow(int windowId)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("이름 / ID 검색", GUILayout.Width(100f));
        string search = GUILayout.TextField(_catalogTestSearch);
        if (search != _catalogTestSearch)
        {
            _catalogTestSearch = search;
            _catalogTestScroll = Vector2.zero;
        }
        if (GUILayout.Button("닫기", GUILayout.Width(60f))) _catalogTestOpen = false;
        GUILayout.EndHorizontal();

        GUILayout.Label(IsInitialized ? _catalogTestMessage : "초기화 대기 중입니다. 목록은 볼 수 있지만 획득은 초기화 후 가능합니다.",
            _catalogTestDescriptionStyle);
        if (_artifactCatalog == null)
        {
            GUILayout.Label("Artifact Catalog가 연결되어 있지 않습니다.");
            return;
        }

        _catalogTestScroll = GUILayout.BeginScrollView(_catalogTestScroll);
        int shown = 0;
        string filter = _catalogTestSearch.Trim();
        foreach (var artifact in _artifactCatalog.Artifacts)
        {
            if (artifact == null) continue;
            if (filter.Length > 0 &&
                (artifact.Id ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                (artifact.DisplayName ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            shown++;
            int stacks = TryGetById(artifact.Id, out var instance) ? instance.StackCount : 0;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"{artifact.Id} · {artifact.DisplayName} · {artifact.Rarity} · {stacks}/{artifact.MaxStacks}");
            GUILayout.Label(artifact.Description ?? "", _catalogTestDescriptionStyle);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && IsInitialized && !_isChanging && stacks < artifact.MaxStacks;
            if (GUILayout.Button(stacks >= artifact.MaxStacks ? "최대 중첩" : "획득", GUILayout.Height(28f)))
            {
                _catalogTestMessage = TryAdd(artifact)
                    ? $"{artifact.DisplayName} 획득 완료"
                    : $"{artifact.DisplayName} 획득 실패 — Console을 확인하세요.";
            }
            GUI.enabled = enabled;
            GUILayout.EndVertical();
        }
        if (shown == 0) GUILayout.Label("표시할 아티팩트가 없습니다.");
        GUILayout.EndScrollView();
    }

   
    // 10.2(문규성) : 인자 추가중 오류가 많이 발생해서 일단 null 허용으로 만들어놓았습니다.
    // 나중에 테스트시 문제가 생길 소지가 있어서 = null지우고 ArtifactRewardPresenter상속받거나 테스트용 인터페이스 구현체 만들어서 연결이 필요합니다. 
    // 웨이브와 공통 효과 매니저를 주입받아 새로운 Run의 보유 목록을 준비
    public void Initialize(WaveController waveController, EffectManager effectManager,
        RunCurrencyManager runCurrencyManager, IArtifactSelectionUI artifactSelectionUI = null)
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
        
        if (runCurrencyManager == null)
        {
            Debug.LogError("[Artifacts/ArtifactManager] RunCurrencyManager 참조가 없습니다.", this);
            return;
        }

        if (artifactSelectionUI == null)
        {
            Debug.LogError("[Artifacts/ArtifactManager] ArtifactSelectionUI 참조가 없습니다.", this);
            return;
        }
        _effectManager = effectManager;
        _runCurrencyManager = runCurrencyManager;
        _selectionUI = artifactSelectionUI;
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

        PrepareRewardPosition();
        if (_hasRewardCandidates || _rewardApplied)
        {
            candidates = _selectionCandidates.ToArray();
            return true;
        }
        if (!_candidateSelector.TryCreateCandidates(IsBossWave, out candidates)) return false;
        _selectionCandidates = new List<ArtifactData>(candidates);
        _hasRewardCandidates = true;
        return true;
    }

    // 후보 생성 → UI 선택 및 지급 대기 → 완료. UI는 TrySelectReward로 선택 전달
    public async UniTask<bool> SelectAndApplyAsync(WaveBattleType battleType, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!IsInitialized || _isChanging || IsSelectingReward)
        {
            return false;
        }

        // Bootstrap에서는 재화 초기화 전에 참조를 주입하므로 실제 사용 시 준비 상태를 확인합니다.
        if (_runCurrencyManager == null || !_runCurrencyManager.IsInitialized)
        {
            Debug.LogError("[Artifacts/ArtifactManager] 초기화된 RunCurrencyManager가 필요합니다.", this);
            return false;
        }

        PrepareRewardPosition();
        IReadOnlyList<ArtifactData> candidates = _testCandidates ?? (_hasRewardCandidates ? _selectionCandidates : null);
        _testCandidates = null;
        if (!_rewardApplied && candidates == null &&
            !_candidateSelector.TryCreateCandidates(battleType == WaveBattleType.Boss, out candidates))
        {
            return false;
        }
        // 지급 직후 취소된 요청은 재지급 없이 완료
        if (_rewardApplied)
        {
            return TryCompleteWaveCurrencyReward();
        }
        if (candidates.Count == 0)
        {
            _hasRewardCandidates = true;
            _rewardApplied = true;
            return TryCompleteWaveCurrencyReward();
        }

        _selectionCandidates = new List<ArtifactData>(candidates);
        _hasRewardCandidates = true;
        CancellationTokenSource wait = CancellationTokenSource.CreateLinkedTokenSource(token);
        CancellationToken waitToken = wait.Token;
        _selectionWait = wait;
        // TODO: SelectionCandidates를 전달하여 아티팩트 선택 UI 열기
        //10.02(문규성) : 실제 UI와 연결용으로 추가한 스크립트입니다. 
        try
        {
            string message = null;

            while (!_rewardApplied)
            {
                ArtifactData result = await _selectionUI.SelectAsync(
                    _selectionCandidates, waitToken, false, message);

                waitToken.ThrowIfCancellationRequested();

                if (TrySelectReward(result))
                    break;

                message = "유물을 획득하지 못했습니다. 다시 선택해주세요.";
            }

            waitToken.ThrowIfCancellationRequested();
            // SelectAsync의 UI 닫기와 아티팩트 획득 성공 이후에 재화를 지급합니다.
            return TryCompleteWaveCurrencyReward();
        }
        //SelectAsync에서 취소나 예외 발생시에 _selectionWait를 정리해 다음 요청을 막지 않도록 하기 위한 조치입니다. 
        finally
        {
            if (_selectionWait == wait)
            {
                _selectionWait = null;

                if (_rewardApplied)
                    _selectionCandidates.Clear();
            }
            wait.Dispose();
        }
        
        /*
         bool canceled = await UniTask.WaitUntil(() => _rewardApplied,
            cancellationToken: waitToken).SuppressCancellationThrow();

        if (_selectionWait == wait)
        {
            _selectionWait = null;
            if (_rewardApplied) _selectionCandidates.Clear();
            // TODO: 선택 UI 닫기. 취소된 경우에도 정리
        }
        wait.Dispose();
        waitToken.ThrowIfCancellationRequested();
        return !canceled;
        */
    }

    private bool TryCompleteWaveCurrencyReward()
    {
        // 이전 저장에서 이미 지급했거나, 지급 후 재진입한 경우는 성공으로 종료합니다.
        if (_runCurrencyManager.TryGetAppliedWaveReward(
            _waveController.CurQuarter, _waveController.CurWave, out _, out _)) return true;
        if (_runCurrencyManager.TryApplyWaveReward()) return true;
        Debug.LogError("[Artifacts/ArtifactManager] 아티팩트 선택 후 웨이브 재화 지급에 실패했습니다.", this);
        return false;
    }

    // 테스트 진입점만 별도 제공. 준비된 후보를 전달한 뒤 실제 게임과 같은 함수 실행
    public UniTask<bool> TestSelectAndApplyAsync(IReadOnlyList<ArtifactData> candidates, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!IsInitialized || _isChanging || IsSelectingReward || candidates == null)
        {
            return UniTask.FromResult(false);
        }
        _testCandidates = new List<ArtifactData>(candidates);
        _rewardApplied = false;
        _hasRewardCandidates = false;
        return SelectAndApplyAsync(default, token);
    }

    // 이번 후보 중 하나만 지급. 완료가 반환되기 전 중복 선택 차단
    public bool TrySelectReward(ArtifactData selected)
    {
        if (!IsSelectingReward || _selectionWait.IsCancellationRequested || _rewardApplied ||
            !_selectionCandidates.Contains(selected))
        {
            return false;
        }
        _rewardApplied = true;
        if (!TryAdd(selected))
        {
            _rewardApplied = false;
            return false;
        }
        return true;
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

        if (_isChanging || _effectManager == null)
        {
            return false;
        }

        if (!TryPrepareAdd(artifact, out PreparedChange change) || !_inventory.TryAdd(change.Instance))
        {
            return false;
        }

        // 이벤트 처리 중 중복 획득·차감·종료 요청을 방지
        _isChanging = true;
        _effectManager.RegisterEffects(change.Instance, change.Effects);
        StackChanged?.Invoke(change.Instance, change.PreviousStacks, change.CurrentStacks);
        _isChanging = false;
        return true;
    }

    // 아티팩트 중첩 1개 차감. 효과 변환 실패 시 기존 보유 상태 유지
    public bool TryRemove(ArtifactData artifact)
    {
        if (!IsInitialized || _isChanging || _effectManager == null)
        {
            return false;
        }

        if (!TryPrepareRemove(artifact, out PreparedChange change) || !_inventory.TryRemove(change.Instance))
        {
            return false;
        }

        _isChanging = true;
        if (change.CurrentStacks == 0)
        {
            _effectManager.RemoveEffects(change.Instance);
        }
        else
        {
            _effectManager.RegisterEffects(change.Instance, change.Effects);
        }

        StackChanged?.Invoke(change.Instance, change.PreviousStacks, change.CurrentStacks);
        _isChanging = false;
        return true;
    }

    // UI에 표시할 동일 등급의 보유 목록. 조회만 수행하며 동일 품목은 제외
    public List<ArtifactInstance> GetExchangeCandidates(ArtifactData rewardArtifact)
    {
        List<ArtifactInstance> candidates = new List<ArtifactInstance>();
        if (!IsInitialized || _isChanging || !_inventory.CanAdd(rewardArtifact))
        {
            return candidates;
        }

        foreach (ArtifactInstance instance in _inventory.Instances)
        {
            if (instance.StackCount > 0 && instance.Data != rewardArtifact &&
                instance.Data.Rarity == rewardArtifact.Rarity)
            {
                candidates.Add(instance);
            }
        }

        return candidates;
    }

    // 선택한 보유 아티팩트 1개를 같은 등급의 아티팩트 1개로 교환
    public bool TryExchange(ArtifactData ownedArtifact, ArtifactData rewardArtifact)
    {
        if (!IsInitialized || _isChanging || _effectManager == null ||
            ownedArtifact == null || rewardArtifact == null || ownedArtifact == rewardArtifact ||
            ownedArtifact.Rarity != rewardArtifact.Rarity)
        {
            return false;
        }

        // 양쪽 준비가 모두 성공해야 실제 보유 상태 변경
        if (!TryPrepareRemove(ownedArtifact, out PreparedChange remove) ||
            !TryPrepareAdd(rewardArtifact, out PreparedChange add))
        {
            return false;
        }

        if (!_inventory.TryExchange(remove.Instance, add.Instance))
        {
            return false;
        }

        // 보유 상태와 효과가 모두 갱신된 후 알림. 이벤트 중 추가 변경 요청 차단
        _isChanging = true;
        _effectManager.RegisterExchangeEffects(remove.Instance, remove.Effects, add.Instance, add.Effects);
        StackChanged?.Invoke(remove.Instance, remove.PreviousStacks, remove.CurrentStacks);
        StackChanged?.Invoke(add.Instance, add.PreviousStacks, add.CurrentStacks);
        _isChanging = false;
        return true;
    }

    // costas 아티팩트를 소모하고 reward아티팩트를 인벤토리에 추가한다. 
    // 특정 아티팩트를 바치고 다른 아티팩트를 획득하는 이벤트 구현을 위해 추가한 메서드
    public bool TryApplyTrade(IReadOnlyList<ArtifactData> costArtifacts, ArtifactData rewardArtifact = null)
    {
        if (!IsInitialized || _isChanging || _effectManager == null)
        {
            return false;
        }

        var costCounts = new Dictionary<ArtifactData, int>();
        if (costArtifacts != null)
        {
            foreach (ArtifactData artifact in costArtifacts)
            {
                if (!_artifactCatalog.Contains(artifact)) return false;
                costCounts.TryGetValue(artifact, out int count);
                costCounts[artifact] = count + 1;
            }
        }

        if (rewardArtifact != null)
        {
            if (!_artifactCatalog.Contains(rewardArtifact))
            {
                return false;
            }
            if (!costCounts.ContainsKey(rewardArtifact))
            {
                costCounts.Add(rewardArtifact, 0);
            }
        }
        
        var changes = new List<PreparedChange>(); //인벤토리 변경과 이벤트 발행에 필요한 객체 + 변경 전 갯수 + 변경 후 갯수 기록 
        var removedSources = new List<object>(); //지울 효과들의 목록
        var finalEffects = new Dictionary<object, ConvertedEffects>(); //교환 완료 후 적용할 효과
        //해당 foreach문에서는 소모 & 추가되는 아티팩트의 최종 수량을 검증하고 적용할 효과를 준비합니다
        foreach (var cost in costCounts)
        {
            ArtifactData costArtifact = cost.Key; //처리할 유물의 정보
            
            _inventory.TryGetById(costArtifact.Id, out ArtifactInstance inventoryArtifact); //삭제할 유물이 실제로 인벤토리에 있는지 확인
            int previousStacks = inventoryArtifact == null ? 0 : inventoryArtifact.StackCount; //삭제할 유물이 인벤토리에 있으면 기존 보유 수량 기록
            // 같은 유물을 보상으로 받더라도 소모할 아티팩트는 교환 전에 보유해야 함
            // (A아티팩트 2개를 소모하고 A 아티팩트를 다시 1개 받는다고 해도 기존 인벤토리에 A아티팩트 2개가 있어야 교환가능)
            if (previousStacks < cost.Value || (inventoryArtifact != null && inventoryArtifact.Data != costArtifact)) 
                return false;
            
            //소모되는 아티팩트가 보상 아티팩트라면 스택 + 1 (416번째 줄에서 추가한 아티팩트)
            long finalStacks = (long)previousStacks - cost.Value + (costArtifact == rewardArtifact ? 1 : 0);
            if (finalStacks > costArtifact.MaxStacks) 
                return false;
            //최종 수량 변경이 없다면 효과갱신이 필요 없음
            int currentStacks = (int)finalStacks;
            if (currentStacks == previousStacks) 
                continue;

            //인벤토리에 아이템이 없었다면 신규획득이기 때문에 새 인스턴스를 만들어줌
            if (inventoryArtifact == null) 
                inventoryArtifact = new ArtifactInstance(costArtifact);
            ConvertedEffects effects = null;
            if (currentStacks > 0 && !_effectManager.TryConvertEffects(inventoryArtifact, new EffectDataGroup
                {
                    StatEffects = costArtifact.UnitStatEffects,
                    CurrencyEffects = costArtifact.CurrencyEffects,
                    SlotEffects = costArtifact.ConsumableSlotEffects,
                    PassiveEffects = costArtifact.PassiveSkillEffects
                }, currentStacks, out effects))
            {
                return false;
            }
            //해당 유물의 변경정보(객체 정보, 교환 전 갯수, 교환 후 갯수)를 기록
            changes.Add(new PreparedChange
            {
                Instance = inventoryArtifact,
                PreviousStacks = previousStacks,
                CurrentStacks = currentStacks,
            });
            //기존 보유 유물의 변경 전 효과를 제거대상에 추가(교환으로 개수가 감소한 아티팩트)
            if (previousStacks > 0) 
                removedSources.Add(inventoryArtifact);
            //교환 이후에도 보유하고 있다면 해당 효과를 적용 대상으로 기록(교환을 하고도 남은 아티팩트 + 교환의 대가로 받은 아티팩트)
            if (currentStacks > 0) 
                finalEffects.Add(inventoryArtifact, effects);
        }
        if (changes.Count == 0) return true;

        _isChanging = true;
        try
        {
            // 앞서 검증 및 준비한 수량 변경을 인벤토리에 적용한다. 지금은 변경 알림을 보내지 않는다
            foreach (PreparedChange change in changes)
            {
                if (change.CurrentStacks > change.PreviousStacks)
                {
                    _inventory.TryAdd(change.Instance);
                }
                else
                {
                    //현재는 아티팩트의 장비 중첩을 여러번 깎는 메서드가 없어서 for문 사용
                    for (int i = change.CurrentStacks; i < change.PreviousStacks; i++)
                        _inventory.TryRemove(change.Instance);
                }
            }
            //변경 대상 유물의 효과를 최종 수량에 맞게 교체
            _effectManager.ReplaceSourceEffects(removedSources, finalEffects);
            foreach (PreparedChange change in changes)
                StackChanged?.Invoke(change.Instance, change.PreviousStacks, change.CurrentStacks);
            return true;
        }
        finally { _isChanging = false; }
    }

    // 획득 가능 여부와 증가 후 효과만 준비. 보유 상태 변경 X
    private bool TryPrepareAdd(ArtifactData artifact, out PreparedChange change)
    {
        change = default;
        if (!_inventory.CanAdd(artifact))
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
        if (!_effectManager.TryConvertEffects(instance, new EffectDataGroup
            {
                StatEffects = artifact.UnitStatEffects,
                CurrencyEffects = artifact.CurrencyEffects,
                SlotEffects = artifact.ConsumableSlotEffects,
                PassiveEffects = artifact.PassiveSkillEffects
            }, currentStacks, out ConvertedEffects effects))
        {
            return false;
        }

        change = new PreparedChange
        {
            Instance = instance,
            PreviousStacks = previousStacks,
            CurrentStacks = currentStacks,
            Effects = effects
        };
        return true;
    }

    // 보유 여부와 감소 후 효과만 준비. 마지막 중첩은 효과 제거를 위해 null 유지
    private bool TryPrepareRemove(ArtifactData artifact, out PreparedChange change)
    {
        change = default;
        if (artifact == null ||
            !_inventory.TryGetById(artifact.Id, out ArtifactInstance instance) ||
            instance.Data != artifact || instance.StackCount < 1)
        {
            return false;
        }

        int currentStacks = instance.StackCount - 1;
        ConvertedEffects effects = null;
        if (currentStacks > 0 && !_effectManager.TryConvertEffects(instance, new EffectDataGroup
            {
                StatEffects = artifact.UnitStatEffects,
                CurrencyEffects = artifact.CurrencyEffects,
                SlotEffects = artifact.ConsumableSlotEffects,
                PassiveEffects = artifact.PassiveSkillEffects
            }, currentStacks, out effects))
        {
            return false;
        }

        change = new PreparedChange
        {
            Instance = instance,
            PreviousStacks = instance.StackCount,
            CurrentStacks = currentStacks,
            Effects = effects
        };
        return true;
    }

    // 정산 완료 후 호출 : 보유 목록 제거 및 Inventory 구독 해제
    public bool TryEndRun()
    {
        if (!IsInitialized || _isChanging)
        {
            return false;
        }

        ArtifactInventory inventory = _inventory;
        if (_selectionWait != null)
        {
            CancellationTokenSource wait = _selectionWait;
            _selectionWait = null;
            wait.Cancel();
        }
        _rewardApplied = false;
        _hasRewardCandidates = false;
        _rewardQuarter = _rewardWave = 0;
        _selectionCandidates.Clear();
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

    private void PrepareRewardPosition()
    {
        int quarter = _waveController.CurQuarter, wave = _waveController.CurWave;
        if (_rewardQuarter == quarter && _rewardWave == wave) return;
        _rewardQuarter = quarter;
        _rewardWave = wave;
        _rewardApplied = false;
        _hasRewardCandidates = false;
        _selectionCandidates.Clear();
    }

    public ArtifactSaveData CaptureSaveData()
    {
        if (!IsInitialized || _isChanging) throw new InvalidOperationException("아티팩트 초기화 및 변경 완료 후 저장하세요.");
        var data = new ArtifactSaveData {
            HasRewardCandidates = _hasRewardCandidates, RewardApplied = _rewardApplied,
            RewardQuarter = _rewardQuarter, RewardWave = _rewardWave
        };
        foreach (var instance in Instances)
            data.Owned.Add(new ArtifactSaveEntry { ArtifactId = instance.Data.Id, StackCount = instance.StackCount });
        if (!_rewardApplied)
            foreach (var candidate in _selectionCandidates) data.CandidateIds.Add(candidate.Id);
        return data;
    }

    // 전체 데이터와 효과 변환 검증 후에만 보유 상태를 교체합니다.
    public void RestoreSaveData(ArtifactSaveData data)
    {
        if (!IsInitialized || _isChanging || IsSelectingReward)
            throw new InvalidOperationException("아티팩트 초기화 후, 선택 대기를 취소하고 복원하세요.");
        if (data == null || data.Owned == null || data.CandidateIds == null)
            throw new ArgumentException("아티팩트 저장 데이터가 없습니다.");
        if (data.RewardQuarter < 0 || data.RewardWave < 0 || data.RewardWave > WaveController.MAX_WAVE ||
            ((data.RewardQuarter == 0) != (data.RewardWave == 0)) ||
            ((data.HasRewardCandidates || data.RewardApplied) && data.RewardQuarter == 0) ||
            (data.RewardApplied && !data.HasRewardCandidates) ||
            ((!data.HasRewardCandidates || data.RewardApplied) && data.CandidateIds.Count != 0))
            throw new ArgumentException("아티팩트 보상 상태가 올바르지 않습니다.");
        var inventory = new ArtifactInventory(_artifactCatalog);
        var effects = new Dictionary<object, ConvertedEffects>();
        var seen = new HashSet<string>();
        foreach (var entry in data.Owned)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.ArtifactId) || !seen.Add(entry.ArtifactId) ||
                !_artifactCatalog.TryGetById(entry.ArtifactId, out var artifact) || entry.StackCount < 1 || entry.StackCount > artifact.MaxStacks)
                throw new ArgumentException("보유 아티팩트 ID·중첩 수·중복을 확인하세요.");
            var instance = new ArtifactInstance(artifact);
            for (int i = 0; i < entry.StackCount; i++)
                if (!inventory.TryAdd(instance)) throw new ArgumentException("아티팩트 중첩 복원 실패: " + entry.ArtifactId);
            if (!_effectManager.TryConvertEffects(instance, new EffectDataGroup
                {
                    StatEffects = artifact.UnitStatEffects,
                    CurrencyEffects = artifact.CurrencyEffects,
                    SlotEffects = artifact.ConsumableSlotEffects,
                    PassiveEffects = artifact.PassiveSkillEffects
                }, entry.StackCount, out var converted))
                throw new ArgumentException("아티팩트 효과 복원 실패: " + entry.ArtifactId);
            effects.Add(instance, converted);
        }
        var candidates = new List<ArtifactData>();
        seen.Clear();
        foreach (string id in data.CandidateIds)
        {
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id) || !_artifactCatalog.TryGetById(id, out var artifact))
                throw new ArgumentException("보상 후보 ID·중복을 확인하세요.");
            candidates.Add(artifact);
        }
        var removed = new List<ArtifactInstance>(_inventory.Instances);
        _isChanging = true;
        try
        {
            _inventory.Cleared -= HandleCleared;
            _inventory = inventory;
            _inventory.Cleared += HandleCleared;
            _candidateSelector = new ArtifactCandidateSelector(_artifactCatalog, _inventory, _rewardTable);
            _testCandidates = null;
            _selectionCandidates = candidates;
            _hasRewardCandidates = data.HasRewardCandidates;
            _rewardApplied = data.RewardApplied;
            _rewardQuarter = data.RewardQuarter;
            _rewardWave = data.RewardWave;
            // 제거·추가 사이의 임시 슬롯 감소를 노출하지 않습니다. 다른 시스템 효과는 유지합니다.
            _effectManager.ReplaceSourceEffects(removed, effects);
            if (removed.Count > 0) Cleared?.Invoke(removed);
            foreach (var instance in _inventory.Instances) StackChanged?.Invoke(instance, 0, instance.StackCount);
        }
        finally { _isChanging = false; }
        // 대기 작업은 저장하지 않습니다. 게임 흐름에서 SelectAndApplyAsync를 다시 호출하면 저장 후보를 사용합니다.
    }
}
