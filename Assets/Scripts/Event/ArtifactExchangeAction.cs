using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "ArtifactExchangeAction", menuName = "Events/Actions/Artifact Exchange")]
public sealed class ArtifactExchangeAction : RandomArtifactAction
{
    public override bool CanProceed(EventContext context, out string reason)
    {
        reason = null;
        if (context.SelectionUI == null) reason = "유물 선택 화면이 연결되지 않았습니다.";
        else if (GetExchangeCandidates(context).Count == 0) reason = "교환할 수 있는 유물이 없습니다.";
        return reason == null;
    }

    public override async UniTask<EventResolution> EventProceedAsync(EventContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        List<ArtifactData> candidates = GetExchangeCandidates(context);
        if (candidates.Count == 0) return null;

        ArtifactData selected = await context.SelectionUI.SelectAsync(candidates, token, true,
            "교환에 사용할 유물 1개를 선택해주세요.", ArtifactSelectionPurpose.Exchange);
        token.ThrowIfCancellationRequested();
        if (selected == null) return null;

        // 선택 화면을 기다리는 동안 보유 상태가 달라졌다면 비용 없이 돌아간다.
        if (!candidates.Contains(selected) || !GetExchangeCandidates(context).Contains(selected)) return null;
        ArtifactData reward = SelectReward(context, selected);
        if (reward == null) return null;
        return new EventResolution(new EventResultData(EventResourceType.Artifact, artifact: reward), selected);
    }

    private List<ArtifactData> GetExchangeCandidates(EventContext context)
    {
        var candidates = new List<ArtifactData>();
        foreach (ArtifactInstance owned in context.Artifacts.Instances)
        {
            // 지정 비용으로 소모할 중첩 외에, 사용자가 선택할 중첩이 하나 더 필요하다.
            if (owned.StackCount <= context.GetArtifactCostCount(owned.Data)) continue;
            if (HasRewardCandidate(context, owned.Data)) candidates.Add(owned.Data);
        }
        return candidates;
    }
}
