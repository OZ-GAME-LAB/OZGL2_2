using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Core
{
    public enum GamePhase
    {
        None = 0,
        Preparation = 1,
        BattlePreparing = 2,
        Battle = 3,
        Reward = 4,
        Finished = 5,
        BattleResolving = 6,
        QuarterComplete = 7
    }

    public enum ResultType { Victory, Defeat }
    public enum RunDecision { Finish, Continue }

    public class GameFlowController : MonoBehaviour
    {
        [Header("테스트용 생성 대기 시간입니다. Play 모드에서 조절하세요.")]
        [Min(0)]
        public float SpawnTime = 1f;

        [Header("테스트용 종료 연출 시간입니다. Play 모드에서 조절하세요.")]
        [Min(0)]
        public float StagingTime = 0.5f;

        public event Action<GamePhase> PhaseChanged;
        public event Action QuarterDecisionRequested;
        public event Action ArtifactSelectionRequested;
        public GamePhase CurPhase => _curPhase;
        public bool HasClearedMainGame { get; private set; }
        public bool IsWaitingForRunDecision { get; private set; }
        public bool CanChooseRunDecision => IsWaitingForRunDecision && !_runDecision.HasValue;
        public bool IsWaitingForArtifactSelection { get; private set; }

        [SerializeField] private bool _autoContinue;
        public bool AutoContinue { get => _autoContinue; set => _autoContinue = value; }

        private TestWaitingScript _testScript;
        private WaveController _waveController;
        private GamePhase _curPhase;
        private bool _isTransitioning;
        private CancellationTokenSource _cts;
        private RunDecision? _runDecision;

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        /// <summary>참조를 연결하고 기존 실행을 취소한다.</summary>
        public void Initialize(WaveController waveController, TestWaitingScript testScript)
        {
            _testScript = testScript;
            _waveController = waveController;
            ClearToken();
            ResetDecisionState();
            _isTransitioning = false;
            ChangePhase(GamePhase.None);
        }

        /// <summary>새 판은 1분기 1웨이브부터 시작한다.</summary>
        public void BeginRun()
        {
            if (_isTransitioning) return;
            if (_curPhase != GamePhase.None && _curPhase != GamePhase.Finished) return;
            _isTransitioning = true;
            HasClearedMainGame = false;
            ResetDecisionState();
            _waveController.BeginRun();
            ChangePhase(GamePhase.Preparation);
            _isTransitioning = false;
        }

        public void ResetRun()
        {
            ClearToken();
            ResetDecisionState();
            _isTransitioning = false;
            ChangePhase(GamePhase.None);
            BeginRun();
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
        
        public async UniTask<bool> TryStartWave()
        {
            if (_isTransitioning || _curPhase != GamePhase.Preparation) return false;
            _isTransitioning = true;
            var token = _cts.Token;
            ChangePhase(GamePhase.BattlePreparing);
            // 실제 아군 준비 연동 시 적 준비와 함께 완료를 기다린다.
            bool canceled = await _waveController.PrepareEnemy(SpawnTime, token).SuppressCancellationThrow();
            
            if (canceled || token.IsCancellationRequested) return false;
            ChangePhase(GamePhase.Battle);
            _isTransitioning = false;
            return true;
        }
        /// <summary>
        /// 전투 결과를 받는 외부노출 메서드
        /// </summary>
        /// <param name="result"></param>
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
        /// <param name="result"></param>
        /// <param name="token"></param>
        private async UniTask ResolveBattleCoreAsync(ResultType result, CancellationToken token)
        {
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
            if (!_waveController.IsLastWave)
            {
                ChangePhase(GamePhase.Reward);
                //보상 선택
                await _testScript.WaitToggle(token);
                token.ThrowIfCancellationRequested();
                //전장 정리
                await CleanupBattleAsync(token);
                token.ThrowIfCancellationRequested();
                //웨이브 진행
                _waveController.ProgressStage();
            }
            //분기 마지막웨이브일 때 처리
            else
            {
                // 기본 구간 클리어 기록은 계속 도전하거나 이후 패배해도 유지한다.
                if (_waveController.CurQuarter >= WaveController.MAIN_QUARTERS)
                    HasClearedMainGame = true;

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
                        await CleanupBattleAsync(token);
                        token.ThrowIfCancellationRequested();
                        FinishRun(ResultType.Victory);
                        return;
                    }
                }

                // 요청 이벤트 전에 선택 값을 초기화한다.
                // 이벤트 구독자가 즉시 선택을 완료해도 그 결과가 초기화로 지워지지 않게 한다.
                // 아티팩트 선택
                ChangePhase(GamePhase.Reward);
                var artifactWait = _testScript.WaitToggle(token);
                IsWaitingForArtifactSelection = true;
                ArtifactSelectionRequested?.Invoke();
                
                await artifactWait;
                token.ThrowIfCancellationRequested();
                IsWaitingForArtifactSelection = false;
                
                //IsWaitingForArtifactSelection = true;
                //차후에는 아티펙트 시스템에 아티팩트 선택 요청을 보내면 선택 완료 적용까지 마무리된 후 종료
                //await _artifactRewardSystem.SelectAndApplyAsync(token);
                //token.ThrowIfCancellationRequested();
                //IsWaitingForArtifactSelection = false;
                
                //전장정리
                await CleanupBattleAsync(token);
                token.ThrowIfCancellationRequested();
                _waveController.ProgressQuarter();
            }
            token.ThrowIfCancellationRequested();
            ChangePhase(GamePhase.Preparation);
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

        /// <summary>임시 연출 대기. 실제 컷씬·통계창 완료를 기다리는 구현으로 교체한다.</summary>
        private UniTask PlayBattleResultAsync(ResultType result, CancellationToken token)
        {
            return _testScript.WaitForSeconds(StagingTime, token);
        }

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
        // 메서드 요청 파트
        
        /// <summary>현재는 더미 생존 수만 정리한다. 실제 스포너의 정리 완료 대기로 교체한다.
        /// 담당 요청 파트 : 유닛
        /// </summary>
        private UniTask CleanupBattleAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            _waveController.CleanupTestBattle();
            return UniTask.CompletedTask;
        }

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
    }
}
