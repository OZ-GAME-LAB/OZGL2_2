using Game.Core;
using UnityEngine;

public interface ISummary
{
    public void CompleteRun();
    public bool TryGetRunSummary(out RunSummary summary);
}
