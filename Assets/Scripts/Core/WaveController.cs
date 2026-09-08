using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Core
{
    public interface ISpawner
    {
        public event Action MonsterKilled;
        public UniTask SpawnAllUnits(CancellationToken cts);
        public bool GetRemain(out int enemy,out int  allies);
        public bool GetRemainBoss(out int enemy, out int allies);
    }
    public class WaveController : MonoBehaviour
    {
        public const int MAX_WAVE = 3;

        public event Action<int> WaveChanged;
        //스폰 매니저 참조필요
        private ISpawner _spawner;
        private GameFlowController _controller;

        private int _curWave;
        public int CurWave => _curWave;
        public bool IsLastWave => _curWave >= MAX_WAVE;
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

        public void BeginRun()
        {
            _curWave = 1;
            if (_spawner is TestSpawner testSpawner)
                testSpawner.ResetUnits();
            WaveChanged?.Invoke(_curWave);
        }
        public void ProgressStage()
        {
            if (IsLastWave) return;
            _curWave++;
            WaveChanged?.Invoke(_curWave);
        }
        private void HandleMonsterDead()
        {
            if (_controller == null || _controller.CurPhase != GamePhase.Battle) return;
            //스폰 매니저에게 남은 적 / 아군 수 요청
            EvaluateBattleResult();
        }
        
        private void EvaluateBattleResult()
        {
            if (!_spawner.GetRemain(out int enemy, out int allies)) return;

            if (enemy == 0)
            {

                Debug.Log("전투 승리");
                _controller.CompleteWave().Forget();
                return;
            }
            if (allies != 0) return;
            Debug.Log("전투 패배");
            _controller.FinishedGame(ResultType.Defeat).Forget();

        }
        public UniTask PrepareEnemy(CancellationToken token)
        {
            return _spawner.SpawnAllUnits(token);
        }

        public void SetSuccess()
        {
            if (_controller == null || _controller.CurPhase != GamePhase.Battle) return;
            if(_spawner is TestSpawner spawner)
            {
                spawner.setSuccess();
                spawner.invokeMonsterKilled();
            }
        }
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

    public class TestSpawner : ISpawner
    {
        public event Action MonsterKilled;
        
        private int _enemyCount = 5;
        private int _alliesCount = 5;

        public async UniTask SpawnAllUnits(CancellationToken cts)
        {
            cts.ThrowIfCancellationRequested();
            ResetUnits();
            Debug.Log("[Test] 유닛 생성중....");
            await UniTask.WaitForSeconds(3, cancellationToken: cts);
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