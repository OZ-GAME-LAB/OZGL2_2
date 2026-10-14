using OZGL.KDH;
using Units;
using Units.Skills;
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
            string className = ClassLabel(support.targetClass);
            string unitName = UnitLabel(support.targetUnitType);

            // Current date KDH 2026-10-14
            // 정보 창을 열 때 한 번만 문자열을 만듭니다. Update에서 호출하지 않습니다.
            // 계열 수치 줄과 그 T3 유닛 전용 패시브 이름을 나눠 보여 줍니다.
            string summary = AppendEffects(null, support.effects, className == null ? null : className + " 계열");
            summary = AppendPassive(summary, support.uniquePassive, unitName);
            if (summary == null)
                return className == null && unitName == null ? "적용 대상 없음" : "적용할 효과가 없습니다.";
            return summary + "\n건물이 있는 동안 적용";
        }

        private static string AppendEffects(string summary, BuildingSupportEffect[] effects, string target)
        {
            if (target == null || effects == null)
                return summary;

            for (int i = 0; i < effects.Length; i++)
            {
                BuildingSupportEffect effect = effects[i];
                string stat = StatLabel(effect.statType);
                float value = effect.modifierType == UnitStatModifierType.Percent ? effect.value * 100f : effect.value;
                string suffix = effect.modifierType == UnitStatModifierType.Percent ? "%" : "";
                string line = $"{target} {stat} {value:+0.##;-0.##;0}{suffix}";
                summary = summary == null ? line : summary + "\n" + line;
            }

            return summary;
        }

        private static string AppendPassive(string summary, PassiveSkillData passive, string unitName)
        {
            if (passive == null || unitName == null)
                return summary;

            string passiveName = passive.Display != null ? passive.Display.GetName(passive.name) : passive.name;
            string line = $"{unitName} 패시브: {passiveName}";
            return summary == null ? line : summary + "\n" + line;
        }

        private static string ClassLabel(AllyUnitClass targetClass)
        {
            switch (targetClass)
            {
                case AllyUnitClass.Warrior: return "전사";
                case AllyUnitClass.Swordsman: return "검사";
                case AllyUnitClass.Archer: return "궁수";
                case AllyUnitClass.Mage: return "마법사";
                case AllyUnitClass.Cultist: return "광신도";
                default: return null;
            }
        }

        private static string UnitLabel(AllyUnitType unitType)
        {
            switch (unitType)
            {
                case AllyUnitType.WA_T3_11_폭주전사: return "폭주전사";
                case AllyUnitType.WA_T3_21_철갑전사: return "철갑전사";
                case AllyUnitType.WA_T3_22_마갑전사: return "마갑전사";
                case AllyUnitType.SW_T3_11_환영검사: return "환영검사";
                case AllyUnitType.SW_T3_21_그림자검: return "그림자검";
                case AllyUnitType.AR_T3_11_명사수: return "명사수";
                case AllyUnitType.AR_T3_12_속사수: return "속사수";
                case AllyUnitType.AR_T3_21_일격사수: return "일격사수";
                case AllyUnitType.MG_T3_11_폭렬마도사: return "폭렬마도사";
                case AllyUnitType.MG_T3_21_혹한마도사: return "혹한마도사";
                case AllyUnitType.MG_T3_31_천공마도사: return "천공마도사";
                case AllyUnitType.CU_T3_11_혈사제: return "혈사제";
                case AllyUnitType.CU_T3_21_임프: return "임프";
                case AllyUnitType.CU_T3_22_몽마: return "몽마";
                default: return null;
            }
        }

        private static string StatLabel(UnitStatType statType)
        {
            switch (statType)
            {
                case UnitStatType.MaxHp: return "최대 체력";
                case UnitStatType.Defense: return "방어력";
                case UnitStatType.DamageTakenMultiplier: return "받는 피해 배율";
                case UnitStatType.HealingTakenMultiplier: return "받는 회복 배율";
                case UnitStatType.AttackPower: return "공격력";
                case UnitStatType.DamageMultiplier: return "피해 배율";
                case UnitStatType.BasicAttackMultiplier: return "기본 공격 배율";
                case UnitStatType.SkillDamageMultiplier: return "스킬 피해 배율";
                case UnitStatType.DefenseIgnore: return "방어 무시";
                case UnitStatType.LifeSteal: return "흡혈";
                case UnitStatType.HealingMultiplier: return "회복 배율";
                case UnitStatType.AttackSpeed: return "공격 속도";
                case UnitStatType.CooldownReduction: return "재사용 대기시간 감소";
                case UnitStatType.CriticalChance: return "치명타 확률";
                case UnitStatType.CriticalDamage: return "치명타 피해";
                case UnitStatType.MoveSpeed: return "이동 속도";
                case UnitStatType.DetectionRange: return "탐지 거리";
                default: return statType.ToString();
            }
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
