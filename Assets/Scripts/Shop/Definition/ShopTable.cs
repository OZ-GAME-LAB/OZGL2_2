using UnityEngine;

// 아티팩트 구매·교환과 소모성 아이템 판매 설정
[CreateAssetMenu(fileName = "ShopTable", menuName = "Shop/Shop Table")]
public class ShopTable : ScriptableObject
{
    public int ArtifactSlotCount => _artifactSlotCount;
    public CurrencyType PurchaseCurrency => _purchaseCurrency;
    public ShopArtifactTable ShopArtifactTable => _artifactShopTable;
    public ShopArtifactExchangeTable ShopArtifactExchangeTable => _artifactExchangeTable;
    public ShopConsumableTable ShopConsumableTable => _consumableShopTable;

    [SerializeField, Min(1)] private int _artifactSlotCount = 5;
    [SerializeField] private CurrencyType _purchaseCurrency = CurrencyType.Gold;
    [SerializeField] private ShopArtifactTable _artifactShopTable;
    [SerializeField] private ShopArtifactExchangeTable _artifactExchangeTable;
    // 연결하지 않으면 기존 아티팩트 상점 설정만 사용
    [SerializeField] private ShopConsumableTable _consumableShopTable;
}
