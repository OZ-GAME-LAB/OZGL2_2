using System;
using UnityEngine;

namespace Game.UI.InGame
{
    public enum UIId
    {
        None, Hud, BuildingCatalog, BuildingInfo, WaveReward,
        ArtifactReward, RunDecision, RunResult, Message, Detail
    }

    public enum UILayer { Hud, Popup, Modal }
    public enum UICloseReason { UserCancel, Replaced, Completed, ContextLost }

    /// <summary>같은 창을 다시 열어도 이전 요청으로 닫을 수 없도록 열림 회차를 함께 보관한다.</summary>
    public readonly struct UIHandle : IEquatable<UIHandle>
    {
        public UIId Id { get; }
        public int Version { get; }
        public bool IsValid => Id != UIId.None && Version > 0;
        public UIHandle(UIId id, int version) { Id = id; Version = version; }
        public bool Equals(UIHandle other) => Id == other.Id && Version == other.Version;
        public override bool Equals(object obj) => obj is UIHandle other && Equals(other);
        public override int GetHashCode() => ((int)Id * 397) ^ Version;
    }

    [Serializable]
    public sealed class UIRegistration
    {
        public UIId Id;
        public UIScreen Instance;
        public UIScreen Prefab;
        public Transform Parent;
        public UILayer Layer;
        public bool AllowUserClose = true;
        [NonSerialized] internal bool Created;
    }
}
