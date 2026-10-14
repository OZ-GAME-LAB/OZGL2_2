using System;

namespace Game.UI
{
    /// <summary>UI 수신용 스냅샷. null은 미연결이며 0 보상과 구분한다.</summary>
    public sealed class RunSettlementViewData
    {
        public bool IsVictory { get; }
        public string TotemSummary { get; }
        public string ProgressSummary { get; }
        public string ArtifactSummary { get; }
        public long? TotalScore { get; }
        public int? Bloodstones { get; }
        public int? GaugeCurrent { get; }
        public int? GaugeTarget { get; }

        public RunSettlementViewData(bool isVictory, string totems, string progress, string artifacts,
            long? totalScore, int? bloodstones, int? gaugeCurrent, int? gaugeTarget)
        {
            if (totalScore < 0 || bloodstones < 0 || gaugeCurrent < 0 || gaugeTarget <= 0 ||
                gaugeCurrent.HasValue != gaugeTarget.HasValue || gaugeCurrent > gaugeTarget)
                throw new ArgumentOutOfRangeException(nameof(totalScore), "Invalid settlement display values.");
            IsVictory = isVictory;
            TotemSummary = totems;
            ProgressSummary = progress;
            ArtifactSummary = artifacts;
            TotalScore = totalScore;
            Bloodstones = bloodstones;
            GaugeCurrent = gaugeCurrent;
            GaugeTarget = gaugeTarget;
        }
    }
}
