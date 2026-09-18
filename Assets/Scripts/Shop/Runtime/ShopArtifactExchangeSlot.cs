// 교환 품목과 완료 상태. 교환 재료 목록은 선택 시 ArtifactManager에서 조회
public class ShopArtifactExchangeSlot
{
    public ArtifactData Artifact { get; }
    public bool IsExchanged { get; private set; }

    public ShopArtifactExchangeSlot(ArtifactData artifact)
    {
        Artifact = artifact;
    }

    // 아티팩트 교환 성공 후 호출
    public void MarkExchanged()
    {
        IsExchanged = true;
    }
}
