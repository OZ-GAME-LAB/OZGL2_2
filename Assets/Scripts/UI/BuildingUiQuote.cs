using OZGL.KDH;
using UnityEngine;

namespace Game.UI
{
    /// <summary>원본 BuildingData/RefundRate의 읽기 전용 표시 어댑터. 어떤 재화도 변경하지 않는다.</summary>
    internal static class BuildingUiQuote
    {
        public static bool TryRead(BuildingData data, float refundRate, out int gold, out int gems,
            out int refundGold, out int refundGems)
        {
            if (TryReadCosts(data, refundRate, out gold, out gems, out refundGold, out refundGems) &&
                refundGold >= 0 && refundGems >= 0) return true;
            gold = gems = refundGold = refundGems = 0;
            return false;
        }

        public static bool TryReadUpgrade(BuildingResourceCost[] costs, out int gold, out int gems)
        {
            gold = gems = 0;
            bool hasGold = false, hasGem = false;
            foreach (var cost in costs ?? System.Array.Empty<BuildingResourceCost>())
            {
                if (cost.amount < 0) return false;
                switch (cost.type)
                {
                    case BuildingResourceType.Gold:
                        if (hasGold) return false;
                        hasGold = true; gold = cost.amount; break;
                    case BuildingResourceType.Gem:
                        if (hasGem) return false;
                        hasGem = true; gems = cost.amount; break;
                    default: return false;
                }
            }
            return true;
        }

        public static string Category(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Core: return "베이스캠프";
                case BuildingType.Producer: return "자원 생산";
                case BuildingType.Barracks: return "유닛 생산";
                case BuildingType.Support: return "지원";
                default: return "건물";
            }
        }

        public static string Production(BuildingData data)
        {
            if (data.HasProduction)
                return $"웨이브 종료 후 {data.Production.amount:N0} " +
                    (data.Production.resourceType == BuildingResourceType.Gem ? "보석" : "골드");
            if (data.HasSpawn) return $"웨이브 시작 시 {Mathf.Min(data.Spawn.countPerWave, data.Spawn.maxAlive)}명 생산";
            return null;
        }
        private static bool TryReadCosts(BuildingData data, float refundRate, out int gold, out int gems,
            out int refundGold, out int refundGems)
        {
            gold = gems = refundGold = refundGems = 0;
            if (data == null || string.IsNullOrWhiteSpace(data.BuildingId) ||
                string.IsNullOrWhiteSpace(data.DisplayName) || (data.Prefab == null && data.WorldSprite == null) ||
                float.IsNaN(refundRate) || float.IsInfinity(refundRate)) return false;
            bool hasGold = false, hasGem = false;
            // 중복 재화 항목은 원본 CanAfford/순차 차감 간 부분 적용 위험이 있어 UI에서 실행하지 않는다.
            foreach (var cost in data.BuildCost ?? System.Array.Empty<BuildingResourceCost>())
            {
                if (cost.amount < 0) return false;
                switch (cost.type)
                {
                    case BuildingResourceType.Gold:
                        if (hasGold) return false;
                        hasGold = true; gold = cost.amount;
                        refundGold = Mathf.FloorToInt(cost.amount * Mathf.Clamp01(refundRate)); break;
                    case BuildingResourceType.Gem:
                        if (hasGem) return false;
                        hasGem = true; gems = cost.amount;
                        refundGems = Mathf.FloorToInt(cost.amount * Mathf.Clamp01(refundRate)); break;
                    default: return false;
                }
            }
            return true;
        }

    }
}
