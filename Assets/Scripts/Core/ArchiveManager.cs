using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Core;
using Units;
using UnityEngine;

namespace Game.Core
{
    [Serializable]
    public class ArtifactCountData
    {
        public ArtifactRarity Rarity;
        public int Count;

        public ArtifactCountData(ArtifactRarity rarity, int count)
        {
            Rarity = rarity;
            Count = count;
        }
    }
    [Serializable]
    public sealed class ArchiveSaveData
    {
        public long AllyDeathCount;
        public long EnemyKillCount;
        public List<ArtifactCountData> Artifacts = new List<ArtifactCountData>();
        public int ClearWaveCount;
        public int ClearBossCount;
        public int MaxQuarter;
        public int MaxWave;

        public ArchiveSaveData(RunStats stat)
        {
            AllyDeathCount = stat.AllyDeathCount;
            EnemyKillCount = stat.EnemyKillCount;
            ClearWaveCount = stat.ClearWaveCount;
            ClearBossCount = stat.ClearBossCount;
            MaxQuarter = stat.MaxQuarter;
            MaxWave = stat.MaxWave;
            foreach (var artifact in stat.Artifacts)
            {
                Artifacts.Add(new ArtifactCountData(artifact.Key, artifact.Value));
            }
        }
    }
    
    public class ArchiveManager : MonoBehaviour, ISummary, ISaveDataProvider<ArchiveSaveData>
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

        public void NewGame()
        {
            _isRecording = true;
            _runStats = new RunStats();
            _runSummary = null;
        }

        public void Continue()
        {
            
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

        public ArchiveSaveData CaptureSaveData()
        {
            return new ArchiveSaveData(_runStats);
        }

        public void RestoreSaveData(ArchiveSaveData data)
        {
            RunStats stat = new RunStats();
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (data.Artifacts == null)
                throw new ArgumentException("유물 기록 목록이 없습니다.", nameof(data));

            stat.AllyDeathCount = data.AllyDeathCount;
            stat.EnemyKillCount = data.EnemyKillCount;
            stat.ClearWaveCount = data.ClearWaveCount;
            stat.ClearBossCount = data.ClearBossCount;
            stat.MaxQuarter = data.MaxQuarter;
            stat.MaxWave = data.MaxWave;
            foreach (var artifact in data.Artifacts)
            {
                _runStats.Artifacts.Add(artifact.Rarity, artifact.Count);
            }

            _runStats = stat;
            _isRecording = true;
            _runSummary = null;
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
        public RunStats(){ }
        public RunStats(ArchiveSaveData saveData)
        {
            
        }
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
