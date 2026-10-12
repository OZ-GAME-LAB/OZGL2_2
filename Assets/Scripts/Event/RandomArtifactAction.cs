using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "RandomArtifactAction", menuName = "Events/Actions/Random Artifact")]
public class RandomArtifactAction : EventActionSO
{
    [SerializeField, Min(0)] private int _commonWeight = 70;
    [SerializeField, Min(0)] private int _rareWeight = 25;
    [SerializeField, Min(0)] private int _legendaryWeight = 5;
    [SerializeField, Min(0)] private int _mythicWeight;

    private static readonly ArtifactRarity[] Rarities =
    {
        ArtifactRarity.Common, ArtifactRarity.Rare, ArtifactRarity.Legendary, ArtifactRarity.Mythic
    };

    public override bool CanProceed(EventContext context, out string reason)
    {
        reason = HasRewardCandidate(context) ? null : "획득할 수 있는 유물이 없습니다.";
        return reason == null;
    }

    public override UniTask<EventResolution> EventProceedAsync(EventContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ArtifactData artifact = SelectReward(context);
        return UniTask.FromResult(artifact == null ? null :
            new EventResolution(new EventResultData(EventResourceType.Artifact, artifact: artifact)));
    }

    public override bool TryValidate(ArtifactCatalog catalog, out string error)
    {
        error = null;
        if (_commonWeight < 0 || _rareWeight < 0 || _legendaryWeight < 0 || _mythicWeight < 0)
            error = "유물 등급 가중치는 0 이상이어야 합니다.";
        else if (_commonWeight == 0 && _rareWeight == 0 && _legendaryWeight == 0 && _mythicWeight == 0)
            error = "양수 가중치를 가진 유물 등급이 하나 이상 필요합니다.";
        return error == null;
    }

    protected bool HasRewardCandidate(EventContext context, ArtifactData selectedCost = null)
    {
        foreach (ArtifactRarity rarity in Rarities)
            if (GetWeight(rarity) > 0 && GetCandidates(context, rarity, selectedCost).Count > 0) return true;
        return false;
    }

    protected ArtifactData SelectReward(EventContext context, ArtifactData selectedCost = null)
    {
        var pools = new List<ArtifactData>[Rarities.Length];
        double totalWeight = 0;
        for (int i = 0; i < Rarities.Length; i++)
        {
            if (GetWeight(Rarities[i]) <= 0) continue;
            pools[i] = GetCandidates(context, Rarities[i], selectedCost);
            if (pools[i].Count > 0) totalWeight += GetWeight(Rarities[i]);
        }
        if (totalWeight == 0) return null;

        // 남은 등급 중 하나를 뽑은 뒤, 그 등급의 유물 중 하나를 균등하게 뽑는다.
        double roll = context.Random.NextDouble() * totalWeight;
        for (int i = 0; i < Rarities.Length; i++)
        {
            if (pools[i] == null || pools[i].Count == 0) continue;
            roll -= GetWeight(Rarities[i]);
            if (roll < 0) return pools[i][context.Random.Next(pools[i].Count)];
        }
        return null;
    }

    private List<ArtifactData> GetCandidates(EventContext context, ArtifactRarity rarity, ArtifactData selectedCost)
    {
        var candidates = new List<ArtifactData>();
        foreach (ArtifactData artifact in context.Artifacts.ArtifactCatalog.GetByRarity(rarity))
        {
            // 바친 유물과 같은 종류는 보상에서 제외한다. 다른 보유 유물은 중첩 한도까지 허용한다.
            if (artifact == selectedCost || context.GetArtifactCostCount(artifact) > 0) continue;
            if (context.CanReceive(artifact, selectedCost)) candidates.Add(artifact);
        }
        return candidates;
    }

    private int GetWeight(ArtifactRarity rarity)
    {
        switch (rarity)
        {
            case ArtifactRarity.Common: return _commonWeight;
            case ArtifactRarity.Rare: return _rareWeight;
            case ArtifactRarity.Legendary: return _legendaryWeight;
            case ArtifactRarity.Mythic: return _mythicWeight;
            default: return 0;
        }
    }
}
