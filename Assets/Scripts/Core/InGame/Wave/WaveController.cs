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
        // General-unit candidates; their counts are determined by cost and weights.
        public List<EnemyUnitType> MonsterIDs;
        // One boss per entry. Duplicates are intentional; null/empty means no bosses.
        public List<EnemyUnitType> BossIDs;

        public SpawnContext(ClassWeights weights, List<EnemyUnitType> monsterIDs)
            : this(weights, monsterIDs, null) { }

        public SpawnContext(ClassWeights weights, List<EnemyUnitType> monsterIDs,
            List<EnemyUnitType> bossIDs)
        {
            Weights = weights;
            MonsterIDs = monsterIDs;
            BossIDs = bossIDs;
        }
    }

    public enum QuarterType
    {
        Regular, Infinity
    }

    /// <summary>
    /// 현재 전투의 스폰 준비 요청, 생존 수 조회를 통한 승패 판정, 전투 정리 요청
    /// </summary>
    public class WaveController : MonoBehaviour
    {
        // 기존 외부 참조를 위한 호환 API. 진행 상태는 GameFlow의 NodeController가 소유한다.
        public const int MAX_WAVE = NodeController.WavesPerQuarter;
        public const int MAIN_QUARTERS = NodeController.MainQuarters;
        private const int InitialSpawnCost = 5;
        private const int SpawnCostPerWave = 2;
        private const float InfinityBonusPerWave = 0.02f;
        private const float InfinityBonusPerQuarter = 0.15f;
        private const float EliteBonus = 0.20f;
        private readonly object _eliteModifierSource = new();
        
        [SerializeField] private WaveSODictionary _waveCatalog;
        public WaveSODictionary WaveCatalog => _waveCatalog;
        public WaveSO CurrentPreset => _controller?.CurrentNode?.Preset;
        public EnemyUnitFaction CurrentFaction => _controller?.CurrentNode?.Faction ?? default;
        public int CurWave => _controller?.CurrentWave ?? 0;
        public int CurQuarter => _controller?.CurrentQuarter ?? 0;
        public bool IsLastWave => _controller != null && _controller.IsLastNode;
        public event Action<WaveInfo> WaveChanged;
        public event Action<WaveInfo> WaveCleared;
        public event Action BattleStarted;

        
        private ISpawnManager _spawner;
        private IRuntimeUnitManager _runtimeUnitManager;
        private GameFlowController _controller;
        private TotemFlowEffects _totemFlowEffects;
        private IUnitStatModifierRegister _unitStatModifiers;
        private readonly object _infinityModifierSource = new();
        private bool _isWaitingForPreparation;
        private CancellationToken _preparationToken;

        public bool CanJumpToLastWave => _controller != null && _controller.CanJumpToLastWave;
        public bool CanJumpToLastQuarter => _controller != null && _controller.CanJumpToLastQuarter;
        
        private void OnDestroy()
        {
            _isWaitingForPreparation = false;
            _unitStatModifiers?.RemoveModifiersBySource(_infinityModifierSource);
            _unitStatModifiers?.RemoveModifiersBySource(_eliteModifierSource);
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
        public void Initialize(GameFlowController controller, ISpawnManager spawner,
            IRuntimeUnitManager runtimeUnitManager, TotemFlowEffects totemFlowEffects,
            IUnitStatModifierRegister unitStatModifiers)
        {
            if (unitStatModifiers == null) throw new ArgumentNullException(nameof(unitStatModifiers));
            _isWaitingForPreparation = false;
            _unitStatModifiers?.RemoveModifiersBySource(_infinityModifierSource);
            _unitStatModifiers?.RemoveModifiersBySource(_eliteModifierSource);
            if (_runtimeUnitManager != null)
            {
                _runtimeUnitManager.UnitDied -= HandleMonsterDead;
                _runtimeUnitManager.PreparationCompleted -= HandleMonsterSpawnCompleted;
            }
            _controller = controller;
            _spawner = spawner;
            _runtimeUnitManager = runtimeUnitManager;
            _totemFlowEffects = totemFlowEffects;
            _unitStatModifiers = unitStatModifiers;
            _runtimeUnitManager.UnitDied += HandleMonsterDead;
            _runtimeUnitManager.PreparationCompleted += HandleMonsterSpawnCompleted;
        }

        // 전투 준비 및 승패 판정
        public UniTask PrepareEnemy(CancellationToken runToken, CancellationToken spawnToken)
        {
            runToken.ThrowIfCancellationRequested();
            spawnToken.ThrowIfCancellationRequested();
            _preparationToken = runToken;
            _isWaitingForPreparation = true;
            SpawnContext context = new(
                weights:CurrentPreset.Weights, 
                monsterIDs:CurrentPreset.MonsterIDs,
                bossIDs:CurrentPreset.BattleType == WaveBattleType.Boss ? CurrentPreset.BossIDs : null);
            RefreshInfinityModifiers();
            RefreshEliteModifiers();
            int cost = CalculateSpawnCost(CurQuarter, CurWave);
            int enemyCount = _totemFlowEffects.CalculateEnemyCount(cost);
            return _spawner.SpawnEnemyWaveAsync(enemyCount, context, spawnToken);
        }

        private static int CalculateSpawnCost(int quarter, int wave)
        {
            // 무한 모드는 일반 모드의 마지막 웨이브 코스트를 유지한다.
            int waveIndex = quarter > MAIN_QUARTERS
                ? MAIN_QUARTERS * MAX_WAVE - 1
                : (quarter - 1) * MAX_WAVE + wave - 1;
            return InitialSpawnCost + waveIndex * SpawnCostPerWave;
        }
        
        private void RefreshInfinityModifiers()
        {
            RefreshCombatModifiers(_infinityModifierSource,
                CalculateInfinityBonus(CurQuarter, CurWave));
        }
        private static float CalculateInfinityBonus(int quarter, int wave)
        {
            int infinityQuarter = quarter - MAIN_QUARTERS;
            if (infinityQuarter <= 0) return 0f;

            // 첫 무한 분기는 2~10%, 이후 분기 시작값은 15%, 30%, 45%...이다.
            float quarterStartBonus = infinityQuarter == 1
                ? InfinityBonusPerWave
                : (infinityQuarter - 1) * InfinityBonusPerQuarter;
            return quarterStartBonus + (wave - 1) * InfinityBonusPerWave;
        }
        /// <summary> 현재 전투가 엘리트전투면 배율 적용, 아니면 미적용</summary>
        private void RefreshEliteModifiers()
        {
            float bonus = _controller.CurrentNode.IsElite
                ? EliteBonus
                : 0f;

            RefreshCombatModifiers(_eliteModifierSource, bonus);
        }
        
        private void RefreshCombatModifiers(object source, float bonus)
        {
            _unitStatModifiers.RemoveModifiersBySource(source);
            if (bonus <= 0f) return;

            AddCombatModifier(source, UnitStatType.MaxHp, bonus);
            AddCombatModifier(source, UnitStatType.AttackPower, bonus);
        }

        private void AddCombatModifier(object source, UnitStatType statType, float bonus)
        {
            _unitStatModifiers.AddEnemyModifier(new EnemyStatModifier(
                source: source,
                applyType: UnitModifierApplyType.All,
                targetClass: default,
                targetUnitType: default,
                statType: statType,
                modifierType: UnitStatModifierType.Percent,
                value: bonus));
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
            BattleStarted?.Invoke();
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
