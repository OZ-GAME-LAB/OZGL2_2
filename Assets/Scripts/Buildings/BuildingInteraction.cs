using System;
using System.Collections.Generic;

namespace OZGL.KDH
{
    public enum BuildingInteractionAction { Build, Upgrade, Demolish }

    public enum BuildingInteractionFailure
    {
        None, NotReady, PhaseBlocked, InvalidTarget, TargetChanged, InvalidData,
        InvalidCost, CandidateUnavailable, CoreProtected, BuildCapacity,
        InsufficientFunds, Busy, ExecutionFailed, UnexpectedError, CoreLevelRequired
    }

    /// <summary>선택 당시의 대상. 전역 버전 대신 슬롯·건물·데이터 참조를 실행 직전에 비교한다.</summary>
    public sealed class BuildingInteractionTarget
    {
        public BuildingSlot Slot { get; }
        public Building ExpectedBuilding { get; }
        public BuildingData ExpectedData { get; }

        internal BuildingInteractionTarget(BuildingSlot slot)
        {
            Slot = slot;
            ExpectedBuilding = slot != null ? slot.CurrentBuilding : null;
            ExpectedData = ExpectedBuilding != null ? ExpectedBuilding.Data : null;
        }
    }

    public readonly struct BuildingInteractionCost
    {
        public int Gold { get; }
        public int Gems { get; }
        public BuildingInteractionCost(int gold, int gems) { Gold = gold; Gems = gems; }
    }

    public sealed class BuildingInteractionCandidate
    {
        public BuildingData Data { get; }
        public BuildingInteractionCost Cost { get; }
        public bool HasValidCost { get; }
        public int RequiredCoreLevel { get; }
        public bool IsCoreLevelMet { get; }
        public BuildingInteractionOffer Offer { get; }
        internal BuildingInteractionCandidate(BuildingData data, BuildingInteractionCost cost, bool valid,
            int requiredCoreLevel = 0, bool isCoreLevelMet = true)
            : this(data, cost, valid, requiredCoreLevel, isCoreLevelMet, null) { }

        internal BuildingInteractionCandidate(BuildingData data, BuildingInteractionCost cost, bool valid,
            int requiredCoreLevel, bool isCoreLevelMet, BuildingInteractionOffer offer)
        {
            Data = data; Cost = cost; HasValidCost = valid;
            RequiredCoreLevel = requiredCoreLevel; IsCoreLevelMet = isCoreLevelMet;
            Offer = offer;
        }
    }

    public sealed class BuildingInteractionOffer
    {
        public BuildingInteractionAction Action { get; }
        public BuildingData Data { get; }
        public BuildingInteractionCost Cost { get; }
        public BuildingInteractionFailure Failure { get; }
        public int? RequiredCoreLevel { get; }
        public bool CanExecute => Failure == BuildingInteractionFailure.None;
        internal BuildingInteractionOffer(BuildingInteractionAction action, BuildingData data,
            BuildingInteractionCost cost, BuildingInteractionFailure failure)
            : this(action, data, cost, failure, null) { }

        internal BuildingInteractionOffer(BuildingInteractionAction action, BuildingData data,
            BuildingInteractionCost cost, BuildingInteractionFailure failure, int? requiredCoreLevel)
        {
            Action = action; Data = data; Cost = cost; Failure = failure;
            RequiredCoreLevel = requiredCoreLevel;
        }
    }

    public sealed class BuildingInteractionState
    {
        public BuildingInteractionTarget Target { get; }
        public BuildingInteractionFailure Failure { get; }
        public bool CanInteract => Failure == BuildingInteractionFailure.None;
        public BuildingData DisplayData { get; }
        public IReadOnlyList<BuildingInteractionCandidate> Candidates { get; }
        public IReadOnlyList<BuildingInteractionCandidate> DisplayCandidates { get; }
        public BuildingInteractionOffer Build { get; }
        public BuildingInteractionOffer Upgrade { get; }
        public BuildingInteractionOffer Demolish { get; }
        public int? RequiredUpgradeCoreLevel { get; }

        internal BuildingInteractionState(BuildingInteractionTarget target, BuildingInteractionFailure failure,
            BuildingData displayData, List<BuildingInteractionCandidate> candidates,
            BuildingInteractionOffer build, BuildingInteractionOffer upgrade, BuildingInteractionOffer demolish,
            int? requiredUpgradeCoreLevel = null, List<BuildingInteractionCandidate> displayCandidates = null)
        {
            Target = target; Failure = failure; DisplayData = displayData;
            Candidates = candidates.AsReadOnly(); Build = build; Upgrade = upgrade; Demolish = demolish;
            RequiredUpgradeCoreLevel = requiredUpgradeCoreLevel;
            DisplayCandidates = displayCandidates == null ? Candidates : displayCandidates.AsReadOnly();
        }
    }

    public readonly struct BuildingInteractionResult
    {
        public BuildingInteractionFailure Failure { get; }
        public int? RequiredCoreLevel { get; }
        public bool Succeeded => Failure == BuildingInteractionFailure.None;
        // 기존 Try API의 실패는 부분 차감이 없었다는 보장이 아니다. 자동 재시도하지 않는다.
        public bool MayHaveChangedState => Succeeded || Failure == BuildingInteractionFailure.ExecutionFailed ||
            Failure == BuildingInteractionFailure.UnexpectedError;
        internal BuildingInteractionResult(BuildingInteractionFailure failure) : this(failure, null) { }
        internal BuildingInteractionResult(BuildingInteractionFailure failure, int? requiredCoreLevel)
        { Failure = failure; RequiredCoreLevel = requiredCoreLevel; }
    }
}
