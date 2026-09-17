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
        [Header("아군 생성 없는 테스트")]
        [Tooltip("아군 생성 요청이 없는 테스트에서만 사용합니다. 실제 아군 연동 시 해제하세요.")]
        [SerializeField] private bool _completeEmptyAllySpawnForTest = true;
        internal WaveSODictionary WaveCatalog => _waveCatalog;
        public WaveSO CurrentPreset => _controller?.CurrentNode?.Preset;
        public EnemyUnitFaction CurrentFaction => _controller?.CurrentNode?.Faction ?? default;
        public int CurWave => _controller?.CurrentWave ?? 0;
        public int CurQuarter => _controller?.CurrentQuarter ?? 0;
        public bool IsLastWave => _controller != null && _controller.IsLastNode;
        public event Action<WaveChangedInfo> WaveChanged;
        private ISpawnManager _spawner;
        private IRuntimeUnitManager _runtimeUnitManager;
        private GameFlowController _controller;
        private bool _isWaitingForPreparation;
        private CancellationToken _preparationToken;

        public bool CanJumpToLastWave => _controller != null && _controller.CanJumpToLastWave;
        public bool CanJumpToLastQuarter => _controller != null && _controller.CanJumpToLastQuarter;

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

        private void OnDestroy()
        {
            _isWaitingForPreparation = false;
            if (_runtimeUnitManager != null)
            {
                _runtimeUnitManager.UnitDied -= HandleMonsterDead;
                _runtimeUnitManager.PreparationCompleted -= HandleMonsterSpawnCompleted;
            }
        }

        // 전투 준비 및 승패 판정
        public UniTask PrepareEnemy(float time, CancellationToken runToken, CancellationToken spawnToken)
        {
            runToken.ThrowIfCancellationRequested();
            _preparationToken = runToken;
            _isWaitingForPreparation = true;
            CompleteEmptyAllySpawnForTest();
            runToken.ThrowIfCancellationRequested();
            SpawnContext context = new(
                weights:CurrentPreset.Weights, 
                monsterIDs:CurrentPreset.MonsterIDs);
            
            return _spawner.SpawnEnemyWaveAsync(10, context, spawnToken);
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

        // 진행 변경 알림
        internal void NotifyNodeChanged()
        {
            WaveChangedInfo info = new WaveChangedInfo(
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

        internal void ResetTestBattle()
        {
            if (_spawner is TestSpawner testSpawner)
                testSpawner.ResetUnits();
        }

        public void CleanupBattle()
        {
            // 그룹 제거 도중 발생하는 준비 완료 알림으로 전투에 재진입하지 않는다.
            _isWaitingForPreparation = false;
            _runtimeUnitManager.ClearRuntime();
        }

        /// <summary>
        /// 스테이지 성공 판정을 위해 임시로 만든 메서드.
        /// <br/> 강제로 승리 설정으로 바꾼다.
        /// </summary>
        public void SetSuccess()
        {
            if (_controller == null || _controller.CurPhase != GamePhase.Battle) return;
            if(_spawner is TestSpawner spawner)
            {
                spawner.setSuccess();
                spawner.invokeMonsterKilled();
            }
        }

        /// <summary>
        /// 스테이지 성공 판정을 위해 임시로 만든 메서드.
        /// <br/> 강제로 승리 설정으로 바꾼다.
        /// </summary>
        public void SetFail()
        {
            if (_controller == null || _controller.CurPhase != GamePhase.Battle) return;
            if(_spawner is TestSpawner spawner)
            {
                spawner.setFail();
                spawner.invokeMonsterKilled();
            }
        }

        /// <summary>아군을 생성하지 않는 테스트에서만 생성 완료 조건을 충족한다.</summary>
        private void CompleteEmptyAllySpawnForTest()
        {
            if (_completeEmptyAllySpawnForTest && _runtimeUnitManager is RuntimeUnitManager runtime)
                runtime.NotifyAllySpawnCompleted();
        }
    }
    /// <summary>
    /// 스테이지 내 몬스터 구현을 위해 임시로 만든 테스트클래스
    /// </summary>
    public class TestSpawner : ISpawnManager, IRuntimeUnitManager
    {
        public event Action MonsterKilled;

        private int _enemyCount = 5;
        private int _alliesCount = 5;


        public event Action PreparationCompleted;
        public event Action<Unit_Gateway> UnitDied;
        public event Action<UnitTeam> TeamWiped;
        public int AllyUnitCount { get; }
        public int EnemyUnitCount { get; }

        public bool GetRemain(out int enemy, out int allies)
        {
            enemy = _enemyCount;
            allies = _alliesCount;
            if(_enemyCount == 0 || _alliesCount == 0)
                return true;
            return false;
        }

        public void StartBattlePhase()
        {
            throw new NotImplementedException();
        }

        public void ClearRuntime()
        {
            ClearUnits();
        }

        public bool GetRemainBoss(out int enemy, out int allies)
        {
            enemy = _enemyCount;
            allies = _alliesCount;
            if(_enemyCount == 0 || _alliesCount == 0)
                return true;
            return false;
        }

        public void ClearUnits()
        {
            _enemyCount = 0;
            _alliesCount = 0;
        }

        public void ResetUnits()
        {
            _enemyCount = 5;
            _alliesCount = 5;
        }

        public void setFail()
        {
            _alliesCount = 0;
        }

        public void setSuccess()
        {
            _enemyCount = 0;
        }

        public void invokeMonsterKilled() => MonsterKilled?.Invoke();
        public event Action AllySpawnCompleted;
        public event Action EnemySpawnCompleted;
        public void SpawnAllyGroup(AllyUnitType unitType, Vector2 spawnPosition, int count, Vector2 rallyPoint)
        {
            Debug.Log("[Test] SpawnAllyGroup 미구현....");
        }

        public async UniTask SpawnEnemyWaveAsync(int cost, SpawnContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ResetUnits();
            Debug.Log("[Test] 유닛 생성중....");
            await UniTask.WaitForSeconds(0.5f, cancellationToken: cancellationToken);
            Debug.Log("[Test] 유닛 생성완료!");
        }

        public bool TryGetAllyPrefab(AllyUnitType unitType, out GameObject prefab)
        {
            throw new NotImplementedException();
        }

        public bool TryGetEnemyPrefab(EnemyUnitType unitType, out GameObject prefab)
        {
            throw new NotImplementedException();
        }
    }
}
