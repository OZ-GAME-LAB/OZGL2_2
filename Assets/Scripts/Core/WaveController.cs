using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Units;
using UnityEngine;

namespace Game.Core
{
    public struct SpawnContext
    {
        public ClassWeights Weights;
        public List<EnemyUnitType> MonsterIDs; //몬스터 id 리스트
        
        public SpawnContext(ClassWeights weights, List<EnemyUnitType> monsterIDs)
        {
            Weights = weights;
            MonsterIDs = monsterIDs;
        }
    }
    //cost = 분기 * 10 + 웨이브 * 2 / 몬스터는 소환코스트 1로고정 (MVP)
    /// <summary>
    /// 현재 전투의 스폰 준비 요청, 생존 수 조회를 통한 승패 판정, 전투 정리 요청
    /// </summary>
    public class WaveController : MonoBehaviour
    {
        // 기존 외부 참조를 위한 호환 API. 진행 상태는 GameFlow의 NodeController가 소유한다.
        public const int MAX_WAVE = NodeController.WavesPerQuarter;
        public const int MAIN_QUARTERS = NodeController.MainQuarters;
        [SerializeField] private WaveSODictionary _waveCatalog;
        public WaveSODictionary WaveCatalog => _waveCatalog;
        public WaveSO CurrentPreset => _controller?.CurrentNode?.Preset;
        public EnemyUnitFaction CurrentFaction => _controller?.CurrentNode?.Faction ?? default;
        public int CurWave => _controller?.CurrentWave ?? 0;
        public int CurQuarter => _controller?.CurrentQuarter ?? 0;
        public bool IsLastWave => _controller != null && _controller.IsLastNode;
        public event Action<WaveInfo> WaveChanged;
        public event Action<WaveInfo> WaveCleared;

        
        private ISpawnManager _spawner;
        private IRuntimeUnitManager _runtimeUnitManager;
        private GameFlowController _controller;
        private bool _isWaitingForPreparation;
        private CancellationToken _preparationToken;

        public bool CanJumpToLastWave => _controller != null && _controller.CanJumpToLastWave;
        public bool CanJumpToLastQuarter => _controller != null && _controller.CanJumpToLastQuarter;
        
        private void OnDestroy()
        {
            _isWaitingForPreparation = false;
            if (_runtimeUnitManager != null)
            {
                _runtimeUnitManager.UnitDied -= HandleMonsterDead;
                _runtimeUnitManager.PreparationCompleted -= HandleMonsterSpawnCompleted;
            }
        }
        
        // 초기화 및 수명 관리
        /// <summary>
        /// 의존성을 주입해 필요한 참조 및 이벤트 연결 실행
        /// </summary>
        public void Initialize(GameFlowController controller, ISpawnManager spawner, IRuntimeUnitManager runtimeUnitManager)
        {
            _isWaitingForPreparation = false;
            if (_runtimeUnitManager != null)
            {
                _runtimeUnitManager.UnitDied -= HandleMonsterDead;
                _runtimeUnitManager.PreparationCompleted -= HandleMonsterSpawnCompleted;
            }
            _controller = controller;
            _spawner = spawner;
            _runtimeUnitManager = runtimeUnitManager;
            _runtimeUnitManager.UnitDied += HandleMonsterDead;
            _runtimeUnitManager.PreparationCompleted += HandleMonsterSpawnCompleted;
        }

        // 전투 준비 및 승패 판정
        public UniTask PrepareEnemy(CancellationToken runToken, CancellationToken spawnToken)
        {
            runToken.ThrowIfCancellationRequested();
            _preparationToken = runToken;
            _isWaitingForPreparation = true;
            SpawnContext context = new(
                weights:CurrentPreset.Weights, 
                monsterIDs:CurrentPreset.MonsterIDs);
            
            return _spawner.SpawnEnemyWaveAsync(10, context, spawnToken);
        }
        internal void NotifyWaveCleared(WaveInfo info)
        {
            WaveCleared?.Invoke(info);
        }
        /// <summary>
        /// 유닛 파트에서 발행하는 몬스터 사망 이벤트와 연결해 클리어 조건을 확인
        /// </summary>
        private void HandleMonsterDead(Unit_Gateway unit)
        {
            if (_controller == null || _controller.CurPhase != GamePhase.Battle) return;
            //스폰 매니저에게 남은 적 / 아군 수 요청
            EvaluateBattleResult();
        }

        private void HandleMonsterSpawnCompleted()
        {
            if (!_isWaitingForPreparation || _preparationToken.IsCancellationRequested ||
                _controller == null || _controller.CurPhase != GamePhase.BattlePreparing) return;
            _isWaitingForPreparation = false;
            _controller.TryStartWave();
        }

        public void BattleStart()
        {
            _runtimeUnitManager.StartBattlePhase();
        }

        public void BattlePause()
        {
            _runtimeUnitManager.PauseBattle();
        }
        /// <summary>
        /// 전투 결과를 확인하고 승/패를 판정한다.
        /// </summary>
        private void EvaluateBattleResult()
        {
            if (!_runtimeUnitManager.GetRemain(out int enemy, out int allies)) return;

            if (enemy == 0)
            {
                Debug.Log("전투 승리");
                _controller.ResolveBattleAsync(ResultType.Victory).Forget();
                return;
            }
            if (allies != 0) return;
            Debug.Log("전투 패배");
            _controller.ResolveBattleAsync(ResultType.Defeat).Forget();
        }

        public void CleanupBattle()
        {
            // 그룹 제거 도중 발생하는 준비 완료 알림으로 전투에 재진입하지 않는다.
            _isWaitingForPreparation = false;
            _runtimeUnitManager.ClearRuntime();
        }

        // 진행 변경 알림
        internal void NotifyNodeChanged()
        {
            WaveInfo info = new WaveInfo(
                quarterNumber:CurQuarter, 
                waveNumber:CurWave, 
                battleType:CurrentPreset.BattleType,
                postBattleEvent:_controller.CurrentNode.PostBattleEvent);
            
            WaveChanged?.Invoke(info);
        }

        // 테스트·호환용 진행 입력 및 전투 조작
        /// <summary>
        /// 게임 시작 시 실제 초기화 및 기초세팅 시작
        /// </summary>
        public void BeginRun() => _controller?.BeginRun();

        public void ProgressStage() => _controller?.RequestProgressStage();

        public void ProgressQuarter() => _controller?.RequestProgressQuarter();

        public void JumpToLastWaveForTest() => _controller?.JumpToLastWaveForTest();

        public void JumpToLastQuarterForTest() => _controller?.JumpToLastQuarterForTest();

        /// <summary>
        /// 기존 테스트 버튼의 연결을 유지하며 실제 결과 처리 경로에 승리를 요청한다.
        /// </summary>
        public void SetSuccess()
        {
            if (_controller == null || _controller.CurPhase != GamePhase.Battle) return;
            _controller.ResolveBattleAsync(ResultType.Victory).Forget();
        }

        /// <summary>
        /// 기존 테스트 버튼의 연결을 유지하며 실제 결과 처리 경로에 패배를 요청한다.
        /// </summary>
        public void SetFail()
        {
            if (_controller == null || _controller.CurPhase != GamePhase.Battle) return;
            _controller.ResolveBattleAsync(ResultType.Defeat).Forget();
        }
    }
}
