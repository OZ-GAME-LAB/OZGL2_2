using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI;
using UnityEngine;

// 전략은 결과를 준비하기만 한다. 비용 차감과 보상 지급은 EventManager가 처리한다.
public abstract class EventActionSO : ScriptableObject
{
    public virtual bool IsFreeExit => false;

    public abstract bool CanProceed(EventContext context, out string reason);
    public abstract UniTask<EventResolution> EventProceedAsync(EventContext context, CancellationToken token);
    public abstract bool TryValidate(ArtifactCatalog catalog, out string error);

    protected static bool TryValidateReward(EventResultData reward, ArtifactCatalog catalog, out string error)
    {
        error = null;
        if (reward == null || !Enum.IsDefined(typeof(EventResourceType), reward.Type))
            error = "보상 종류를 확인해주세요.";
        else if (reward.Type == EventResourceType.Gold && reward.Gold <= 0)
            error = "골드 보상은 1 이상이어야 합니다. 보상이 없으면 None을 사용해주세요.";
        else if (reward.Type == EventResourceType.Artifact &&
            (catalog == null || !catalog.Contains(reward.Artifact) || reward.Artifact.MaxStacks < 1))
            error = "보상 유물의 카탈로그 등록과 최대 보유 수량을 확인해주세요.";
        return error == null;
    }

    protected static bool CanReceiveReward(EventContext context, EventResultData reward, out string reason)
    {
        reason = null;
        if (reward.Type == EventResourceType.Artifact && !context.CanReceive(reward.Artifact))
            reason = "보상 유물을 더 획득할 수 없습니다.";
        return reason == null;
    }
}

// 실행마다 새로 전달한다. 공유 SO에는 선택한 유물이나 추첨 결과를 보관하지 않는다.
public sealed class EventContext
{
    public ArtifactManager Artifacts { get; }
    public IArtifactSelectionUI SelectionUI { get; }
    public EventCostData Cost { get; }
    public System.Random Random { get; }

    public EventContext(ArtifactManager artifacts, IArtifactSelectionUI selectionUI,
        EventCostData cost, System.Random random)
    {
        Artifacts = artifacts;
        SelectionUI = selectionUI;
        Cost = cost;
        Random = random;
    }

    public int GetArtifactCostCount(ArtifactData artifact)
    {
        int count = 0;
        foreach (ArtifactData cost in Cost.Artifacts)
            if (cost == artifact) count++;
        return count;
    }

    public bool CanReceive(ArtifactData artifact, ArtifactData selectedCost = null)
    {
        if (!Artifacts.IsInitialized || !Artifacts.ArtifactCatalog.Contains(artifact) || artifact.MaxStacks < 1)
            return false;

        int ownedCount = 0;
        if (Artifacts.TryGetById(artifact.Id, out ArtifactInstance owned))
        {
            if (owned.Data != artifact) return false;
            ownedCount = owned.StackCount;
        }
        int spent = GetArtifactCostCount(artifact) + (selectedCost == artifact ? 1 : 0);
        return ownedCount >= spent && (long)ownedCount - spent + 1 <= artifact.MaxStacks;
    }
}

public sealed class EventResolution
{
    public EventResultData Reward { get; }
    public ArtifactData SelectedArtifactCost { get; }

    public EventResolution(EventResultData reward, ArtifactData selectedArtifactCost = null)
    {
        Reward = reward;
        SelectedArtifactCost = selectedArtifactCost;
    }
}
