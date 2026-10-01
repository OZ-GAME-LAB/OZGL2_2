namespace Game.UI.InGame
{
    public enum UIId
    {
        None, Hud, BuildingCatalog, BuildingInfo, WaveReward,
        ArtifactReward, RunDecision, RunResult, Message, Detail
    }

    public enum UICloseReason { UserCancel, Replaced, Completed, ContextLost }
}
