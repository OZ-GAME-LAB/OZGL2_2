namespace Game.UI.InGame
{
    public enum UIId
    {
        None, Hud, BuildingCatalog, BuildingInfo, WaveReward,
        ArtifactReward, RunDecision, RunResult, Message, Detail, Shop,
        ArtifactDetail, UnitDetail, SkillDetail, ArtifactInventory, Event
    }

    public enum UICloseReason { UserCancel, Replaced, Completed, ContextLost }
}
