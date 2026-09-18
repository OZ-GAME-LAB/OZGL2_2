using System;
using UnityEngine;

namespace Game.UI
{
    /// <summary>선택한 유닛의 UI 스냅샷. 유닛 SO/능력치 계산/사망 판정을 소유하지 않는다.</summary>
    public sealed class UnitInfoData
    {
        public string SelectionId { get; }
        public string DisplayName { get; }
        public string FactionLabel { get; }
        public string RoleLabel { get; }
        public float CurrentHealth { get; }
        public float MaxHealth { get; }
        public string Description { get; }
        public string CombatSummary { get; }
        public string TraitSummary { get; }
        public Sprite Icon { get; }

        public UnitInfoData(string selectionId, string displayName, string factionLabel, string roleLabel,
            float currentHealth, float maxHealth, string description = null, string combatSummary = null,
            string traitSummary = null, Sprite icon = null)
        {
            if (string.IsNullOrWhiteSpace(selectionId)) throw new ArgumentException("A spawn instance ID is required.", nameof(selectionId));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("A display name is required.", nameof(displayName));
            if (string.IsNullOrWhiteSpace(factionLabel)) throw new ArgumentException("A faction label is required.", nameof(factionLabel));
            if (string.IsNullOrWhiteSpace(roleLabel)) throw new ArgumentException("A role label is required.", nameof(roleLabel));
            if (!IsValidHealth(currentHealth, maxHealth)) throw new ArgumentOutOfRangeException(nameof(currentHealth), "Health must be finite; current >= 0 and maximum > 0.");
            SelectionId = selectionId;
            DisplayName = displayName;
            FactionLabel = factionLabel;
            RoleLabel = roleLabel;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            Description = description;
            CombatSummary = combatSummary;
            TraitSummary = traitSummary;
            Icon = icon;
        }

        internal static bool IsValidHealth(float current, float maximum) =>
            !float.IsNaN(current) && !float.IsInfinity(current) && current >= 0 &&
            !float.IsNaN(maximum) && !float.IsInfinity(maximum) && maximum > 0;
    }
}
