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
        public int ProgressVersion;
        public PlayerRecordSaveData Records;
        public double CurrentWaveDamage;

        public ArchiveSaveData(RunStats stat)
        {
            AllyDeathCount = stat.AllyDeathCount;
            EnemyKillCount = stat.EnemyKillCount;
            ClearWaveCount = stat.ClearWaveCount;
            ClearBossCount = stat.ClearBossCount;
            MaxQuarter = stat.MaxQuarter;
            MaxWave = stat.MaxWave;
            ProgressVersion = 1;
            Records = stat.CaptureRecords();
            CurrentWaveDamage = stat.CurrentWaveDamage;
            foreach (var artifact in stat.Artifacts)
            {
                Artifacts.Add(new ArtifactCountData(artifact.Key, artifact.Value));
            }
        }
    }
    
    // 이번 런의 기록을 수집하고 종료 사본을 제공합니다. 파일 저장은 SaveCoordinator가 담당합니다.
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Game.Core", sourceAssembly: "Assembly-CSharp", sourceClassName: "ArchiveManager")]
    public class RunRecordManager : MonoBehaviour, ISummary, ISaveDataProvider<ArchiveSaveData>
    {
        private IArtifactReader _artifactReader;
        private TotemRunApplier _totemReader;
        private AltarManager _altarReader;
        private TraitRunApplier _traitReader;
        private RuntimeUnitManager _unitManager;
        private WaveController _waveController;
        private RunStats _runStats; //현재 진행중인 정보를 저장
        private RunSummary _runSummary; //게임 종료 확정시의 정보를 저장
        private bool _isRecording = true;

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (_artifactReader != null)
                _artifactReader.StackChanged -= RefreshArtifacts;

            if (_waveController != null)
            {
                _waveController.WaveCleared -= RefreshWave;
                _waveController.BattleStarted -= BeginWave;
            }

            if (_unitManager != null)
            {
                _unitManager.UnitDied -= RefreshUnitDied;
                _unitManager.DamageApplied -= RecordDamage;
            }
        }

        public void Initialize(IArtifactReader reader, RuntimeUnitManager unitManager, WaveController waveController,
            TotemRunApplier totemReader, AltarManager altarReader = null, TraitRunApplier traitReader = null)
        {
            Unsubscribe();
            _artifactReader = reader;
            _totemReader = totemReader;
            _altarReader = altarReader;
            _traitReader = traitReader;
            _unitManager = unitManager;
            _waveController = waveController;
            _artifactReader.StackChanged += RefreshArtifacts;
            _waveController.WaveCleared += RefreshWave;
            _waveController.BattleStarted += BeginWave;
            _unitManager.UnitDied += RefreshUnitDied;
            _unitManager.DamageApplied += RecordDamage;

            _isRecording = true;
            _runStats = new RunStats();
            _runSummary = null;
        }

        public void CompleteRun()
        {
            if (_runSummary != null) return;
            _isRecording = false;

            IReadOnlyList<ActiveTotem> totems = _totemReader.ActiveTotems;
            _runSummary = new RunSummary(_runStats, totems, _altarReader?.Selected,
                _traitReader?.CaptureLevels(), _artifactReader.Instances);
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
            _runStats.ClearWaveCount = checked(_runStats.ClearWaveCount + 1);
            if (info.BattleType == WaveBattleType.Boss)
            {
                _runStats.ClearBossCount = checked(_runStats.ClearBossCount + 1);
                if (!_runStats.Records.ClearedBossQuarters.Contains(info.QuarterNumber))
                    _runStats.Records.ClearedBossQuarters.Add(info.QuarterNumber);
            }
        }
        private void RefreshUnitDied(Unit_Gateway unit)
        {
            if (!_isRecording) return;

            if (unit.Team == UnitTeam.Ally)
            {
                _runStats.AllyDeathCount = checked(_runStats.AllyDeathCount + 1);
                return;
            }
            _runStats.EnemyKillCount = checked(_runStats.EnemyKillCount + 1);
            // Died 시점에는 LastDeath가 이미 확정되어 있습니다. 뒤의 DeathConfirmed는
            // 런타임 목록에서 제거된 뒤 발행되므로 여기서 확정 원인의 스냅샷을 읽습니다.
            CombatDeathResult death = unit.LastDeath;
            if (death != null && death.Killer.Target != null && death.Killer.Team == UnitTeam.Ally &&
                death.Victim.Team == UnitTeam.Enemy && death.SourceType == DamageSourceType.Skill)
                _runStats.Records.SkillKills = checked(_runStats.Records.SkillKills + 1);
        }

        private void BeginWave() { if (_isRecording) _runStats.CurrentWaveDamage = 0; }

        private void RecordDamage(CombatApplicationResult damage)
        {
            if (!_isRecording || damage.Metadata.Owner.Target == null || damage.Metadata.Owner.Team != UnitTeam.Ally ||
                damage.Target.Team != UnitTeam.Enemy) return;
            double amount = (double)damage.HpDamage + damage.ShieldAbsorbed;
            PlayerRecordSaveData.ValidateDamage(amount);
            _runStats.Records.TotalDamage += amount;
            _runStats.CurrentWaveDamage += amount;
            _runStats.Records.HighestHitDamage = Math.Max(_runStats.Records.HighestHitDamage, amount);
            _runStats.Records.HighestWaveDamage = Math.Max(_runStats.Records.HighestWaveDamage, _runStats.CurrentWaveDamage);
        }

        public ArchiveSaveData CaptureSaveData()
        {
            _runStats.CaptureRecords().Validate();
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
            if (data.ProgressVersion == 0)
            {
                // 도입 이전 기록에는 피해량·스킬 귀속이 없으므로 추정하지 않습니다.
                stat.Records = PlayerRecordSaveData.CreateEmpty();
            }
            else if (data.ProgressVersion == 1 && data.Records != null)
            {
                data.Records.Validate();
                if (data.Records.EnemyKills != data.EnemyKillCount || data.Records.AllyDeaths != data.AllyDeathCount ||
                    data.Records.ClearedWaves != data.ClearWaveCount || data.Records.ClearedBosses != data.ClearBossCount)
                    throw new ArgumentException("런 기록 집계가 일치하지 않습니다.");
                stat.Records = data.Records.Copy();
                PlayerRecordSaveData.ValidateDamage(data.CurrentWaveDamage);
                if (data.CurrentWaveDamage > data.Records.HighestWaveDamage)
                    throw new ArgumentException("현재 웨이브 피해량이 최고 기록보다 큽니다.");
                stat.CurrentWaveDamage = data.CurrentWaveDamage;
            }
            else throw new ArgumentException("런 누적 기록 버전 또는 데이터가 올바르지 않습니다.");
            stat.CaptureRecords().Validate();
            foreach (var artifact in data.Artifacts)
            {
                stat.Artifacts.Add(artifact.Rarity, artifact.Count);
            }

            _runStats = stat;
            _isRecording = true;
            _runSummary = null;
        }
    }

    public class RunStats
    {
        public PlayerRecordSaveData Records = PlayerRecordSaveData.CreateEmpty();
        public double CurrentWaveDamage;

        public PlayerRecordSaveData CaptureRecords()
        {
            var data = Records.Copy();
            data.EnemyKills = EnemyKillCount;
            data.AllyDeaths = AllyDeathCount;
            data.ClearedWaves = ClearWaveCount;
            data.ClearedBosses = ClearBossCount;
            return data;
        }
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
        private readonly PlayerRecordSaveData _records;
        public PlayerRecordSaveData CaptureRecords() => _records.Copy();
        public long AllyDeathCount { get; }
        public long EnemyKillCount { get; }
        public int MaxQuarter { get; }
        public int MaxWave { get; }
        public int ClearWaveCount { get; }
        public int ClearBossCount { get; }
        public IReadOnlyDictionary<ArtifactRarity, int> Artifacts { get; }
        public IReadOnlyList<RunArtifactEntry> ArtifactInventory { get; }
        public IReadOnlyList<ActiveTotem> Totems { get; }
        public AltarData Altar { get; }
        public IReadOnlyList<TraitLevelEntry> Traits { get; }

        public RunSummary(RunStats stats, IReadOnlyList<ActiveTotem> totems,
            AltarData altar = null, IReadOnlyList<TraitLevelEntry> traits = null,
            IReadOnlyList<ArtifactInstance> artifactInstances = null)
        {
            _records = stats.CaptureRecords();
            // 종료 후 인벤토리가 비워지거나 중첩 수가 바뀌어도 이 기록은 유지한다
            var copiedArtifacts = new List<RunArtifactEntry>();
            if (artifactInstances != null)
                foreach (ArtifactInstance instance in artifactInstances)
                    if (instance != null && instance.Data != null)
                        copiedArtifacts.Add(new RunArtifactEntry(instance.Data, instance.StackCount));
            ArtifactInventory = copiedArtifacts.AsReadOnly();

            Altar = altar;
            var copiedTraits = new List<TraitLevelEntry>();
            if (traits != null)
                foreach (TraitLevelEntry entry in traits)
                    copiedTraits.Add(new TraitLevelEntry { Id = entry.Id, Level = entry.Level });
            Traits = copiedTraits.AsReadOnly();
            AllyDeathCount = stats.AllyDeathCount;
            EnemyKillCount = stats.EnemyKillCount;
            MaxQuarter = stats.MaxQuarter;
            MaxWave = stats.MaxWave;
            ClearWaveCount = stats.ClearWaveCount;
            ClearBossCount = stats.ClearBossCount;
            // 현재 기록과 독립된 복사본을 만든 뒤 읽기 전용으로 제공
            Totems = totems == null
                ? Array.Empty<ActiveTotem>()
                : new List<ActiveTotem>(totems).AsReadOnly();
            Artifacts = new ReadOnlyDictionary<ArtifactRarity, int>(
                new Dictionary<ArtifactRarity, int>(stats.Artifacts));
        }
    }
}
