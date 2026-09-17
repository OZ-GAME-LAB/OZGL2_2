using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Cameras;
using UnityEngine;

namespace Game.Core
{
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

    public enum ResultType { Victory, Defeat }
    public enum RunDecision { Finish, Continue }

    /// <summary>
    /// 게임 전체 페이즈, 전투·보상 처리 순서, 완료 대기, 종료·리셋을 관리한다.
    /// <br/>언제 다음 노드로 이동할지 결정
    /// </summary>
    public class GameFlowController : MonoBehaviour
    {
        [Header("테스트용 생성 대기 시간입니다. Play 모드에서 조절하세요.")]
        [Min(0)]
        public float SpawnTime = 1f;

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
        private WaveController _waveController;
        private ArtifactManager _artifactManager;
        private GamePhase _curPhase;
        private bool _isTransitioning;
        private bool _isResetting;
        private UniTaskCompletionSource _spawnCompletion;
        private CancellationTokenSource _cts;
        private RunDecision? _runDecision;

        public bool CanJumpToLastWave => CanEnterBuildMode() && CurrentNode != null && !IsLastNode;
        public bool CanJumpToLastQuarter => CanEnterBuildMode() && CurrentQuarter < NodeController.MainQuarters;

        // 초기화 및 수명 관리
        /// <summary>참조를 연결하고 기존 실행을 취소한다.</summary>
        public void Initialize(WaveController waveController, TestWaitingScript testScript, ArtifactManager artifactManager)
        {
            _testScript = testScript;
            _waveController = waveController;
            _artifactManager = artifactManager;
            _nodeController = new NodeController(waveController.WaveCatalog);
            ClearToken();
            ResetDecisionState();
            _isTransitioning = false;
            ChangePhase(GamePhase.None);
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        // 게임 시작·리셋 및 행동 가능 여부
        /// <summary>새 판은 1분기 1웨이브부터 시작한다.</summary>
        public void BeginRun()
        {
            if (_isTransitioning) return;
            if (_curPhase != GamePhase.None && _curPhase != GamePhase.Finished) return;
            _isTransitioning = true;
            HasClearedMainGame = false;
            ResetDecisionState();
            _nodeController.Reset();
            _waveController.CleanupBattle();
            _waveController.ResetTestBattle();
            if (!StartQuarter(1))
            {
                _isTransitioning = false;
                return;
            }
            ChangePhase(GamePhase.Preparation);
            _isTransitioning = false;
        }

        public void ResetRun()
        {
            if (_isResetting) return;
            _isResetting = true;
            ResetRunAsync().Forget();
        }

        private async UniTask ResetRunAsync()
        {
            var pendingSpawn = _spawnCompletion?.Task ?? UniTask.CompletedTask;
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
                _isTransitioning = false;
                BeginRun();
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
                ChangePhase(GamePhase.BattlePreparing);
                if (token.IsCancellationRequested) return false;
                _cameraController.ShowBase();
                // 실제 아군 준비 연동 시 적 준비와 함께 완료를 기다린다.

                _cameraController.ShowBattleField();
                // 리셋은 진행만 취소하고 적 생성은 완료까지 기다린다. 파괴 시에는 생성도 취소한다.
                bool canceled = await _waveController.PrepareEnemy(
                    SpawnTime, token, this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();

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

        /// <summary>
        /// 전투 결과를 받는 외부노출 메서드
        /// </summary>
        public async UniTask ResolveBattleAsync(ResultType result)
        {
            if (_isTransitioning || _curPhase != GamePhase.Battle) return;
            _isTransitioning = true;
            var token = _cts.Token;
            bool canceled = await ResolveBattleCoreAsync(result, token).SuppressCancellationThrow();
            
            // 이전 판의 취소된 작업은 새 판의 잠금이나 상태를 변경하지 않는다.
            if (canceled || token.IsCancellationRequested) return;
            _isTransitioning = false;
        }

        /// <summary>
        /// 전투 종료 후 연출·보상·분기 진행 순서 처리 등 실제 기능 구현
        /// </summary>
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
                FinishRun(result);
                return;
            }
            //분기 마지막 웨이브가 아닐경우 그대로 보상처리 후 진행
            if (!IsLastNode)
            {
                //보상 선택
                await WaitArtifactSelection(completedNode.BattleType, token);
                //전장정리
                await CleanupBattleAsync(token);
                token.ThrowIfCancellationRequested();
                //이벤트 확인
                await ProcessEventAsync(completedNode, token);
                token.ThrowIfCancellationRequested();
                AdvanceNode();
            }
            //분기 마지막웨이브일 때 처리
            else
            {
                // 기본 구간 클리어 기록은 계속 도전하거나 이후 패배해도 유지한다.
                if (CurrentQuarter >= NodeController.MainQuarters)
                    HasClearedMainGame = true;

                ChangePhase(GamePhase.QuarterComplete);
                token.ThrowIfCancellationRequested();
                //아티팩트 + 재화 획득
                await WaitArtifactSelection(completedNode.BattleType, token);
                
                //전장정리
                await CleanupBattleAsync(token);
                token.ThrowIfCancellationRequested();
                await ProcessEventAsync(completedNode, token);
                token.ThrowIfCancellationRequested();
                ChangePhase(GamePhase.QuarterComplete);
                token.ThrowIfCancellationRequested();
                //분기 종료후 계속 진행할지 확인하는 부분
                if (HasClearedMainGame && !AutoContinue)
                {
                    _runDecision = null;
                    IsWaitingForRunDecision = true;
                    //차후 해당 파트를 ShowContinueConfirmationAsync 메서드 호출로 변경
                    QuarterDecisionRequested?.Invoke();
                    await UniTask.WaitUntil(() => _runDecision.HasValue, cancellationToken: token);
                    token.ThrowIfCancellationRequested();
                    
                    IsWaitingForRunDecision = false;

                    if (_runDecision.Value == RunDecision.Finish)
                    {
                        FinishRun(ResultType.Victory);
                        return;
                    }
                }

                if (!StartQuarter(CurrentQuarter + 1)) return;
            }
            token.ThrowIfCancellationRequested();
            _cameraController.ShowBase();
            ChangePhase(GamePhase.Preparation);
        }

        /// <summary>
        /// 돌발 이벤트 완료까지만 담당하고 이후 진행은 승리 처리 경로에서 결정한다.
        /// </summary>
        private async UniTask ProcessEventAsync(Node completedNode, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (completedNode.PostBattleEvent == PostBattleEventType.None) return;

            GamePhase phase = completedNode.PostBattleEvent == PostBattleEventType.Shop
                ? GamePhase.Store : GamePhase.Event;
            // 페이즈 구독자가 즉시 완료할 수 있도록 대기를 먼저 준비한다.
            var contentWait = _testScript.WaitPostBattleContentAsync(completedNode, token);
            ChangePhase(phase);
            await contentWait;
            token.ThrowIfCancellationRequested();
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
        private void FinishRun(ResultType type)
        {
            Debug.Log($"[Core/GameFlowController] 게임 종료 : {type}");
            ResetDecisionState();
            ChangePhase(GamePhase.Finished);
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
        // 외부 시스템 연동 예정 메서드
        /// <summary>
        /// 게임을 계속 할지 확인하는 창을 띄우고 결과를 반환하는 메서드
        /// true: 계속 진행 / false: 승리 종료, 담당 요청 파트 : UI
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async UniTask<bool> ShowContinueConfirmationAsync(CancellationToken token)
        {
            //팝업 띄워서 계속진행할지 버튼클릭 받은 수 결과를 반환해주시면 됩니다. 
            await UniTask.Delay(1000, cancellationToken: token);
            return default;
        }

        /// <summary>
        /// 보상 유물을 선택해서 실제 적용까지 완료된 상태가 기준
        /// 담당 요청 파트 : 재화
        /// </summary>
        /// <param name="token"></param>
        public async UniTask SelectAndApplyAsync(WaveBattleType type,CancellationToken token)
        {
            
        }

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

        /// <summary>아티팩트 선택 UI 연동 전까지 기존 ChooseResult 버튼으로 보상 완료를 기다린다.</summary>
        private async UniTask WaitArtifactSelection(WaveBattleType battleType, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            IsWaitingForArtifactSelection = true;
            
            // 페이즈/선택 요청 구독자가 즉시 버튼 입력을 보내도 초기화로 덮어쓰지 않는다.
            var rewardWait = _testScript.WaitToggle(token);
            ChangePhase(GamePhase.Reward);
            token.ThrowIfCancellationRequested();
            ArtifactSelectionRequested?.Invoke();
            token.ThrowIfCancellationRequested();

            // 현재 미완성 선택 UI의 false 반환은 허용하고 테스트 버튼으로 완료한다.
            await _artifactManager.SelectAndApplyAsync(battleType, token);
            token.ThrowIfCancellationRequested();
            // TODO: 실제 아티팩트 선택·적용 완료가 연결되면 이 토글 대기를 제거한다.
            await rewardWait;
            token.ThrowIfCancellationRequested();
            IsWaitingForArtifactSelection = false;
        }

        /// <summary>임시 연출 대기. 실제 컷씬·통계창 완료를 기다리는 구현으로 교체한다.</summary>
        private UniTask PlayBattleResultAsync(ResultType result, CancellationToken token)
        {
            return _testScript.WaitForSeconds(StagingTime, token);
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
