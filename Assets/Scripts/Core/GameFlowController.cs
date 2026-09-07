using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.VisualScripting;
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

    public enum FailType
    {
        Changing, //변경중
        Fail //특정 원인으로 실패
    }
    public class GameFlowController : MonoBehaviour
    {
        public event Action<GamePhase> PhaseChanged;
        public GamePhase CurPhase => _curPhase;

        [SerializeField] private TestWaitingScript _testScript;
        private GamePhase _curPhase;
        private bool _isTransitioning; //중복변경요청 제한
        private CancellationTokenSource _cts;

        void Start()
        {
            _cts = new CancellationTokenSource();
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        public void Initialize()
        {
            ChangePhase(GamePhase.None);
            _isTransitioning = false;
            ClearToken();
            BeginRun(); //테스트용으로, 추후 삭제할것
        }
        public void BeginRun()
        {
            if (_isTransitioning) return;
            if (_curPhase != GamePhase.None && _curPhase != GamePhase.Finished) return;
            Debug.Log("[Core/GameFlowController] 준비 상태 진입");
            _isTransitioning = true;
            // 첫 웨이브 정보 설정
            // 준비 상태로 전환

            ChangePhase(GamePhase.Preparation);
            _isTransitioning = false;
        }

        public bool CanEnterBuildMode()
        {
            return !_isTransitioning && _curPhase == GamePhase.Preparation;
        }

        public async UniTask<bool> TryStartWave()
        {
            if (_isTransitioning || _curPhase != GamePhase.Preparation) return false;
            _isTransitioning = true;
            ChangePhase(GamePhase.BattlePreparing);
            Debug.Log("[Core/GameFlowController] 전투 준비상태 돌입");
            bool canceled = await _testScript.
                WaitForSeconds(_cts.Token).
                SuppressCancellationThrow();
            // 준비 상태가 아니면 false 반환
            // 전투 준비 상태로 전환
            // 연결 예정: 도현님 — 생산 건물의 아군 생산
            // 연결 예정: 내 웨이브 파트 — 적 생성
            // 양쪽 준비 완료 후 전투 상태로 전환
            if (canceled)
                return false;
            ChangePhase(GamePhase.Battle);
            _isTransitioning = false;
            return true;
        }
        
        public async UniTask CompleteWave()
        {
            if (_isTransitioning || _curPhase != GamePhase.Battle) return;
            _isTransitioning = true;
            ChangePhase(GamePhase.Reward);
            // 정산 상태로 전환
            // 연결 예정: 재희님 — 웨이브 보상 지급
            // 연결 예정: 원준님 — 합의한 규칙에 따른 전투 유닛 정리
            // 다음 웨이브 준비 상태로 전환
            bool canceled = await _testScript
                .WaitToggle(_cts.Token)
                .SuppressCancellationThrow();

            if (canceled)
                return;
            ChangePhase(GamePhase.Preparation);
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