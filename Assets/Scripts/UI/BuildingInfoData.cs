using System;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// UI 전용 읽기 스냅샷. 건물 SO나 실제 건물 상태를 대체하지 않는다.
    /// SelectionId는 건물 종류가 아니라 선택한 인스턴스/슬롯의 식별자다.
    /// </summary>
    public sealed class BuildingInfoData
    {
        public string SelectionId { get; }
        public string DisplayName { get; }
        public string CategoryLabel { get; }
        public int Level { get; }
        public string Description { get; }
        public string ProductionSummary { get; }
        public string EffectSummary { get; }
        public Sprite Icon { get; }

        public BuildingInfoData(string selectionId, string displayName, string categoryLabel,
            int level, string description = null, string productionSummary = null,
            string effectSummary = null, Sprite icon = null)
        {
            if (string.IsNullOrWhiteSpace(selectionId))
                throw new ArgumentException("A selection instance/slot ID is required.", nameof(selectionId));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("A display name is required.", nameof(displayName));
            if (string.IsNullOrWhiteSpace(categoryLabel))
                throw new ArgumentException("A category label is required.", nameof(categoryLabel));
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));

            SelectionId = selectionId;
            DisplayName = displayName;
            CategoryLabel = categoryLabel;
            Level = level;
            Description = description;
            ProductionSummary = productionSummary;
            EffectSummary = effectSummary;
            Icon = icon;
        }
    }
}
