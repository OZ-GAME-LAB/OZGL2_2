using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Core;
using Units;
using UnityEngine;

namespace Game.Core
{
    public class ArchiveManager : MonoBehaviour, ISummary
    {
        private IArtifactReader _artifactReader;
        private RuntimeUnitManager _unitManager;
        private WaveController _waveController;
        private RunStats _runStats; //현재 진행중인 정보를 저장
        private RunSummary _runSummary; //게임 종료 확정시의 정보를 저장
        private bool _isRecording = true;

        private void OnDestroy()
        {
            if (_artifactReader != null)
                _artifactReader.StackChanged -= RefreshArtifacts;

            if (_waveController != null)
                _waveController.WaveCleared -= RefreshWave;

            if (_unitManager != null)
                _unitManager.UnitDied -= RefreshUnitDied;
        }

        public void Initialize(IArtifactReader reader, RuntimeUnitManager unitManager, WaveController waveController)
        {
            _artifactReader = reader;
            _unitManager = unitManager;
            _waveController = waveController;
            _artifactReader.StackChanged += RefreshArtifacts;
            _waveController.WaveCleared += RefreshWave;
            _unitManager.UnitDied += RefreshUnitDied;
        }

        public void BeginRun()
        {
            _isRecording = true;
            _runStats = new RunStats();
            _runSummary = null;
        }
        public void CompleteRun()
        {
            _isRecording = false;
            _runSummary = new RunSummary(_runStats);
        }

        public bool TryGetRunSummary(out RunSummary summary)
        {
            summary = _runSummary;
            return summary != null;
        }
        
        private void RefreshArtifacts(ArtifactInstance instance, int post, int current)
        {
            if (!_isRecording) return;
            var rarity = instance.Data.Rarity;

            _runStats.Artifacts.TryGetValue(rarity, out int count);
            _runStats.Artifacts[rarity] = count + (current - post);
        }
        private void RefreshWave(WaveInfo info)
        {
            if (!_isRecording) return;

            if (_runStats.MaxQuarter < info.QuarterNumber)
            {
                _runStats.MaxQuarter = info.QuarterNumber;
            }
            _runStats.MaxWave = info.WaveNumber;
            _runStats.ClearWaveCount++;
            if (info.BattleType == WaveBattleType.Boss) _runStats.ClearBossCount++;
        }
        private void RefreshUnitDied(Unit_Gateway unit)
        {
            if (!_isRecording) return;

            if (unit.Team == UnitTeam.Ally)
            {
                _runStats.AllyDeathCount++;
                return;
            }
            _runStats.EnemyKillCount++;
        }
    }

    public class RunStats
    {
        public long AllyDeathCount;
        public long EnemyKillCount;
        public Dictionary<ArtifactRarity, int> Artifacts = new();
        public int ClearWaveCount;
        public int ClearBossCount;
        public int MaxQuarter;
        public int MaxWave;
    }

    public class RunSummary
    {
        public long AllyDeathCount { get; }
        public long EnemyKillCount { get; }
        public int MaxQuarter { get; }
        public int MaxWave { get; }
        public int ClearWaveCount { get; }
        public int ClearBossCount { get; }
        public IReadOnlyDictionary<ArtifactRarity, int> Artifacts { get; }

        public RunSummary(RunStats stats)
        {
            AllyDeathCount = stats.AllyDeathCount;
            EnemyKillCount = stats.EnemyKillCount;
            MaxQuarter = stats.MaxQuarter;
            MaxWave = stats.MaxWave;
            ClearWaveCount = stats.ClearWaveCount;
            ClearBossCount = stats.ClearBossCount;
            
            // 현재 기록과 독립된 복사본을 만든 뒤 읽기 전용으로 제공
            Artifacts = new ReadOnlyDictionary<ArtifactRarity, int>(
                new Dictionary<ArtifactRarity, int>(stats.Artifacts));
        }
    }
}
