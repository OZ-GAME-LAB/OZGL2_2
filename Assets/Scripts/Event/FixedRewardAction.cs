using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "FixedRewardAction", menuName = "Events/Actions/Fixed Reward")]
public sealed class FixedRewardAction : EventActionSO
{
    [SerializeField] private EventResultData _result = new EventResultData();

    public override bool IsFreeExit => _result != null && _result.Type == EventResourceType.None;

    public override bool CanProceed(EventContext context, out string reason)
    {
        return CanReceiveReward(context, _result, out reason);
    }

    public override UniTask<EventResolution> EventProceedAsync(EventContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return UniTask.FromResult(new EventResolution(_result));
    }

    public override bool TryValidate(ArtifactCatalog catalog, out string error)
    {
        return TryValidateReward(_result, catalog, out error);
    }
}
