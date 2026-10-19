using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZGL.KDH
{
    public partial class BuildingBuildController
    {
        private bool _interactionExecuting;

        public bool CanInteract => InteractionAvailability() == BuildingInteractionFailure.None;

        /// <summary>UI와 실행이 같은 후보·비용·조건 검사를 사용한다. 조회는 게임 상태를 바꾸지 않는다.</summary>
        public BuildingInteractionState QueryInteraction(BuildingSlot slot, BuildingData selectedOption = null)
        {
            var target = new BuildingInteractionTarget(slot);
            var failure = ValidateInteractionTarget(target);
            var candidates = new List<BuildingInteractionCandidate>();
            var displayCandidates = new List<BuildingInteractionCandidate>();
            BuildingInteractionOffer build = null, upgrade = null, demolish = null;
            BuildingData displayed = null;
            int? requiredUpgradeCoreLevel = null;
            if (IsLiveInteractionSlot(slot))
            {
                // 현재 슬롯의 전체 후보를 수집하고 실행 조건을 함께 판정한다.
                displayCandidates = CollectInteractionCandidates(target, failure);
                foreach (BuildingInteractionCandidate candidate in displayCandidates)
                {
                    if (candidate.IsCoreLevelMet) candidates.Add(candidate);
                    else if (!requiredUpgradeCoreLevel.HasValue || candidate.RequiredCoreLevel < requiredUpgradeCoreLevel.Value)
                        requiredUpgradeCoreLevel = candidate.RequiredCoreLevel;
                }
                if (!slot.IsOccupied || candidates.Count > 0) requiredUpgradeCoreLevel = null;
                displayed = slot.IsOccupied ? target.ExpectedData : selectedOption;
                if (slot.IsOccupied)
                {
                    if (selectedOption != null)
                        upgrade = EvaluateInteraction(target, BuildingInteractionAction.Upgrade, selectedOption, displayCandidates, failure);
                    if (!IsCoreSlot(slot))
                        demolish = EvaluateInteraction(target, BuildingInteractionAction.Demolish, null, displayCandidates, failure);
                }
                else if (selectedOption != null)
                    build = EvaluateInteraction(target, BuildingInteractionAction.Build, selectedOption, displayCandidates, failure);
            }
            return new BuildingInteractionState(target, failure, displayed, candidates, build, upgrade, demolish,
                requiredUpgradeCoreLevel, displayCandidates);
        }

        /// <summary>Build는 빈 슬롯 전용이다. 기존 TryBuild의 점유 슬롯 교체 계약은 그대로 유지한다.</summary>
        public BuildingInteractionResult TryExecuteInteraction(BuildingInteractionTarget target,
            BuildingInteractionAction action, BuildingData option = null)
        {
            BuildingInteractionFailure availability = ValidateInteractionTarget(target);
            if (!IsInteractionTargetCurrent(target)) return new BuildingInteractionResult(availability);
            var candidates = action == BuildingInteractionAction.Demolish
                ? null : CollectInteractionCandidates(target, availability);
            BuildingInteractionOffer offer = EvaluateInteraction(target, action, option, candidates, availability);
            if (!offer.CanExecute) return new BuildingInteractionResult(offer.Failure, offer.RequiredCoreLevel);
            _interactionExecuting = true;
            try
            {
                bool succeeded;
                switch (action)
                {
                    case BuildingInteractionAction.Build: succeeded = TryBuild(target.Slot, option); break;
                    case BuildingInteractionAction.Upgrade: succeeded = TryUpgrade(target.Slot, option); break;
                    case BuildingInteractionAction.Demolish: succeeded = TryDemolish(target.Slot); break;
                    default: return new BuildingInteractionResult(BuildingInteractionFailure.CandidateUnavailable);
                }
                return new BuildingInteractionResult(succeeded
                    ? BuildingInteractionFailure.None : BuildingInteractionFailure.ExecutionFailed);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return new BuildingInteractionResult(BuildingInteractionFailure.UnexpectedError);
            }
            finally { _interactionExecuting = false; }
        }

        public bool IsInteractionTargetCurrent(BuildingInteractionTarget target)
        {
            if (target == null || !IsLiveInteractionSlot(target.Slot)) return false;
            Building expected = target.ExpectedBuilding;
            if (!ReferenceEquals(expected, null) && expected == null) return false;
            return ReferenceEquals(target.Slot.CurrentBuilding, expected) &&
                (expected == null || ReferenceEquals(expected.Data, target.ExpectedData));
        }

        public static bool IsLiveInteractionSlot(BuildingSlot slot)
        {
            if (slot == null || !slot.isActiveAndEnabled) return false;
            Collider2D collider = slot.GetComponent<Collider2D>();
            return collider != null && collider.enabled;
        }

        private BuildingInteractionFailure InteractionAvailability()
        {
            if (_interactionExecuting) return BuildingInteractionFailure.Busy;
            if (!isActiveAndEnabled || wallet == null || !wallet.isActiveAndEnabled || !wallet.IsInitialized ||
                gameFlow == null || !gameFlow.isActiveAndEnabled) return BuildingInteractionFailure.NotReady;
            return gameFlow.CanEnterBuildMode() ? BuildingInteractionFailure.None : BuildingInteractionFailure.PhaseBlocked;
        }

        private BuildingInteractionFailure ValidateInteractionTarget(BuildingInteractionTarget target)
        {
            if (target == null || !IsLiveInteractionSlot(target.Slot)) return BuildingInteractionFailure.InvalidTarget;
            if (!IsInteractionTargetCurrent(target)) return BuildingInteractionFailure.TargetChanged;
            return InteractionAvailability();
        }

        private BuildingInteractionOffer EvaluateInteraction(BuildingInteractionTarget target,
            BuildingInteractionAction action, BuildingData option,
            IReadOnlyList<BuildingInteractionCandidate> candidates, BuildingInteractionFailure availability)
        {
            BuildingSlot slot = target.Slot;
            BuildingData data = action == BuildingInteractionAction.Demolish ? target.ExpectedData : option;
            BuildingInteractionCost cost = default;
            BuildingInteractionFailure failure;
            if (action == BuildingInteractionAction.Demolish)
            {
                failure = !slot.IsOccupied ? BuildingInteractionFailure.TargetChanged :
                    !TryReadConstructionCost(data, out _, out cost) ? BuildingInteractionFailure.InvalidCost :
                    IsCoreSlot(slot) ? BuildingInteractionFailure.CoreProtected : availability;
            }
            else if (action == BuildingInteractionAction.Build && slot.IsOccupied)
                failure = BuildingInteractionFailure.TargetChanged;
            else
            {
                if (action == (slot.IsOccupied ? BuildingInteractionAction.Upgrade : BuildingInteractionAction.Build))
                    foreach (BuildingInteractionCandidate candidate in candidates)
                        if (candidate.Data == option) return candidate.Offer;
                failure = BuildingInteractionFailure.CandidateUnavailable;
            }
            return new BuildingInteractionOffer(action, data, cost, failure);
        }

        private List<BuildingInteractionCandidate> CollectInteractionCandidates(BuildingInteractionTarget target,
            BuildingInteractionFailure availability)
        {
            BuildingSlot slot = target.Slot;
            int coreLevel = GetCurrentCoreLevel();
            var byId = new Dictionary<string, BuildingInteractionCandidate>();
            if (slot.IsOccupied)
            {
                BuildingData current = target.ExpectedData;
                var executionCosts = new Dictionary<string, BuildingResourceCost[]>();
                foreach (BuildingUpgradeOption option in current?.Upgrades ?? Array.Empty<BuildingUpgradeOption>())
                {
                    if (!current.IsUpgradeAvailable(option, int.MaxValue)) continue;
                    BuildingData next = option.nextBuilding;
                    bool coreLevelMet = coreLevel >= option.requiredCoreLevel;
                    if (string.IsNullOrWhiteSpace(next.BuildingId)) continue;
                    // TryGetUpgradeCost는 표시명과 관계없이 같은 ID의 첫 해제 경로를 사용한다.
                    if (coreLevelMet && !executionCosts.ContainsKey(next.BuildingId))
                        executionCosts.Add(next.BuildingId, option.cost);
                    if (string.IsNullOrWhiteSpace(next.DisplayName) ||
                        IsSupportAlreadyBuilt(next)) continue;
                    if (byId.TryGetValue(next.BuildingId, out BuildingInteractionCandidate existing) &&
                        (existing.IsCoreLevelMet || (!coreLevelMet && existing.RequiredCoreLevel <= option.requiredCoreLevel)))
                        continue;
                    bool validCost = TryReadInteractionCosts(coreLevelMet ? executionCosts[next.BuildingId] : option.cost,
                        out BuildingInteractionCost cost);
                    byId[next.BuildingId] = CreateInteractionCandidate(target, next, cost, validCost,
                        option.requiredCoreLevel, coreLevelMet, availability);
                }
            }
            else
            {
                var buildings = new List<BuildingData>();
                // 표시 후보에서만 사령부 제한을 완화한다. 슬롯/가문/빈 칸 건설 제한은 그대로 사용한다.
                slot.CollectCandidates(buildings, database, int.MaxValue, _census);
                var duplicates = new HashSet<string>();
                foreach (BuildingData data in buildings)
                {
                    if (data == null || string.IsNullOrWhiteSpace(data.BuildingId) ||
                        string.IsNullOrWhiteSpace(data.DisplayName) || data.IsCore) continue;
                    if (byId.ContainsKey(data.BuildingId)) { duplicates.Add(data.BuildingId); continue; }
                    bool validCost = TryReadConstructionCost(data, out BuildingInteractionCost cost, out _);
                    byId.Add(data.BuildingId, CreateInteractionCandidate(target, data, cost, validCost,
                        data.RequiredCoreLevel, coreLevel >= data.RequiredCoreLevel, availability));
                }
                foreach (string id in duplicates) byId.Remove(id);
            }
            return new List<BuildingInteractionCandidate>(byId.Values);
        }

        private BuildingInteractionCandidate CreateInteractionCandidate(BuildingInteractionTarget target,
            BuildingData data, BuildingInteractionCost cost, bool validCost, int requiredCoreLevel,
            bool coreLevelMet, BuildingInteractionFailure availability)
        {
            BuildingSlot slot = target.Slot;
            BuildingInteractionFailure failure;
            if (!validCost || (slot.IsOccupied && !TryReadConstructionCost(target.ExpectedData, out _, out _)))
                failure = BuildingInteractionFailure.InvalidCost;
            else if (!coreLevelMet)
                failure = BuildingInteractionFailure.CoreLevelRequired;
            else if (!slot.IsOccupied && !HasBuildCapacity)
                failure = BuildingInteractionFailure.BuildCapacity;
            else if (!HasWallet() || (cost.Gold > 0 && !wallet.CanSpend(CurrencyType.Gold, cost.Gold)) ||
                (cost.Gems > 0 && !wallet.CanSpend(CurrencyType.Gem, cost.Gems)))
                failure = BuildingInteractionFailure.InsufficientFunds;
            else
                failure = availability;
            var action = slot.IsOccupied ? BuildingInteractionAction.Upgrade : BuildingInteractionAction.Build;
            var offer = new BuildingInteractionOffer(action, data, cost, failure,
                failure == BuildingInteractionFailure.CoreLevelRequired ? requiredCoreLevel : (int?)null);
            return new BuildingInteractionCandidate(data, cost, validCost, requiredCoreLevel, coreLevelMet, offer);
        }

        private bool TryReadConstructionCost(BuildingData data, out BuildingInteractionCost cost,
            out BuildingInteractionCost refund)
        {
            cost = refund = default;
            if (data == null || string.IsNullOrWhiteSpace(data.BuildingId) || string.IsNullOrWhiteSpace(data.DisplayName) ||
                (data.Prefab == null && data.WorldSprite == null) || float.IsNaN(refundRate) || float.IsInfinity(refundRate) ||
                !TryReadInteractionCosts(data.BuildCost, out cost)) return false;
            refund = new BuildingInteractionCost(Mathf.FloorToInt(cost.Gold * Mathf.Clamp01(refundRate)),
                Mathf.FloorToInt(cost.Gems * Mathf.Clamp01(refundRate)));
            if (refund.Gold >= 0 && refund.Gems >= 0) return true;
            cost = refund = default;
            return false;
        }

        /// <summary>기존 UI의 중복·음수·알 수 없는 비용 차단을 시스템 경계에서 유지한다.</summary>
        internal static bool TryReadInteractionCosts(BuildingResourceCost[] costs, out BuildingInteractionCost total)
        {
            total = default;
            int gold = 0, gems = 0;
            bool hasGold = false, hasGems = false;
            foreach (BuildingResourceCost cost in costs ?? Array.Empty<BuildingResourceCost>())
            {
                if (cost.amount < 0) return false;
                switch (cost.type)
                {
                    case BuildingResourceType.Gold:
                        if (hasGold) return false;
                        hasGold = true; gold = cost.amount; break;
                    case BuildingResourceType.Gem:
                        if (hasGems) return false;
                        hasGems = true; gems = cost.amount; break;
                    default: return false;
                }
            }
            total = new BuildingInteractionCost(gold, gems);
            return true;
        }
    }
}
