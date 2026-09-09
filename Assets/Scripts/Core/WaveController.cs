using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Core
{
    public interface ISpawner
    {
        public event Action MonsterKilled;
        public UniTask SpawnAllUnits(float time, CancellationToken cts);
        public bool GetRemain(out int enemy,out int  allies);
        public bool GetRemainBoss(out int enemy, out int allies);
    }
    public class WaveController : MonoBehaviour
    {
        public const int MAX_WAVE = 3; // 분기당 웨이브 수
        public const int MAIN_QUARTERS = 3;

        [SerializeField] private WaveSODictionary _waveCatalog;
        public WaveSO CurrentPreset { get; private set; }
        public EnemyFaction CurrentFaction { get; private set; }
        public event Action<int> WaveChanged;
        //스폰 매니저 참조필요
        private ISpawner _spawner;
        private GameFlowController _controller;

        public int CurWave => _curWave;
        private int _curWave;
        public int CurQuarter { get; private set; }
        public bool IsLastWave => _curWave >= MAX_WAVE;
        /// <summary>
        /// 의존성을 주입해 필요한 참조 및 이벤트 연결 실행
        /// </summary>
        public void Initialize(GameFlowController controller, ISpawner spawner)
        {
            if (_spawner != null)
                _spawner.MonsterKilled -= HandleMonsterDead;
            _controller = controller;
            _spawner = spawner;
            _spawner.MonsterKilled += HandleMonsterDead;
        }

        private void OnDestroy()
        {
            if (_spawner != null)
                _spawner.MonsterKilled -= HandleMonsterDead;
        }
        /// <summary>
        /// 게임 시작 시 실제 초기화 및 기초세팅 시작
        /// </summary>
        public void BeginRun()
        {
            CurQuarter = 1;
            _curWave = 1;
            if (_spawner is TestSpawner testSpawner)
                testSpawner.ResetUnits();
            SelectCurrentPreset();
            WaveChanged?.Invoke(_curWave);
        }
        /// <summary>
        /// 스테이지 진행
        /// </summary>
        public void ProgressStage()
        {
            if (IsLastWave) return;
            _curWave++;
            SelectCurrentPreset();
            WaveChanged?.Invoke(_curWave);
        }
        /// <summary>
        /// 유닛 파트에서 발행하는 몬스터 사망 이벤트와 연결해 클리어 조건을 확인
        /// </summary>
        private void HandleMonsterDead()
        {
            if (_controller == null || _controller.CurPhase != GamePhase.Battle) return;
            //스폰 매니저에게 남은 적 / 아군 수 요청
            EvaluateBattleResult();
        }
        /// <summary>
        /// 전투 결과를 확인하고 승/패를 판정한다.
        /// </summary>
        private void EvaluateBattleResult()
        {
            if (!_spawner.GetRemain(out int enemy, out int allies)) return;

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
        public void ProgressQuarter()
        {
            if (!IsLastWave) return;
            CurQuarter++;
            _curWave = 1;
            SelectCurrentPreset();
            WaveChanged?.Invoke(_curWave);
        }

        private void SelectCurrentPreset()
        {
            CurrentPreset = null;
            CurrentFaction = default;
            WaveBattleType type = _curWave == MAX_WAVE ? WaveBattleType.Boss :
                _curWave == 2 ? WaveBattleType.Elite : WaveBattleType.Normal;
            // 테스트 무한 진행은 5분기의 출현 규칙을 재사용한다.
            if (_waveCatalog == null ||
                !_waveCatalog.TryGetRandomWaveSO(Math.Min(CurQuarter, 5), type,
                    out var preset, out var faction))
            {
                Debug.LogError($"[WaveController] 프리셋 목록 참조 또는 후보 누락: 분기 {CurQuarter}, 타입 {type}", this);
                return;
            }
            CurrentPreset = preset;
            CurrentFaction = faction;
        }

        public void CleanupTestBattle()
        {
            if (_spawner is TestSpawner testSpawner)
                testSpawner.ClearUnits();
        }

        public UniTask PrepareEnemy(float time, CancellationToken token)
        {
            return _spawner.SpawnAllUnits(time, token);
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
    }
    /// <summary>
    /// 스테이지 내 몬스터 구현을 위해 임시로 만든 테스트클래스
    /// </summary>
    public class TestSpawner : ISpawner
    {
        public event Action MonsterKilled;

        private int _enemyCount = 5;
        private int _alliesCount = 5;

        public async UniTask SpawnAllUnits(float time,CancellationToken cts)
        {
            cts.ThrowIfCancellationRequested();
            ResetUnits();
            Debug.Log("[Test] 유닛 생성중....");
            await UniTask.WaitForSeconds(time, cancellationToken: cts);
            Debug.Log("[Test] 유닛 생성완료!");
        }

        public bool GetRemain(out int enemy, out int allies)
        {
            enemy = _enemyCount;
            allies = _alliesCount;
            if(_enemyCount == 0 || _alliesCount == 0)
                return true;
            return false;
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
    }
}