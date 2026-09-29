using System;
using System.Collections.Generic;
using Units.Skills;
using UnityEngine;

// 한 발의 명중·적용·종료를 기록하고 지연 Once 및 FX 정리를 한 번만 통지한다.
namespace Units
{
    // 투사체 1발의 일회 종료/명중 기록. 마지막 요청 참조가 해제되면 실행 횟수 기록도 회수된다.
    public sealed class ProjectileFlightState
    {

        // ============================================================
        // Data / Runtime State
        // ============================================================

        private readonly HashSet<(int, int)> _hits = new();

        private Action<bool> _completed;

        private Action _applied;

        private SkillEffectBatch _batch;

        private CombatEventMetadata _metadata;

        private Vector2 _position;

        // ============================================================
        // Execution
        // ============================================================

        internal void Configure(
            SkillEffectBatch batch,
            CombatEventMetadata metadata)
        {
            _batch = batch;

            _metadata = metadata;
        }

        // ============================================================
        // Notification
        // ============================================================

        public void NotifyFX(
            SkillFXHook hook,
            Vector2 position)
        {
            if (Ended)
                return;

            _position = position;

            var batch = _batch;

            if (batch == null)
                return;

            foreach (var fx in batch.FXEntries)
            {
                if (Ended)
                    break;

                if (fx != null && fx.Hook == hook)
                    batch.FXRequested?.Invoke(new SkillFXRequest(fx, _metadata, position));
            }
        }

        // ============================================================
        // Properties
        // ============================================================

        public bool Ended { get; private set; }

        public bool Applied { get; private set; }

        // ============================================================
        // Constructor
        // ============================================================

        public ProjectileFlightState(
            Action<bool> completed = null,
            Action applied = null)
        {
            _completed = completed;

            _applied = applied;
        }

        // ============================================================
        // Execution
        // ============================================================

        public bool Enter(CombatTargetSnapshot target) => !Ended && _hits.Add((target.ObjectId, target.LifetimeVersion));

        // 첫 실제 적용만 알리므로 Once 패시브가 거절된 타격에 소모되지 않는다.
        public void Record(CombatApplicationResult result)
        {
            if (!Ended && !Applied && result.WasApplied)
            {
                Applied = true;

                var callback = _applied;

                _applied = null;

                callback?.Invoke();
            }
        }

        // ============================================================
        // Cleanup
        // ============================================================

        // 충돌·소멸·풀 반환이 겹쳐도 정리와 완료 알림은 한 번만 보낸다.
        public void Complete()
        {
            if (Ended)
                return;

            Ended = true;

            _applied = null;

            var callback = _completed;

            _completed = null;

            _hits.Clear();

            var batch = _batch;

            _batch = null;

            try
            {
                batch?.CleanupConditionalFX();

                if (batch != null)
                    foreach (var fx in batch.FXEntries)
                        if (fx != null && (fx.Hook == SkillFXHook.Flight || fx.EndPolicy == SkillFXEndPolicy.Independent))
                            batch.FXRequested?.Invoke(new SkillFXRequest(fx, _metadata, _position, true));
            }
            finally
            {
                callback?.Invoke(Applied);
            }
        }
    }
}
