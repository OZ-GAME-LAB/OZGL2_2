using System;
using System.Collections.Generic;
using UnityEngine;

// 아티팩트 등급별 등장 가중치, 구매 가격 범위 및 고정 판매가
[CreateAssetMenu(fileName = "ShopArtifactTable", menuName = "Shop/Shop Artifact Table")]
public class ShopArtifactTable : ScriptableObject
{
    public List<ShopArtifactRarityData> RaritySettings => _raritySettings;

    [SerializeField] private List<ShopArtifactRarityData> _raritySettings =
        new List<ShopArtifactRarityData>
        {
            new ShopArtifactRarityData(ArtifactRarity.Common, 70, 80, 120, 40),
            new ShopArtifactRarityData(ArtifactRarity.Rare, 25, 200, 300, 100),
            new ShopArtifactRarityData(ArtifactRarity.Legendary, 5, 400, 600, 200),
            new ShopArtifactRarityData(ArtifactRarity.Mythic, 0, 800, 1200, 400)
        };

    private void OnValidate()
    {
        if (_raritySettings == null)
        {
            return;
        }

        var registered = new HashSet<ArtifactRarity>();
        foreach (ShopArtifactRarityData setting in _raritySettings)
        {
            if (setting == null || setting.Weight < 0 || setting.MinPrice < 0 ||
                setting.MaxPrice < setting.MinPrice)
            {
                Debug.LogError("[Shop/ShopArtifactTable] 가중치와 가격은 0 이상, 최대 가격은 최소 가격 이상으로 설정", this);
                return;
            }

            if (!registered.Add(setting.Rarity))
            {
                Debug.LogError("[Shop/ShopArtifactTable] 동일 등급 중복 등록 확인", this);
                return;
            }

            if (setting.SellPrice < 0 || setting.SellPrice > setting.MinPrice)
            {
                Debug.LogError("[Shop/ShopArtifactTable] 판매가는 0 이상, 최소 구매 가격 이하로 설정", this);
                return;
            }
        }
    }
}

[Serializable]
public class ShopArtifactRarityData
{
    public ArtifactRarity Rarity => _rarity;
    public int Weight => _weight;
    public int MinPrice => _minPrice;
    public int MaxPrice => _maxPrice;
    public int SellPrice => _sellPrice;

    [SerializeField] private ArtifactRarity _rarity;
    [SerializeField, Min(0)] private int _weight;
    [SerializeField, Min(0)] private int _minPrice;
    [SerializeField, Min(0)] private int _maxPrice;
    // 보유 아티팩트 중첩 1개 판매 시 지급할 고정 수량
    [SerializeField, Min(0)] private int _sellPrice;

    // 가격은 품목 생성 시 양 끝값을 포함한 범위에서 한 번 추첨 후 유지
    public ShopArtifactRarityData(ArtifactRarity rarity, int weight, int minPrice, int maxPrice, int sellPrice)
    {
        _rarity = rarity;
        _weight = weight;
        _minPrice = minPrice;
        _maxPrice = maxPrice;
        _sellPrice = sellPrice;
    }
}
