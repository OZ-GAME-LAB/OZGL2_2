using System;
using OZGL.KDH;
using UnityEngine;

namespace Game.UI
{
    /// <summary>담당자가 제공한 건설 후보의 표시 정보. null 가격은 미정이며 무료를 뜻하지 않는다.</summary>
    public sealed class BuildingCatalogItem
    {
        public string Id { get; }
        public string Name { get; }
        public string Summary { get; }
        public int? GoldCost { get; }
        public int? GemCost { get; }
        public BuildingType Category { get; }
        public Sprite Icon { get; }
        public BuildingActionOffer Offer { get; }
        public BuildingCatalogItem(string id, string name, string summary, int? goldCost)
            : this(id, name, summary, goldCost, goldCost.HasValue ? 0 : (int?)null) { }

        public BuildingCatalogItem(string id, string name, string summary, int? goldCost, int? gemCost,
            BuildingType category = BuildingType.Barracks, Sprite icon = null, BuildingActionOffer offer = null)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Catalog ID and name are required.");
            if (goldCost < 0) throw new ArgumentOutOfRangeException(nameof(goldCost));
            if (gemCost < 0) throw new ArgumentOutOfRangeException(nameof(gemCost));
            if (goldCost.HasValue != gemCost.HasValue)
                throw new ArgumentException("Both currency amounts must be known, or both must be unspecified.");
            Id = id;
            Name = name;
            Summary = summary ?? "";
            GoldCost = goldCost;
            GemCost = gemCost;
            Category = category;
            Icon = icon;
            Offer = offer;
        }
    }

}
