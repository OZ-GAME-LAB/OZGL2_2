using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "WeightedRewardAction", menuName = "Events/Actions/Weighted Reward")]
public sealed class WeightedRewardAction : EventActionSO
{
    [Serializable]
    private sealed class WeightedResult
    {
        [SerializeField, Min(0)] private int _weight = 1;
        [SerializeField] private EventResultData _result = new EventResultData();

        public int Weight => _weight;
        public EventResultData Result => _result;
    }

    [SerializeField] private WeightedResult[] _results = Array.Empty<WeightedResult>();

    public override bool CanProceed(EventContext context, out string reason)
    {
        reason = null;
        // 받을 수 없는 결과를 빼고 다시 추첨하면 설정한 확률이 바뀌므로 선택 자체를 막는다.
        foreach (WeightedResult result in _results)
            if (result.Weight > 0 && !CanReceiveReward(context, result.Result, out reason)) return false;
        return true;
    }

    public override UniTask<EventResolution> EventProceedAsync(EventContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        double totalWeight = 0;
        foreach (WeightedResult result in _results) totalWeight += result.Weight;

        double roll = context.Random.NextDouble() * totalWeight;
        foreach (WeightedResult result in _results)
        {
            if (result.Weight == 0) continue;
            roll -= result.Weight;
            if (roll < 0) return UniTask.FromResult(new EventResolution(result.Result));
        }
        throw new InvalidOperationException("추첨할 이벤트 결과가 없습니다.");
    }

    public override bool TryValidate(ArtifactCatalog catalog, out string error)
    {
        error = null;
        bool hasResult = false;
        foreach (WeightedResult result in _results ?? Array.Empty<WeightedResult>())
        {
            if (result == null || result.Weight < 0)
            {
                error = "확률 결과의 빈 항목과 음수 가중치를 확인해주세요.";
                return false;
            }
            if (!TryValidateReward(result.Result, catalog, out error)) return false;
            hasResult |= result.Weight > 0;
        }
        if (!hasResult) error = "양수 가중치를 가진 결과가 하나 이상 필요합니다.";
        return error == null;
    }
}
