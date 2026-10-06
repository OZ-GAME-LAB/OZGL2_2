using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Cameras;
using Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    //현재 진행중인 게임 페이즈 
    public enum GamePhase
    {
        // 선언은 진행 순서로 정렬한다.
        None = 0,
        Preparation, //건물 건설 및 준비(건물 건설 / 삭제)
        BattlePreparing, //전투 준비(유닛 스폰)
        Battle, //전투상태
        BattleResolving, //전투 결과 창 출력
        QuarterComplete, //보스 승리 시 유물 보상으로 진행, 부착 처리 후 종료 선택
        Reward, //보상 획득상태
        Event, //이벤트·저주 완료 대기
        Store, //상점 나가기 대기 (Event와 선택적 분기)
        Finished, //게임 종료 상태
    }
    //이어하기시 재개 위치
    public enum RunResumeStep
    {
        Preparation = 0, //전투 준비
        RewardSelection = 1, //보상 선택중
        Event = 2, //이벤트 진입
        Store = 3, //상점 진입
        QuarterDecision = 4, //분기 보상 마무리 후 다음분기 진행여부 결정상황
        Finished = 5 //결산창
    }
    public enum ResultType { Victory, Defeat }
    public enum RunDecision { Finish, Continue }

    [Serializable]
    public class GameFlowSaveData
    {
        public NodeSaveData Node;
        public RunResumeStep ResumeStep;
        public bool HasClearedMainGame;
        public string RunId;

        public GameFlowSaveData(NodeSaveData node, RunResumeStep resumeStep, bool hasCleared, string runId = null)
        {
            Node = node;
            ResumeStep = resumeStep;
            HasClearedMainGame = hasCleared;
            RunId = runId;
        }
    }
    /// <summary>
    /// 게임 전체 페이즈, 전투·보상 처리 순서, 완료 대기, 종료·리셋을 관리한다.
    /// <br/>언제 다음 노드로 이동할지 결정
    /// </summary>
    public class GameFlowController : MonoBehaviour, ISaveDataProvider<GameFlowSaveData>
    {
        private const string MainMenuScene = "Test_MainScreen";

        [Header("테스트용 종료 연출 시간입니다. Play 모드에서 조절하세요.")]
        [Min(0)]
        public float StagingTime = 0.5f;

        [Header("다음 분기 생성 시 적용되는 정예 2개 등장 확률")]
        [SerializeField, Range(0f, 1f)] private float _twoEliteChance = 0.5f;
        private NodeController _nodeController;
        public Node CurrentNode => _nodeController?.CurrentNode;
        public IReadOnlyList<Node> CurrentNodes => _nodeController?.Nodes ?? Array.Empty<Node>();
        public int CurrentQuarter => _nodeController?.CurrentQuarter ?? 0;
        public int CurrentWave => CurrentNode?.WaveNumber ?? 0;
        public bool IsLastNode => _nodeController != null && _nodeController.IsLastNode;

        public event Action<GamePhase> PhaseChanged;
        public event Action QuarterDecisionRequested;
        public event Action ArtifactSelectionRequested;
        public GamePhase CurPhase => _curPhase;
        public bool HasClearedMainGame { get; private set; }
        public bool IsWaitingForRunDecision { get; private set; }
        public bool CanChooseRunDecision => IsWaitingForRunDecision && !_runDecision.HasValue;
        public bool IsWaitingForArtifactSelection { get; private set; }

        [SerializeField] private InGameCameraController _cameraController;
        [SerializeField] private bool _autoContinue;
        public bool AutoContinue { get => _autoContinue; set => _autoContinue = value; }

        private TestWaitingScript _testScript;
        private WaveController _waveController; //인게임 전투 담당
        private ArtifactManager _artifactManager; //게임 진행 중 아티팩트 클리어 담당
        private ArchiveManager _archiveManager;
        private IRunSettlementRewards _settlementRewards;
        private IContinueUI _continueUI;
        private IRunDecisionUI _decisionUI;
        private IShopFlow _shopFlow;
        private GamePhase _curPhase;
        private RunResumeStep _resumeStep;
        private IRunCheckpointWriter _checkpointWriter;
        private bool _hasBegun;
        private bool _restoredStep;
        private bool _startupBlocked;
        private bool _settlementViewPrepared;
        public string RunId { get; private set; }
        public RunResumeStep ResumeStep => _resumeStep;
        public bool IsResumingStep { get; private set; }
        private bool _isTransitioning;
        private bool _isResetting;
        private UniTaskCompletionSource _spawnCompletion;
        private CancellationTokenSource _cts;
        private RunDecision? _runDecision;

        public bool CanJumpToLastWave => CanEnterBuildMode() && CurrentNode != null && !IsLastNode;
        public bool CanJumpToLastQuarter => CanEnterBuildMode() && CurrentQuarter < NodeController.MainQuarters;

        // 초기화 및 수명 관리
        /// <summary>참조를 연결하고 기존 실행을 취소한다.</summary>
        public void Initialize(
            WaveController waveController, 
            TestWaitingScript testScript, 
            ArtifactManager artifactManager, 
            ArchiveManager archiveManager = null,
            IRunSettlementRewards settlementRewards = null,
            InGameCameraController cameraController = null,
            IShopFlow shopFlow = null)
        {
            _testScript = testScript;
            _waveController = waveController;
            _artifactManager = artifactManager;
            _archiveManager = archiveManager;
            _settlementRewards = settlementRewards;
            _cameraController = cameraController;
            _shopFlow = shopFlow;
            _nodeController = new NodeController(waveController.WaveCatalog);
            ClearToken();
            ResetDecisionState();
            _isTransitioning = false;
            _curPhase = GamePhase.None;
            _resumeStep = RunResumeStep.Preparation;
            HasClearedMainGame = false;
            RunId = Guid.NewGuid().ToString("N");
            _hasBegun = false;
            _restoredStep = false;
            _startupBlocked = false;
            _settlementViewPrepared = false;
            IsResumingStep = false;
            if (!_nodeController.TryStartQuarter(1, _twoEliteChance, out string error))
                throw new InvalidOperationException("기본 런 준비 실패: " + error);
        }

        public void InitializeSave(IRunCheckpointWriter checkpointWriter)
        {
            _checkpointWriter = checkpointWriter ?? throw new ArgumentNullException(nameof(checkpointWriter));
        }

        public void BlockStartup()
        {
            _startupBlocked = true;
            _isTransitioning = true;
        }

        /// <summary>호출하고 결과를 기다리는 UI 계약을 연결한다. 게임 데이터는 Flow가 전달한다.</summary>
        public void InitializeUI(IContinueUI continueUI, IRunDecisionUI decisionUI)
        {
            if (continueUI == null) throw new ArgumentNullException(nameof(continueUI));
            if (decisionUI == null) throw new ArgumentNullException(nameof(decisionUI));
            _continueUI = continueUI;
            _decisionUI = decisionUI;
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
        
        // 기존 버튼/샘플의 연결은 유지하되 준비된 런을 다시 초기화하지 않는다.
        public void NewGame() => BeginRun();
        public void Continue() => BeginRun();
        public void BeginRun() => BeginRunAsync().Forget();

        public async UniTask BeginRunAsync()
        {
            if (_hasBegun || _startupBlocked || _curPhase != GamePhase.None) return;
            _hasBegun = true;
            _isTransitioning = true;
            var lifetime = _cts;
            var token = lifetime.Token;
            try
            {
                if (_resumeStep == RunResumeStep.Finished)
                {
                    await LeaveCompletedRunAsync(token);
                    return;
                }
                NotifyNodeChanged();
                IsResumingStep = _restoredStep;
                if (_resumeStep == RunResumeStep.Preparation)
                    await EnterPreparationAsync(token);
                else
                    await ContinueAfterVictoryAsync(_resumeStep, token);
            }
            // UI의 OnDisable 취소가 Flow.OnDestroy보다 먼저 도착할 수 있다.
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                BlockStartup();
                Debug.LogError("[Save/GameFlow] 런 시작 실패: " + exception.Message, this);
            }
            finally
            {
                IsResumingStep = false;
                if (ReferenceEquals(_cts, lifetime) && !_startupBlocked) _isTransitioning = false;
            }
        }

        public void PrepareCompletedRun(string runId)
        {
            if (!string.IsNullOrWhiteSpace(runId)) RunId = runId;
            _resumeStep = RunResumeStep.Finished;
            _restoredStep = true;
        }
        //게임을 종료하고 메인으로 이동
        public void QuitRun()
        {
            if (_isResetting) return;
            _isResetting = true;
            ReturnToMainMenuAsync().Forget();
        }
        //현재 진행중인 값 저장
        public GameFlowSaveData CaptureSaveData()
        {
            return new GameFlowSaveData(
                _nodeController.CaptureSaveData(),
                _resumeStep,
                HasClearedMainGame, RunId);
        }
        //저장된 값 불러와서 다시 쓸 수 있게 복원
        public void RestoreSaveData(GameFlowSaveData data)
        {
            if (data == null || !Enum.IsDefined(typeof(RunResumeStep), data.ResumeStep))
                throw new ArgumentException("게임 진행 저장값이 올바르지 않습니다.");
            _nodeController.RestoreSaveData(data.Node);
            if ((data.ResumeStep == RunResumeStep.Event && CurrentNode.PostBattleEvent != PostBattleEventType.Event &&
                 CurrentNode.PostBattleEvent != PostBattleEventType.Curse) ||
                (data.ResumeStep == RunResumeStep.Store && CurrentNode.PostBattleEvent != PostBattleEventType.Shop) ||
                (data.ResumeStep == RunResumeStep.QuarterDecision && (!IsLastNode || !data.HasClearedMainGame)))
                throw new ArgumentException("저장된 재개 단계와 현재 노드가 일치하지 않습니다.");
            _resumeStep = data.ResumeStep;
            HasClearedMainGame = data.HasClearedMainGame;
            if (!string.IsNullOrWhiteSpace(data.RunId)) RunId = data.RunId;
            _restoredStep = true;
        }

        private async UniTask ReturnToMainMenuAsync()
        {
            var pendingSpawn = _spawnCompletion?.Task ?? UniTask.CompletedTask;
            _isResetting = true;
            _isTransitioning = true;
            ClearToken();
            var token = _cts.Token;
            ResetDecisionState();
            ChangePhase(GamePhase.None);
            try
            {
                // 스포너의 배치 공간 정리까지 끝난 후 런타임을 비운다.
                await pendingSpawn;
                token.ThrowIfCancellationRequested();
                await SceneManager.LoadSceneAsync(MainMenuScene);
            }
            finally
            {
                _isResetting = false;
            }
        }

        /// <summary>
        /// 현재 건물 건설이 가능한 페이즈인지 반환하는 메서드
        /// 사용처 : 건설시스템
        /// </summary>
        public bool CanEnterBuildMode()
        {
            return !_isTransitioning && _curPhase == GamePhase.Preparation;
        }

        /// <summary>
        /// 유닛이 서로 전투가능한 상태인지 반환하는 메서드
        /// 사용처 : 유닛시스템
        /// </summary>
        public bool CanBattle()
        {
            return !_isTransitioning && _curPhase == GamePhase.Battle;
        }

        // 전투 시작 및 결과 처리
        public async UniTask<bool> TrySpawnUnits()
        {
            if (_isTransitioning || _curPhase != GamePhase.Preparation) return false;
            if (_waveController.CurrentPreset == null) return false;
            _isTransitioning = true;
            var token = _cts.Token;
            var completion = new UniTaskCompletionSource();
            _spawnCompletion = completion;
            try
            {
                await SaveCheckpointAsync(token);
                ChangePhase(GamePhase.BattlePreparing);
                if (token.IsCancellationRequested) return false;
                _cameraController.ShowBase();
                // 건물은 BattlePreparing 알림으로 아군을 생성한다.
                // 아군·적 생성과 배치가 끝나면 RuntimeUnitManager의 준비 완료 알림으로 전투에 진입한다.

                _cameraController.ShowBattleField();
                // 리셋은 진행만 취소하고 적 생성은 완료까지 기다린다. 파괴 시에는 생성도 취소한다.
                bool canceled = await _waveController.PrepareEnemy(
                    token, this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();

                if (canceled || token.IsCancellationRequested) return false;
                return true;
            }
            finally
            {
                completion.TrySetResult();
                if (ReferenceEquals(_spawnCompletion, completion))
                    _spawnCompletion = null;
            }
        }

        public void TryStartWave()
        {
            ChangePhase(GamePhase.Battle);
            _waveController.BattleStart();
            _isTransitioning = false;
        }

        /// <summary> 전투 결과를 받는 외부노출 메서드 </summary>
        public async UniTask ResolveBattleAsync(ResultType result)
        {
            if (_isTransitioning || _curPhase != GamePhase.Battle) return;
            _isTransitioning = true;
            CancellationTokenSource lifetime = _cts;
            var token = lifetime.Token;
            try
            {
                await ResolveBattleCoreAsync(result, token).SuppressCancellationThrow();
            }
            finally
            {
                // 화면 취소/오류도 현재 작업의 잠금을 해제한다. 이전 판은 새 판을 변경하지 않는다.
                if (ReferenceEquals(_cts, lifetime)) _isTransitioning = false;
            }
        }

        /// <summary> 전투 종료 후 연출·보상·분기 진행 순서 처리 등 실제 기능 구현 </summary>
        private async UniTask ResolveBattleCoreAsync(ResultType result, CancellationToken token)
        {
            Node completedNode = CurrentNode;
            ChangePhase(GamePhase.BattleResolving);
            // 실제 유닛 파트는 이 페이즈 진입 시 공격·이동·피해 처리를 중단해야 한다.
            await PlayBattleResultAsync(result, token);
            token.ThrowIfCancellationRequested(); //token이 들어간 작업이 중단되면 취소 예외 발생
            
            //패배시 처리
            if (result == ResultType.Defeat)
            {
                //전장 정리
                await CleanupBattleAsync(token);
                token.ThrowIfCancellationRequested(); 
                //게임 종료
                await FinishRun(result, token);
                return;
            }
            // 승리를 확정한 현재 노드의 기록을 다음 노드로 이동하기 전에 한 번 알린다.
            _waveController.NotifyWaveCleared(new WaveInfo(CurrentQuarter, completedNode.WaveNumber,
                completedNode.BattleType, completedNode.PostBattleEvent));
            if (IsLastNode && CurrentQuarter >= NodeController.MainQuarters)
                HasClearedMainGame = true;
            await ContinueAfterVictoryAsync(RunResumeStep.RewardSelection, token);
        }

        // 승리 직후와 저장된 후속 단계가 같은 진행 경로를 사용한다.
        private async UniTask ContinueAfterVictoryAsync(RunResumeStep startStep, CancellationToken token)
        {
            Node completedNode = CurrentNode;
            if (startStep == RunResumeStep.RewardSelection)
            {
                await WaitArtifactSelection(completedNode.BattleType, token);
                await CleanupBattleAsync(token);
            }
            if (startStep != RunResumeStep.QuarterDecision)
                await ProcessEventAsync(completedNode, token);
            token.ThrowIfCancellationRequested();

            if (!IsLastNode)
                AdvanceNode();
            else
            {
                if (startStep == RunResumeStep.QuarterDecision || (HasClearedMainGame && !AutoContinue))
                {
                    RunDecision decision = await ChooseQuarterDecisionAsync(token);
                    if (decision == RunDecision.Finish)
                    {
                        await FinishRun(ResultType.Victory, token);
                        return;
                    }
                }
                if (!StartQuarter(CurrentQuarter + 1))
                    throw new InvalidOperationException("다음 분기 생성에 실패했습니다.");
            }
            await EnterPreparationAsync(token);
        }

        private async UniTask EnterPreparationAsync(CancellationToken token)
        {
            _resumeStep = RunResumeStep.Preparation;
            _cameraController?.ShowBase();
            ChangePhase(GamePhase.Preparation);
            await SaveCheckpointAsync(token);
            IsResumingStep = false;
        }

        private async UniTask<RunDecision> ChooseQuarterDecisionAsync(CancellationToken token)
        {
            _resumeStep = RunResumeStep.QuarterDecision;
            ChangePhase(GamePhase.QuarterComplete);
            await SaveCheckpointAsync(token);
            IsResumingStep = false;
            _runDecision = null;
            IsWaitingForRunDecision = true;
            try
            {
                if (_decisionUI != null)
                    _runDecision = await _decisionUI.ChooseAsync(CurrentQuarter, token);
                else
                {
                    QuarterDecisionRequested?.Invoke();
                    await UniTask.WaitUntil(() => _runDecision.HasValue, cancellationToken: token);
                }
                token.ThrowIfCancellationRequested();
                if (_runDecision != RunDecision.Finish && _runDecision != RunDecision.Continue)
                    throw new InvalidOperationException("종료 선택 UI가 유효하지 않은 선택을 반환했습니다.");
                return _runDecision.Value;
            }
            finally { IsWaitingForRunDecision = false; }
        }

        /// <summary>
        /// 돌발 이벤트 완료까지만 담당하고 이후 진행은 승리 처리 경로에서 결정한다.
        /// </summary>
        private async UniTask ProcessEventAsync(Node completedNode, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (completedNode.PostBattleEvent == PostBattleEventType.None) return;

            if (completedNode.PostBattleEvent == PostBattleEventType.Shop)
            {
                if (_shopFlow == null)
                    throw new InvalidOperationException("상점 진행 계약을 먼저 연결해주세요.");
                PrepareShopCheckpoint();
                _resumeStep = RunResumeStep.Store;
                ChangePhase(GamePhase.Store);
                await SaveCheckpointAsync(token);
                IsResumingStep = false;
                token.ThrowIfCancellationRequested();
                await _shopFlow.OpenShopAsync(token);
                token.ThrowIfCancellationRequested();
                return;
            }

            _resumeStep = RunResumeStep.Event;
            GamePhase phase = GamePhase.Event;
            if (_continueUI != null)
            {
                ChangePhase(phase);
                await SaveCheckpointAsync(token);
                IsResumingStep = false;
                token.ThrowIfCancellationRequested();
                await _continueUI.ShowAsync("돌발 이벤트를 확인했습니다.\n계속해서 다음 전투를 준비합니다.", token);
                token.ThrowIfCancellationRequested();
                return;
            }
            // 페이즈 구독자가 즉시 완료할 수 있도록 대기를 먼저 준비한다.
            var contentWait = _testScript.WaitPostBattleContentAsync(completedNode, token);
            ChangePhase(phase);
            await SaveCheckpointAsync(token);
            IsResumingStep = false;
            await contentWait;
            token.ThrowIfCancellationRequested();
        }

        // 기존 상점 API로 후보를 먼저 확정해 UI 표시 전 체크포인트에 포함한다.
        private void PrepareShopCheckpoint()
        {
            if (_shopFlow is ShopManager shop)
            {
                var data = shop.CaptureSaveData();
                if ((!data.HasStock || (data.StockQuarter != 0 && data.StockQuarter != CurrentQuarter)) &&
                    !shop.TryGenerateStock())
                    throw new InvalidOperationException("상점 품목 생성에 실패했습니다.");
                return;
            }
            if (_checkpointWriter != null)
                throw new InvalidOperationException("저장할 상점 후보를 준비할 수 있는 ShopManager를 연결해주세요.");
        }

        // 분기·노드 진행
        private bool StartQuarter(int quarter)
        {
            if (!_nodeController.TryStartQuarter(quarter, _twoEliteChance, out string error))
            {
                Debug.LogError($"[GameFlowController] 노드 생성 실패: {error}", this);
                ChangePhase(GamePhase.None);
                return false;
            }
            NotifyNodeChanged();
            return true;
        }

        private void AdvanceNode()
        {
            if (_nodeController.MoveNext()) NotifyNodeChanged();
        }

        private void NotifyNodeChanged()
        {
            _waveController.NotifyNodeChanged();
        }

        // 게임 종료 및 내부 상태 관리
        private async UniTask FinishRun(ResultType type, CancellationToken token)
        {
            Debug.Log($"[Core/GameFlowController] 게임 종료 : {type}");
            ResetDecisionState();
            token.ThrowIfCancellationRequested();
            if (_archiveManager == null || _settlementRewards == null)
                throw new InvalidOperationException("게임 종료 기록과 정산 매니저를 먼저 연결해주세요.");

            // 재시도에도 같은 확정 기록을 사용해야 이미 저장한 보상을 다시 지급하지 않는다.
            if (!_archiveManager.TryGetRunSummary(out RunSummary summary))
            {
                _archiveManager.CompleteRun();
                if (!_archiveManager.TryGetRunSummary(out summary))
                    throw new InvalidOperationException("게임 결산 기록을 불러올 수 없습니다.");
            }

            // 런 ID를 지급 전에 인게임 파일에 확정한다.
            await SaveCheckpointAsync(token);
            while (!await _settlementRewards.TryApplyReward(summary, token))
            {
                Debug.LogError("[Save/GameFlow] 정산 확정 또는 표시 실패: 정산 로그를 확인해주세요.", this);
                token.ThrowIfCancellationRequested();
                if (_continueUI == null)
                    throw new InvalidOperationException("정산 실패 안내 UI를 먼저 연결해주세요.");
                await _continueUI.ShowAsync("정산을 완료하지 못했습니다.\n계속 버튼을 누르면 다시 시도합니다.", token);
            }
            token.ThrowIfCancellationRequested();
            // 저장 연결이 없는 기존 독립 테스트도 종료 상태를 정리한다.
            await PrepareSettlementViewAsync(token);
            await LeaveCompletedRunAsync(token);
        }

        // 정산 저장 브리지가 보상·완료 기록 확정 후, 결산 UI 표시 전에 호출한다.
        public async UniTask PrepareSettlementViewAsync(CancellationToken token)
        {
            if (_settlementViewPrepared) return;
            token.ThrowIfCancellationRequested();
            _resumeStep = RunResumeStep.Finished;
            await SaveCheckpointAsync(token);
            // 보상은 효과 해제 전에 이미 파일에 확정됐다.
            ChangePhase(GamePhase.Finished);
            if (_artifactManager != null && _artifactManager.IsInitialized && !_artifactManager.TryEndRun())
                throw new InvalidOperationException("정산 후 유물 상태를 정리하지 못했습니다.");
            _settlementViewPrepared = true;
        }

        private async UniTask SaveCheckpointAsync(CancellationToken token)
        {
            // 저장기를 연결하지 않은 기존 독립 테스트/샘플은 파일을 만들지 않는다.
            if (_checkpointWriter == null) return;
            while (!_checkpointWriter.TrySave(out string error))
            {
                token.ThrowIfCancellationRequested();
                Debug.LogError("[Save/GameFlow] 체크포인트 저장 실패: " + error, this);
                if (_continueUI == null) throw new InvalidOperationException(error);
                await _continueUI.ShowAsync("진행 상태를 저장하지 못했습니다.\n계속 버튼을 누르면 다시 시도합니다.", token);
            }
            token.ThrowIfCancellationRequested();
        }

        private async UniTask LeaveCompletedRunAsync(CancellationToken token)
        {
            if (_checkpointWriter != null)
            {
                while (!_checkpointWriter.TryDelete(out string error))
                {
                    Debug.LogError("[Save/GameFlow] 완료 런 삭제 실패: " + error, this);
                    if (_continueUI == null) throw new InvalidOperationException(error);
                    await _continueUI.ShowAsync("완료된 런의 저장 파일을 정리하지 못했습니다.\n계속 버튼을 누르면 다시 시도합니다.", token);
                }
            }
            token.ThrowIfCancellationRequested();
            await ReturnToMainMenuAsync();
        }

        private void ChangePhase(GamePhase phase)
        {
            _curPhase = phase;
            PhaseChanged?.Invoke(phase);
        }

        private void ClearToken()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }

        private void ResetDecisionState()
        {
            _runDecision = null;
            IsWaitingForRunDecision = false;
            IsWaitingForArtifactSelection = false;
        }

        #region Debug
        // 테스트·호환용 진행 입력 및 임시 처리
        // 기존 테스트/외부 호출의 호환 경로. 전투·보상 처리 중에는 이동하지 않는다.
        public void RequestProgressStage()
        {
            if (CanEnterBuildMode()) AdvanceNode();
        }

        public void RequestProgressQuarter()
        {
            if (CanEnterBuildMode() && IsLastNode) StartQuarter(CurrentQuarter + 1);
        }

        public void JumpToLastWaveForTest()
        {
            if (CanJumpToLastWave && _nodeController.JumpToLastNode()) NotifyNodeChanged();
        }

        public void JumpToLastQuarterForTest()
        {
            if (CanJumpToLastQuarter) StartQuarter(NodeController.MainQuarters);
        }

        [ContextMenu("Test/Finish at quarter choice")]
        public void ChooseFinishRun()
        {
            if (CanChooseRunDecision)
                _runDecision = RunDecision.Finish;
        }

        [ContextMenu("Test/Continue at quarter choice")]
        public void ChooseContinueRun()
        {
            if (CanChooseRunDecision)
                _runDecision = RunDecision.Continue;
        }

        /// <summary>재화 확인창을 거치지 않고 유물 선택·적용 완료를 기다린다.</summary>
        private async UniTask WaitArtifactSelection(WaveBattleType battleType, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!_artifactManager.TryCreateCandidates(out _))
                throw new InvalidOperationException("유물 보상 후보를 준비하지 못했습니다.");
            _resumeStep = RunResumeStep.RewardSelection;
            if (_continueUI != null)
            {
                // 정상 진입은 지급 후 저장하고, 복원 진입은 지급을 건너뛴다.
                ChangePhase(GamePhase.Reward);
                await SaveCheckpointAsync(token);
                IsResumingStep = false;
                token.ThrowIfCancellationRequested();
                IsWaitingForArtifactSelection = true;
                try
                {
                    if (!await _artifactManager.SelectAndApplyAsync(battleType, token))
                        throw new InvalidOperationException("Artifact selection and application did not complete.");
                    token.ThrowIfCancellationRequested();
                }
                finally
                {
                    if (!token.IsCancellationRequested) IsWaitingForArtifactSelection = false;
                }
                return;
            }

            // 기존 테스트/임시 UI 호출자는 이전 대기 계약을 유지한다.
            IsWaitingForArtifactSelection = true;
            
            // 페이즈/선택 요청 구독자가 즉시 버튼 입력을 보내도 초기화로 덮어쓰지 않는다.
            var rewardWait = _testScript.WaitToggle(token);
            ChangePhase(GamePhase.Reward);
            await SaveCheckpointAsync(token);
            IsResumingStep = false;
            token.ThrowIfCancellationRequested();
            ArtifactSelectionRequested?.Invoke();
            token.ThrowIfCancellationRequested();

            // 현재 미완성 선택 UI의 false 반환은 허용하고 테스트 버튼으로 완료한다.
            await _artifactManager.SelectAndApplyAsync(battleType, token);
            token.ThrowIfCancellationRequested();
            await rewardWait; // TODO: 실제 아티팩트 선택·적용 완료가 연결되면 이 토글 대기를 제거한다.
            token.ThrowIfCancellationRequested();
            IsWaitingForArtifactSelection = false;
        }

        /// <summary>임시 연출 대기. 실제 컷씬·통계창 완료를 기다리는 구현으로 교체한다.</summary>
        private UniTask PlayBattleResultAsync(ResultType result, CancellationToken token)
        {
            return UniTask.Delay(TimeSpan.FromSeconds(StagingTime), cancellationToken: token);
        }

        /// <summary>현재 전투의 유닛·그룹과 준비 상태를 정리한다.</summary>
        private UniTask CleanupBattleAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            _waveController.CleanupBattle();
            return UniTask.CompletedTask;
        }
    #endregion
    }
}
