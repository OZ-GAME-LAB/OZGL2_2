// 상점 UI에서 구매·판매·교환 요청에 사용
public interface IShopTrader
{
    bool TryPurchase(ShopArtifactSlot slot);
    bool TrySell(ArtifactData artifact);

    bool TryExchange(ShopArtifactExchangeSlot slot, ArtifactData ownedArtifact);
}
