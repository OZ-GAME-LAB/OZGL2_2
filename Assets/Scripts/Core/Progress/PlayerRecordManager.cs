using System;
using UnityEngine;

namespace Game.Core
{
    // 런의 임시 기록과 구분되는, 저장이 완료된 플레이어 기록입니다.
    public sealed class PlayerRecordManager
    {
        private PlayerRecordSaveData _records = PlayerRecordSaveData.CreateEmpty();
        public event Action Changed;
        public PlayerRecordSaveData CaptureSaveData() => _records.Copy();

        // 합산한 저장 사본만 반환합니다. 현재 기록은 저장 성공 후 RestoreSaveData로 바꿉니다.
        public PlayerRecordSaveData AddRun(RunSummary summary)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            PlayerRecordSaveData run = summary.CaptureRecords();
            run.Validate();
            var next = _records.Copy();
            checked
            {
                next.EnemyKills += run.EnemyKills;
                next.SkillKills += run.SkillKills;
                next.AllyDeaths += run.AllyDeaths;
                next.ClearedWaves += run.ClearedWaves;
                next.ClearedBosses += run.ClearedBosses;
            }
            next.TotalDamage += run.TotalDamage;
            next.HighestHitDamage = Math.Max(next.HighestHitDamage, run.HighestHitDamage);
            next.HighestWaveDamage = Math.Max(next.HighestWaveDamage, run.HighestWaveDamage);
            foreach (int quarter in run.ClearedBossQuarters)
                if (!next.ClearedBossQuarters.Contains(quarter)) next.ClearedBossQuarters.Add(quarter);
            next.Validate();
            return next;
        }

        public void RestoreSaveData(PlayerRecordSaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            data.Validate();
            _records = data.Copy();
        }

        public void NotifyChanged()
        {
            // 이미 파일 저장이 끝났으므로 UI 관찰자의 예외가 저장 실패로 바뀌면 안 됩니다.
            if (Changed == null) return;
            foreach (Action listener in Changed.GetInvocationList())
                try { listener(); } catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}
