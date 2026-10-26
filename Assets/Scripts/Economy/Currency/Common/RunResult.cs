using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>한 번 확정한 정산 내역. 지급과 화면은 같은 결과를 사용합니다.</summary>
public sealed class RunResult
{
    public long ProgressScore { get; }
    public IReadOnlyDictionary<ArtifactRarity, long> ArtifactScores { get; }
    public long ArtifactScore { get; }
    public long BaseScore { get; }
    public long TotemBonusPercent { get; }
    public double TotemMultiplier { get; }
    public double TotemAdjustedScore { get; }
    public double ConvertedBloodstones { get; }
    public double FlatModifier { get; }
    public double PercentModifier { get; }
    public double ModifiedBloodstones { get; }
    public int FinalBloodstones { get; }

    internal RunResult(long progressScore, Dictionary<ArtifactRarity, long> artifactScores,
        long artifactScore, long baseScore, long totemBonusPercent, double totemMultiplier,
        double totemAdjustedScore, double convertedBloodstones, double flatModifier,
        double percentModifier, double modifiedBloodstones, int finalBloodstones)
    {
        ProgressScore = progressScore;
        ArtifactScores = new ReadOnlyDictionary<ArtifactRarity, long>(new Dictionary<ArtifactRarity, long>(artifactScores));
        ArtifactScore = artifactScore;
        BaseScore = baseScore;
        TotemBonusPercent = totemBonusPercent;
        TotemMultiplier = totemMultiplier;
        TotemAdjustedScore = totemAdjustedScore;
        ConvertedBloodstones = convertedBloodstones;
        FlatModifier = flatModifier;
        PercentModifier = percentModifier;
        ModifiedBloodstones = modifiedBloodstones;
        FinalBloodstones = finalBloodstones;
    }
}
