using System;
using System.Collections.Generic;

namespace OZGL.KDH
{
    public enum BuildingInteractionAction { Build, Upgrade, Demolish }

    public enum BuildingInteractionFailure
    {
        None, NotReady, PhaseBlocked, InvalidTarget, TargetChanged, InvalidData,
        InvalidCost, CandidateUnavailable, CoreProtected, BuildCapacity,
        InsufficientFunds, Busy, ExecutionFailed, UnexpectedError
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
        internal BuildingInteractionCandidate(BuildingData data, BuildingInteractionCost cost, bool valid)
        { Data = data; Cost = cost; HasValidCost = valid; }
    }

    public sealed class BuildingInteractionOffer
    {
        public BuildingInteractionAction Action { get; }
        public BuildingData Data { get; }
        public BuildingInteractionCost Cost { get; }
        public BuildingInteractionFailure Failure { get; }
        public bool CanExecute => Failure == BuildingInteractionFailure.None;
        internal BuildingInteractionOffer(BuildingInteractionAction action, BuildingData data,
            BuildingInteractionCost cost, BuildingInteractionFailure failure)
        { Action = action; Data = data; Cost = cost; Failure = failure; }
    }

    public sealed class BuildingInteractionState
    {
        public BuildingInteractionTarget Target { get; }
        public BuildingInteractionFailure Failure { get; }
        public bool CanInteract => Failure == BuildingInteractionFailure.None;
        public BuildingData DisplayData { get; }
        public IReadOnlyList<BuildingInteractionCandidate> Candidates { get; }
        public BuildingInteractionOffer Build { get; }
        public BuildingInteractionOffer Upgrade { get; }
        public BuildingInteractionOffer Demolish { get; }

        internal BuildingInteractionState(BuildingInteractionTarget target, BuildingInteractionFailure failure,
            BuildingData displayData, List<BuildingInteractionCandidate> candidates,
            BuildingInteractionOffer build, BuildingInteractionOffer upgrade, BuildingInteractionOffer demolish)
        {
            Target = target; Failure = failure; DisplayData = displayData;
            Candidates = candidates.AsReadOnly(); Build = build; Upgrade = upgrade; Demolish = demolish;
        }
    }

    public readonly struct BuildingInteractionResult
    {
        public BuildingInteractionFailure Failure { get; }
        public bool Succeeded => Failure == BuildingInteractionFailure.None;
        // 기존 Try API의 실패는 부분 차감이 없었다는 보장이 아니다. 자동 재시도하지 않는다.
        public bool MayHaveChangedState => Succeeded || Failure == BuildingInteractionFailure.ExecutionFailed ||
            Failure == BuildingInteractionFailure.UnexpectedError;
        internal BuildingInteractionResult(BuildingInteractionFailure failure) { Failure = failure; }
    }
}
