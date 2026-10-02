namespace Game.UI.InGame
{
    public enum UIId
    {
        None, Hud, BuildingCatalog, BuildingInfo, WaveReward,
        ArtifactReward, RunDecision, RunResult, Message, Detail, Shop
    }

    public enum UICloseReason { UserCancel, Replaced, Completed, ContextLost }
}
