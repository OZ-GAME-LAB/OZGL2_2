using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Core
{
    public enum GamePhase
    {
        None,
        Preparation, //준비 페이즈
        BattlePreparing, //전투 시작전 연출 / 전투준비
        Battle, //실제 전투 페이즈
        Reward, //보상 페이즈
        Finished //전투 승리 / 패배 결과페이즈
    }

    public enum ResultType
    {
        Victory ,
        Defeat
    }
    public class GameFlowController : MonoBehaviour
    {
        public event Action<GamePhase> PhaseChanged;
        public GamePhase CurPhase => _curPhase;

        private TestWaitingScript _testScript;
        private WaveController _waveController;
        private GamePhase _curPhase;
        private bool _isTransitioning; //중복변경요청 제한
        private CancellationTokenSource _cts;

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        public void Initialize(WaveController waveController, TestWaitingScript testScript)
        {
            _testScript = testScript;
            _waveController = waveController;

            ClearToken();
            ChangePhase(GamePhase.None);
            _isTransitioning = false;
        }
        public void BeginRun()
        {
            if (_isTransitioning) return;
            if (_curPhase != GamePhase.None && _curPhase != GamePhase.Finished) return;
            Debug.Log("[Core/GameFlowController] 준비 상태 진입");
            _isTransitioning = true;
            _waveController.BeginRun();
            // 웨이브 정보를 확정한 뒤 준비 상태를 알린다.

            ChangePhase(GamePhase.Preparation);
            _isTransitioning = false;
        }

        public void ResetRun()
        {
            ClearToken();
            _isTransitioning = false;
            ChangePhase(GamePhase.None);
            BeginRun();
        }

        public bool CanEnterBuildMode()
        {
            return !_isTransitioning && _curPhase == GamePhase.Preparation;
        }

        public async UniTask<bool> TryStartWave()
        {
            if (_isTransitioning || _curPhase != GamePhase.Preparation) return false;
            _isTransitioning = true;
            var token = _cts.Token;
            ChangePhase(GamePhase.BattlePreparing);
            Debug.Log("[Core/GameFlowController] 전투 준비상태 돌입");
            bool canceled = await _waveController.PrepareEnemy(token).
                SuppressCancellationThrow();
            // 연결 예정: 도현님 — 생산 건물의 아군 생산
            // 적 준비는 WaveController를 통해 완료까지 기다린다.
            // 양쪽 준비 완료 후 전투 상태로 전환
            if (canceled || token.IsCancellationRequested)
                return false;
            ChangePhase(GamePhase.Battle);

            _isTransitioning = false;
            return true;
        }

        public async UniTask CompleteWave()
        {
            if (_isTransitioning || _curPhase != GamePhase.Battle) return;
            _isTransitioning = true;
            var token = _cts.Token;
            ChangePhase(GamePhase.Reward);
            // 정산 상태로 전환
            // 연결 예정: 재희님 — 웨이브 보상 지급
            // 연결 예정: 원준님 — 남은 유닛 제거(MVP)
            // 다음 웨이브 준비 상태로 전환
            bool canceled = await _testScript
                .WaitToggle(token)
                .SuppressCancellationThrow();

            if (canceled || token.IsCancellationRequested)
                return;
            if (_waveController.IsLastWave)
            {
                FinishRun(ResultType.Victory);
                return;
            }
            _waveController.ProgressStage();
            ChangePhase(GamePhase.Preparation);
            _isTransitioning = false;
        }

        /// <summary>
        /// 게임 종료 후(패배 / 승리 관계없이) 결산창
        /// </summary>
        public UniTask FinishedGame(ResultType type)
        {
            if (_isTransitioning || _curPhase != GamePhase.Battle)
                return UniTask.CompletedTask;

            // 승리는 마지막 웨이브에서도 보상 선택을 먼저 기다린다.
            if (type == ResultType.Victory)
                return CompleteWave();

            _isTransitioning = true;
            FinishRun(type);
            return UniTask.CompletedTask;
        }

        private void FinishRun(ResultType type)
        {
            Debug.Log($"[Core/GameFlowController] 게임 종료 : {type}");
            ChangePhase(GamePhase.Finished);
            _isTransitioning = false;
        }
        private void ChangePhase(GamePhase targetPhase)
        {
            Debug.Log($"[Core/GameFlowController] 페이즈 변경 : {_curPhase} -> {targetPhase}");
            _curPhase = targetPhase;
            PhaseChanged?.Invoke(targetPhase);
        }

        private void ClearToken()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }
    }
}