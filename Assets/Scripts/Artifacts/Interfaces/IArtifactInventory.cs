// 보상과 상점에서 아티팩트 획득, 차감 및 교환에 사용
public interface IArtifactInventory
{
    bool TryAdd(ArtifactData artifact);
    bool TryRemove(ArtifactData artifact);
    bool TryExchange(ArtifactData ownedArtifact, ArtifactData rewardArtifact);
}
