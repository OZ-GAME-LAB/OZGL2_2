using System;
using System.Collections.Generic;

namespace Game.Core
{
    [Serializable]
    public sealed class PlayerRecordSaveData
    {
        public int Version;
        // JsonUtility의 누락 객체는 Version 0으로 남겨 정상 빈 기록과 구분합니다.
        public static PlayerRecordSaveData CreateEmpty() => new PlayerRecordSaveData { Version = 1 };
        public long EnemyKills;
        public long SkillKills;
        public long AllyDeaths;
        public long ClearedWaves;
        public long ClearedBosses;
        public double TotalDamage;
        public double HighestHitDamage;
        public double HighestWaveDamage;
        public List<int> ClearedBossQuarters = new List<int>();

        public PlayerRecordSaveData Copy()
        {
            return new PlayerRecordSaveData
            {
                Version = Version,
                EnemyKills = EnemyKills, SkillKills = SkillKills, AllyDeaths = AllyDeaths,
                ClearedWaves = ClearedWaves, ClearedBosses = ClearedBosses,
                TotalDamage = TotalDamage, HighestHitDamage = HighestHitDamage, HighestWaveDamage = HighestWaveDamage,
                ClearedBossQuarters = new List<int>(ClearedBossQuarters)
            };
        }

        public void Validate()
        {
            if (Version != 1) throw new ArgumentException("필수 누적 기록이 없거나 버전이 올바르지 않습니다.");
            if (EnemyKills < 0 || SkillKills < 0 || SkillKills > EnemyKills || AllyDeaths < 0 ||
                ClearedWaves < 0 || ClearedBosses < 0 || ClearedBosses > ClearedWaves)
                throw new ArgumentException("누적 횟수가 올바르지 않습니다.");
            ValidateDamage(TotalDamage);
            ValidateDamage(HighestHitDamage);
            ValidateDamage(HighestWaveDamage);
            if (HighestHitDamage > HighestWaveDamage || HighestWaveDamage > TotalDamage)
                throw new ArgumentException("최고 피해량이 전체 피해량보다 큽니다.");
            if (ClearedBossQuarters == null)
                throw new ArgumentException("보스 분기 기록이 없습니다.");
            var seen = new HashSet<int>();
            foreach (int quarter in ClearedBossQuarters)
                if (quarter < 1 || !seen.Add(quarter))
                    throw new ArgumentException("보스 분기 기록의 값 또는 중복을 확인하세요.");
        }

        public static void ValidateDamage(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                throw new ArgumentException("피해량은 유한한 0 이상의 값이어야 합니다.");
        }
    }
}
