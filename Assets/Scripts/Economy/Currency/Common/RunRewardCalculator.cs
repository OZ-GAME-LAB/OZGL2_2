using System;
using System.Collections.Generic;
using Game.Core;

/// <summary>기록을 점수와 혈석으로 환산합니다. 저장이나 화면을 변경하지 않습니다.</summary>
public sealed class RunRewardCalculator
{
    public RunResult Calculate(RunSummary summary, RunScoreSettings settings,
        IReadOnlyList<CurrencyModifier> modifiers)
    {
        if (summary == null) throw new ArgumentNullException(nameof(summary));
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        settings.Validate();
        if (summary.ClearWaveCount < 0 || summary.ClearBossCount < 0 || summary.ClearBossCount > summary.ClearWaveCount)
            throw new ArgumentException("웨이브·보스 클리어 기록이 올바르지 않습니다.");

        checked
        {
            long progress = (long)summary.ClearWaveCount * settings.WaveScore +
                            (long)summary.ClearBossCount * settings.BossBonusScore;
            var artifactScores = new Dictionary<ArtifactRarity, long>();
            long artifactTotal = 0;
            foreach (var entry in summary.Artifacts)
            {
                if (entry.Value < 0) throw new ArgumentException("유물 개수는 음수일 수 없습니다.");
                long score = (long)entry.Value * settings.GetArtifactScore(entry.Key);
                artifactScores.Add(entry.Key, score);
                artifactTotal += score;
            }
            long baseScore = progress + artifactTotal;
            long bonusPercent = 0;
            foreach (var totem in summary.Totems)
            {
                if (totem.Data == null || totem.Level < 0)
                    throw new ArgumentException("확정 토템 기록이 올바르지 않습니다.");
                bonusPercent += totem.Data.GetRewardBonusPercent(totem.Level);
            }

            double multiplier = 1d + bonusPercent / 100d;
            double converted = (double)baseScore / settings.PointsPerBloodstone;
            if (!CurrencyRewardCalculator.TryCalculateModifiedAmount(CurrencyType.Bloodstone, converted,
                    CurrencyRewardType.RunSettlement, modifiers, out double modified,
                    out double flat, out double percent))
                throw new ArgumentException("혈석 정산 보정값이 올바르지 않습니다.");

            // 환산·보정·토템 배율 사이에는 버리지 않고 최종 지급액에서 한 번만 버립니다.
            double final = Math.Floor(modified * multiplier);
            if (double.IsNaN(final) || double.IsInfinity(final) || final < 0 || final > int.MaxValue)
                throw new OverflowException("정산 혈석이 지급 가능한 범위를 초과했습니다.");
            return new RunResult(progress, artifactScores, artifactTotal, baseScore,
                bonusPercent, multiplier, baseScore * multiplier, converted, flat, percent, modified, (int)final);
        }
    }
}
