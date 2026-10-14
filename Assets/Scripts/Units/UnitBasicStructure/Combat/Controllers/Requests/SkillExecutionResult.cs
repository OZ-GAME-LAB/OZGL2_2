using System.Collections.Generic;
using UnityEngine;

// Action별 결과와 스킬 최종 결과를 저장하며 늦은 투사체 명중으로 완료 결과를 바꾸지 않는다.
namespace Units
{
    public enum ActionCompletionKind
    {
        Success,
        Failed,
        Skipped,
        Interrupted
    }
    public enum SkillCompletionKind
    {
        Success,
        Failed,
        Interrupted
    }
    public readonly struct ProjectileLaunchResult
    {

        // ============================================================
        // Properties
        // ============================================================

        public CombatTargetSnapshot Target { get; }

        public bool Fired { get; }

        public string Reason { get; }

        // ============================================================
        // Constructor
        // ============================================================

        public ProjectileLaunchResult(
            CombatTargetSnapshot target,
            bool fired,
            string reason = null)
        {
            Target = target;

            Fired = fired;

            Reason = reason;
        }
    }
    public sealed class ActionExecutionResult
    {

        // ============================================================
        // Properties
        // ============================================================

        public ActionCompletionKind Kind { get; }

        public string Reason { get; }

        public int ActionIndex { get; }

        public CombatTargetSnapshot Target { get; }

        public Vector2 Position { get; }

        public bool HasPosition { get; }

        public bool HitsObserved { get; }

        public IReadOnlyList<CombatTargetSnapshot> HitTargets { get; }

        public IReadOnlyList<CombatApplicationResult> Applications { get; }

        public int AppliedCount { get; }

        public IReadOnlyList<ProjectileLaunchResult> Launches { get; }

        // ============================================================
        // Constructor
        // ============================================================

        public ActionExecutionResult(
            ActionCompletionKind kind,
            int index,
            CombatTargetSnapshot target,
            Vector2 position,
            bool hasPosition,
            bool hitsObserved,
            IEnumerable<CombatTargetSnapshot> hits = null,
            IEnumerable<CombatApplicationResult> applications = null,
            string reason = null,
            IEnumerable<ProjectileLaunchResult> launches = null)
        {
            Launches = new List<ProjectileLaunchResult>(launches ?? System.Array.Empty<ProjectileLaunchResult>()).AsReadOnly();

            Kind = kind;

            ActionIndex = index;

            Target = target;

            Position = position;

            HasPosition = hasPosition;

            HitsObserved = hitsObserved;

            Reason = reason;

            HitTargets = new List<CombatTargetSnapshot>(hits ?? System.Array.Empty<CombatTargetSnapshot>()).AsReadOnly();

            Applications = new List<CombatApplicationResult>(applications ?? System.Array.Empty<CombatApplicationResult>()).AsReadOnly();

            foreach (var result in Applications)
                if (result.WasApplied)
                    AppliedCount++;
        }
    }
    public sealed class SkillExecutionResult
    {

        // ============================================================
        // Properties
        // ============================================================

        public long ExecutionId { get; }

        public SkillCompletionKind Kind { get; }

        public bool Moved { get; }

        public string Reason { get; }

        public IReadOnlyList<ActionExecutionResult> Actions { get; }

        // ============================================================
        // Constructor
        // ============================================================

        public SkillExecutionResult(
            long id,
            SkillCompletionKind kind,
            bool moved,
            IEnumerable<ActionExecutionResult> actions,
            string reason = null)
        {
            ExecutionId = id;

            Kind = kind;

            Moved = moved;

            Actions = new List<ActionExecutionResult>(actions).AsReadOnly();

            Reason = reason;
        }
    }
}
