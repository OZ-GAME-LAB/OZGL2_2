using UnityEngine;

// 상점 공통 설정. 1회성 아이템은 추후 별도 슬롯 설정으로 추가
[CreateAssetMenu(fileName = "ShopTable", menuName = "Shop/Shop Table")]
public class ShopTable : ScriptableObject
{
    public int ArtifactSlotCount => _artifactSlotCount;
    public CurrencyType PurchaseCurrency => _purchaseCurrency;
    public ShopArtifactTable ShopArtifactTable => _artifactShopTable;
    public ShopArtifactExchangeTable ShopArtifactExchangeTable => _artifactExchangeTable;

    [SerializeField, Min(1)] private int _artifactSlotCount = 5;
    [SerializeField] private CurrencyType _purchaseCurrency = CurrencyType.Gold;
    [SerializeField] private ShopArtifactTable _artifactShopTable;
    [SerializeField] private ShopArtifactExchangeTable _artifactExchangeTable;
}
