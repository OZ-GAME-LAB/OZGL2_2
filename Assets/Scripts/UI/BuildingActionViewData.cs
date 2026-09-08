using System;

namespace Game.UI
{
    public enum BuildingUiAction { Build, Upgrade, Dismantle }

    /// <summary>담당 시스템이 계산한 표시용 견적. UI는 비용/환급/실행 가능 여부를 계산하지 않는다.</summary>
    public sealed class BuildingActionOffer
    {
        public BuildingUiAction Action { get; }
        public string OptionId { get; }
        public string DisplayName { get; }
        public int GoldAmount { get; }
        public bool CanExecute { get; }
        public string DisabledReason { get; }

        public BuildingActionOffer(BuildingUiAction action, string displayName, int goldAmount,
            bool canExecute, string disabledReason = null, string optionId = null)
        {
            if (!Enum.IsDefined(typeof(BuildingUiAction), action)) throw new ArgumentOutOfRangeException(nameof(action));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("A display name is required.", nameof(displayName));
            if (goldAmount < 0) throw new ArgumentOutOfRangeException(nameof(goldAmount));
            if (!canExecute && string.IsNullOrWhiteSpace(disabledReason))
                throw new ArgumentException("A disabled reason is required.", nameof(disabledReason));
            if (action == BuildingUiAction.Build && string.IsNullOrWhiteSpace(optionId))
                throw new ArgumentException("Build requires a building option ID.", nameof(optionId));
            Action = action;
            DisplayName = displayName;
            GoldAmount = goldAmount;
            CanExecute = canExecute;
            DisabledReason = disabledReason;
            OptionId = optionId;
        }
    }

    public sealed class BuildingActionViewData
    {
        public string TargetId { get; }
        public string DisplayName { get; }
        public BuildingActionOffer Build { get; }
        public BuildingActionOffer Upgrade { get; }
        public BuildingActionOffer Dismantle { get; }

        public BuildingActionViewData(string targetId, string displayName, BuildingActionOffer build = null,
            BuildingActionOffer upgrade = null, BuildingActionOffer dismantle = null)
        {
            if (string.IsNullOrWhiteSpace(targetId)) throw new ArgumentException("An instance/slot ID is required.", nameof(targetId));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("A display name is required.", nameof(displayName));
            if ((build != null && build.Action != BuildingUiAction.Build) ||
                (upgrade != null && upgrade.Action != BuildingUiAction.Upgrade) ||
                (dismantle != null && dismantle.Action != BuildingUiAction.Dismantle))
                throw new ArgumentException("Offer action does not match its row.");
            TargetId = targetId;
            DisplayName = displayName;
            Build = build;
            Upgrade = upgrade;
            Dismantle = dismantle;
        }
    }

    /// <summary>견적의 금액은 실행 명령에 포함하지 않는다. 수신자가 현재 조건과 비용을 재검사한다.</summary>
    public sealed class BuildingActionRequest
    {
        public Guid RequestId { get; }
        public string TargetId { get; }
        public BuildingUiAction Action { get; }
        public string OptionId { get; }

        internal BuildingActionRequest(string targetId, BuildingActionOffer offer)
        {
            RequestId = Guid.NewGuid();
            TargetId = targetId;
            Action = offer.Action;
            OptionId = offer.OptionId;
        }
    }
}
