using Units.Skills;
using UnityEngine;

// 스킬 단계와 처치 대상 스냅샷을 담아 원래 소유자의 유효한 수명에만 통지한다.
namespace Units
{
    public sealed class CombatSkillEvent
    {

        // ============================================================
        // Properties
        // ============================================================

        public PassiveSkillTriggerType EventType { get; }

        public CombatEventMetadata Metadata { get; }

        public CombatTargetSnapshot InitialTarget { get; }

        public CombatTargetSnapshot ActionTarget { get; }

        public CombatTargetSnapshot HitTarget { get; }

        public Vector2 ResultPosition { get; }

        public SkillCompletionKind? CompletionKind { get; }

        public CombatDeathResult Death { get; }

        public CombatTargetSnapshot Target => EventType == PassiveSkillTriggerType.EnemyKilled ? Death.Victim : EventType == PassiveSkillTriggerType.ActiveSkillActionHit ? HitTarget : ActionTarget.ObjectId != 0 ? ActionTarget : InitialTarget;

        // ============================================================
        // Constructor
        // ============================================================

        public CombatSkillEvent(
            PassiveSkillTriggerType type,
            CombatEventMetadata metadata,
            CombatTargetSnapshot initial = default,
            CombatTargetSnapshot action = default,
            CombatTargetSnapshot hit = default,
            Vector2 position = default,
            SkillCompletionKind? completion = null,
            CombatDeathResult death = null)
        {
            EventType = type;

            Metadata = metadata;

            InitialTarget = initial;

            ActionTarget = action;

            HitTarget = hit;

            ResultPosition = position;

            CompletionKind = completion;

            Death = death;
        }

        // ============================================================
        // Notification
        // ============================================================

        public void Notify()
        {
            if (!Metadata.Owner.IsTargetable)
                return;

            using (CombatEventContext.Enter(Metadata))
                Metadata.Owner.Target.NotifySkillEvent(this);
        }

        public void NotifyHit(
            CombatTargetSnapshot target,
            CombatEventMetadata metadata,
            Vector2 position) => new CombatSkillEvent(
            PassiveSkillTriggerType.ActiveSkillActionHit,
            metadata,
            InitialTarget,
            ActionTarget,
            target,
            position
        ).Notify();
    }
}
