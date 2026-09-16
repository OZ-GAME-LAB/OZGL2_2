// 상점 생성 시 확정한 구매 품목과 가격. 구매 처리는 ShopManager에서 완료 후 표시
public class ShopArtifactSlot
{
    public ArtifactData Artifact { get; }
    public CurrencyType Currency { get; }
    public int Price { get; }
    public bool IsPurchased { get; private set; }

    public ShopArtifactSlot(ArtifactData artifact, CurrencyType currency, int price)
    {
        Artifact = artifact;
        Currency = currency;
        Price = price;
    }

    // 재화 소비와 아티팩트 지급 성공 후 호출
    public void MarkPurchased()
    {
        IsPurchased = true;
    }
}
