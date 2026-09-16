using System;
using System.Collections.Generic;
using UnityEngine;

// 동일 등급의 보유 아티팩트 중첩 1개를 소비하는 교환 설정
[CreateAssetMenu(fileName = "ShopArtifactExchangeTable", menuName = "Shop/Shop Artifact Exchange Table")]
public class ShopArtifactExchangeTable : ScriptableObject
{
    public int SlotCount => _slotCount;
    public bool OnlyExchangeableRarities => _onlyExchangeableRarities;
    public List<ShopArtifactExchangeRarityData> RaritySettings => _raritySettings;

    [SerializeField, Min(1)] private int _slotCount = 1;
    // 꺼두면 보유 등급과 관계없이 전체 등급에서 추첨
    [SerializeField] private bool _onlyExchangeableRarities;
    [SerializeField] private List<ShopArtifactExchangeRarityData> _raritySettings =
        new List<ShopArtifactExchangeRarityData>
        {
            new ShopArtifactExchangeRarityData(ArtifactRarity.Common, 70),
            new ShopArtifactExchangeRarityData(ArtifactRarity.Rare, 25),
            new ShopArtifactExchangeRarityData(ArtifactRarity.Legendary, 5),
            new ShopArtifactExchangeRarityData(ArtifactRarity.Mythic, 0)
        };

    private void OnValidate()
    {
        if (_raritySettings == null)
        {
            return;
        }

        var registered = new HashSet<ArtifactRarity>();
        foreach (ShopArtifactExchangeRarityData setting in _raritySettings)
        {
            if (setting == null || setting.Weight < 0)
            {
                Debug.LogError("[Shop/ShopArtifactExchangeTable] 등급별 설정과 0 이상의 가중치 입력 필요", this);
                return;
            }

            if (!registered.Add(setting.Rarity))
            {
                Debug.LogError("[Shop/ShopArtifactExchangeTable] 동일 등급 중복 등록 확인", this);
                return;
            }
        }
    }
}

[Serializable]
public class ShopArtifactExchangeRarityData
{
    public ArtifactRarity Rarity => _rarity;
    public int Weight => _weight;

    [SerializeField] private ArtifactRarity _rarity;
    [SerializeField, Min(0)] private int _weight;

    public ShopArtifactExchangeRarityData(ArtifactRarity rarity, int weight)
    {
        _rarity = rarity;
        _weight = weight;
    }
}
