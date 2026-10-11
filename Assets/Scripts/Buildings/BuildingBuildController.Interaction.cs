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
            BuildingInteractionOffer build = null, upgrade = null, demolish = null;
            BuildingData displayed = null;
            if (IsLiveInteractionSlot(slot))
            {
                foreach (BuildingData data in CollectInteractionCandidates(slot))
                {
                    BuildingInteractionCost cost;
                    bool valid = slot.IsOccupied
                        ? TryReadInteractionCosts(GetUpgradeCost(slot, data), out cost)
                        : TryReadConstructionCost(data, out cost, out _);
                    candidates.Add(new BuildingInteractionCandidate(data, cost, valid));
                }
                displayed = slot.IsOccupied ? target.ExpectedData : selectedOption;
                if (slot.IsOccupied)
                {
                    if (selectedOption != null)
                        upgrade = CreateInteractionOffer(target, BuildingInteractionAction.Upgrade, selectedOption);
                    if (!IsCoreSlot(slot))
                        demolish = CreateInteractionOffer(target, BuildingInteractionAction.Demolish, null);
                }
                else if (selectedOption != null)
                    build = CreateInteractionOffer(target, BuildingInteractionAction.Build, selectedOption);
            }
            return new BuildingInteractionState(target, failure, displayed, candidates, build, upgrade, demolish);
        }

        /// <summary>Build는 빈 슬롯 전용이다. 기존 TryBuild의 점유 슬롯 교체 계약은 그대로 유지한다.</summary>
        public BuildingInteractionResult TryExecuteInteraction(BuildingInteractionTarget target,
            BuildingInteractionAction action, BuildingData option = null)
        {
            BuildingInteractionFailure failure = EvaluateInteraction(target, action, option, out _);
            if (failure != BuildingInteractionFailure.None) return new BuildingInteractionResult(failure);
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

        private BuildingInteractionOffer CreateInteractionOffer(BuildingInteractionTarget target,
            BuildingInteractionAction action, BuildingData option)
        {
            var failure = EvaluateInteraction(target, action, option, out BuildingInteractionCost cost);
            return new BuildingInteractionOffer(action, option ?? target.ExpectedData, cost, failure);
        }

        private BuildingInteractionFailure EvaluateInteraction(BuildingInteractionTarget target,
            BuildingInteractionAction action, BuildingData option, out BuildingInteractionCost cost)
        {
            cost = default;
            var availability = ValidateInteractionTarget(target);
            if (target == null || !IsInteractionTargetCurrent(target)) return availability;
            BuildingSlot slot = target.Slot;
            BuildingData current = slot.IsOccupied ? slot.CurrentBuilding.Data : option;
            if (!TryReadConstructionCost(current, out BuildingInteractionCost buildCost,
                out BuildingInteractionCost refund)) return BuildingInteractionFailure.InvalidCost;

            switch (action)
            {
                case BuildingInteractionAction.Build:
                    cost = buildCost;
                    if (slot.IsOccupied) return BuildingInteractionFailure.TargetChanged;
                    if (option == null || !CollectInteractionCandidates(slot).Contains(option))
                        return BuildingInteractionFailure.CandidateUnavailable;
                    if (!HasBuildCapacity) return BuildingInteractionFailure.BuildCapacity;
                    if (!CanAffordCandidate(slot, option)) return BuildingInteractionFailure.InsufficientFunds;
                    break;
                case BuildingInteractionAction.Upgrade:
                    if (!slot.IsOccupied || option == null || !CollectInteractionCandidates(slot).Contains(option))
                        return BuildingInteractionFailure.CandidateUnavailable;
                    if (!TryReadInteractionCosts(GetUpgradeCost(slot, option), out cost))
                        return BuildingInteractionFailure.InvalidCost;
                    if (!CanAffordUpgrade(slot, option)) return BuildingInteractionFailure.InsufficientFunds;
                    break;
                case BuildingInteractionAction.Demolish:
                    cost = refund;
                    if (!slot.IsOccupied) return BuildingInteractionFailure.TargetChanged;
                    if (IsCoreSlot(slot)) return BuildingInteractionFailure.CoreProtected;
                    break;
                default: return BuildingInteractionFailure.CandidateUnavailable;
            }
            return availability;
        }

        private List<BuildingData> CollectInteractionCandidates(BuildingSlot slot)
        {
            var candidates = new List<BuildingData>();
            if (slot.IsOccupied)
            {
                if (slot.CurrentBuilding.Data != null)
                {
                    slot.CurrentBuilding.Data.CollectUpgrades(candidates, GetCurrentCoreLevel());
                    // Current date KDH 2026-10-08
                    // 상호작용 업그레이드 목록도 이미 지어진 지원 T2를 빼야 버튼이 남지 않습니다.
                    RemoveBuiltSupports(candidates);
                }
            }
            else slot.CollectCandidates(candidates, database, GetCurrentCoreLevel(), _census);

            var byId = new Dictionary<string, BuildingData>();
            var duplicates = new HashSet<string>();
            foreach (BuildingData data in candidates)
            {
                if (data == null || string.IsNullOrWhiteSpace(data.BuildingId) || string.IsNullOrWhiteSpace(data.DisplayName) ||
                    (!slot.IsOccupied && data.IsCore)) continue;
                if (byId.ContainsKey(data.BuildingId))
                {
                    if (!slot.IsOccupied) duplicates.Add(data.BuildingId);
                    continue;
                }
                byId.Add(data.BuildingId, data);
            }
            foreach (string id in duplicates) byId.Remove(id);
            return new List<BuildingData>(byId.Values);
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
