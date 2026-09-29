using System;
using System.Collections.Generic;
using Units.Skills;
using UnityEngine;

// 탐색 정책·원점·대상 수명을 요청으로 묶고 선택 결과 목록을 독립적으로 보관한다.
namespace Units
{
    public readonly struct SkillTargetRequest
    {

        // ============================================================
        // Properties
        // ============================================================

        public CombatTargetSnapshot Owner { get; }

        public bool IndependentSource { get; }

        public bool CanSelect => IndependentSource || Owner.IsTargetable;

        public CombatTargetSnapshot InitialTarget { get; }

        public CombatTargetSnapshot CurrentTarget { get; }

        public CombatTargetSnapshot PreviousTarget { get; }

        public CombatTargetSnapshot TriggerTarget { get; }

        public SkillTargetSettings Settings { get; }

        public SkillAreaSettings Area { get; }

        public Vector2 Origin { get; }

        public Vector2 Direction { get; }

        public Predicate<ICombatTarget> HostileFilter { get; }

        // 원점/방향은 호출자가 명시한다. 액티브 대상 방향/패시브 right/투사체 충돌점을 혼합하지 않는다.
        public SkillTargetRequest(
            ICombatTarget owner,
            SkillTargetSettings settings,
            Vector2 origin,
            Vector2 direction,
            SkillAreaSettings area = null,
            CombatTargetSnapshot initialTarget = default,
            CombatTargetSnapshot currentTarget = default,
            CombatTargetSnapshot previousTarget = default,
            CombatTargetSnapshot triggerTarget = default,
            Predicate<ICombatTarget> hostileFilter = null,
            CombatSourceSnapshot sourceSnapshot = null)
        {
            IndependentSource = sourceSnapshot != null;

            Owner = sourceSnapshot?.Owner ?? new CombatTargetSnapshot(owner);

            Settings = settings;

            Area = area;

            Origin = origin;

            Direction = direction;

            InitialTarget = initialTarget;

            CurrentTarget = currentTarget;

            PreviousTarget = previousTarget;

            TriggerTarget = triggerTarget;

            HostileFilter = hostileFilter;
        }
    }

    public sealed class SkillTargetResult
    {

        // ============================================================
        // Properties
        // ============================================================

        public bool Success { get; }

        public string FailureReason { get; }

        public CombatTargetSnapshot PrimaryTarget { get; }

        public IReadOnlyList<CombatTargetSnapshot> Targets { get; }

        public Vector2 Origin { get; }

        public Vector2 Direction { get; }

        // ============================================================
        // Constructor
        // ============================================================

        public SkillTargetResult(
            IEnumerable<ICombatTarget> targets,
            Vector2 origin,
            Vector2 direction,
            bool targetless = false,
            string failureReason = null)
        {
            var copy = new List<CombatTargetSnapshot>();

            if (targets != null)
                foreach (var target in targets)
                    copy.Add(new CombatTargetSnapshot(target));

            Targets = copy.AsReadOnly();

            PrimaryTarget = copy.Count > 0 ? copy[0] : default;

            Origin = origin;

            Direction = direction;

            Success = targetless || copy.Count > 0;

            FailureReason = Success ? null : failureReason ?? "No valid target";
        }
    }
}
