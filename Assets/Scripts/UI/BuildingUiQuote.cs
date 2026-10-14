using OZGL.KDH;
using Units;
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
                case BuildingType.Producer: return "자원 건물";
                case BuildingType.Barracks: return "전투 건물";
                case BuildingType.Support: return "연구 건물";
                default: return "건물";
            }
        }

        public static string Production(BuildingData data)
        {
            string summary = null;
            if (data.HasProduction)
                summary = $"웨이브 종료 후 {data.Production.amount:N0} " +
                    (data.Production.resourceType == BuildingResourceType.Gem ? "보석" : "골드");
            if (data.HasSpawn)
            {
                if (summary != null) summary += "\n";
                summary += $"웨이브 시작 시 {Mathf.Min(data.Spawn.countPerWave, data.Spawn.maxAlive)}명 소환";
            }
            return summary;
        }

        public static string Effects(BuildingData data)
        {
            if (data.IsCore)
                return data.BuildLimit == 0 ? "일반 건물 건설 한도: 제한 없음" : $"일반 건물 건설 한도: {data.BuildLimit}개";
            if (!data.HasSupport) return null;

            BuildingSupportSettings support = data.Support;
            string target;
            switch (support.targetClass)
            {
                case AllyUnitClass.Warrior: target = "전사"; break;
                case AllyUnitClass.Swordsman: target = "검사"; break;
                case AllyUnitClass.Archer: target = "궁수"; break;
                case AllyUnitClass.Mage: target = "마법사"; break;
                case AllyUnitClass.Cultist: target = "광신도"; break;
                default: return "적용 대상 없음";
            }
            string stat;
            switch (support.statType)
            {
                case UnitStatType.MaxHp: stat = "최대 체력"; break;
                case UnitStatType.Defense: stat = "방어력"; break;
                case UnitStatType.DamageTakenMultiplier: stat = "받는 피해 배율"; break;
                case UnitStatType.HealingTakenMultiplier: stat = "받는 회복 배율"; break;
                case UnitStatType.AttackPower: stat = "공격력"; break;
                case UnitStatType.DamageMultiplier: stat = "피해 배율"; break;
                case UnitStatType.BasicAttackMultiplier: stat = "기본 공격 배율"; break;
                case UnitStatType.SkillDamageMultiplier: stat = "스킬 피해 배율"; break;
                case UnitStatType.DefenseIgnore: stat = "방어 무시"; break;
                case UnitStatType.LifeSteal: stat = "흡혈"; break;
                case UnitStatType.HealingMultiplier: stat = "회복 배율"; break;
                case UnitStatType.AttackSpeed: stat = "공격 속도"; break;
                case UnitStatType.CooldownReduction: stat = "재사용 대기시간 감소"; break;
                case UnitStatType.CriticalChance: stat = "치명타 확률"; break;
                case UnitStatType.CriticalDamage: stat = "치명타 피해"; break;
                case UnitStatType.MoveSpeed: stat = "이동 속도"; break;
                case UnitStatType.DetectionRange: stat = "탐지 거리"; break;
                default: stat = support.statType.ToString(); break;
            }
            float value = support.modifierType == UnitStatModifierType.Percent ? support.value * 100f : support.value;
            string suffix = support.modifierType == UnitStatModifierType.Percent ? "%" : "";
            return $"{target} 계열 {stat} {value:+0.##;-0.##;0}{suffix}\n건물이 있는 동안 적용";
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
